#!/usr/bin/env bash
# scripts/demo.sh — one-shot demo capture (screenshots + video).
#
# Reads docs/screenshots.config.json. Edit that file (not this script) to change
# which pages get captured.
#
# Flow: stop stale app on port → reset SQLite DB → start app → wait-for-ready
#       → run capture.js → stop app. Idempotent; safe to re-run.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

PORT="${PORT:-5177}"
APP_URL="http://localhost:${PORT}"
WEB_PROJECT="src/CopilotBlazorTemplate.Web"
DB_PATH="${WEB_PROJECT}/Data/app.db"
PW_PREFIX="${PW_PREFIX:-/tmp/pw-runner}"
APP_LOG="${APP_LOG:-/tmp/demo-app.log}"
APP_PID=""

log() { printf '\033[1;36m[demo]\033[0m %s\n' "$*"; }
err() { printf '\033[1;31m[demo]\033[0m %s\n' "$*" >&2; }

cleanup() {
  if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
    log "Stopping app (pid $APP_PID)"
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

kill_port() {
  if command -v lsof >/dev/null 2>&1; then
    local pids
    pids="$(lsof -ti tcp:"$PORT" 2>/dev/null || true)"
    if [[ -n "$pids" ]]; then
      log "Killing process(es) on :$PORT — $pids"
      kill $pids 2>/dev/null || true
      sleep 1
    fi
  fi
}

ensure_playwright() {
  # Prefer project-local node_modules if present (won't add one if absent).
  if node -e "require('playwright')" 2>/dev/null; then
    log "playwright: found in project node_modules"
    return
  fi
  if node -e "require('${PW_PREFIX}/node_modules/playwright')" 2>/dev/null; then
    log "playwright: found in ${PW_PREFIX}"
  else
    log "Installing playwright into ${PW_PREFIX} (one-time, ~30s)"
    mkdir -p "$PW_PREFIX"
    npm install --silent --no-audit --no-fund --prefix "$PW_PREFIX" playwright >/dev/null
  fi
  # Chromium browser (install is idempotent and quick if already present)
  log "Ensuring chromium is installed"
  PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-}" \
    "${PW_PREFIX}/node_modules/.bin/playwright" install chromium >/dev/null 2>&1 || \
    npx --yes playwright install chromium >/dev/null 2>&1 || true
}

reset_db() {
  if [[ -f "$DB_PATH" ]]; then
    log "Removing existing DB: $DB_PATH"
    rm -f "$DB_PATH"
  fi
  mkdir -p "$(dirname "$DB_PATH")"
}

start_app() {
  log "Starting app on $APP_URL (log: $APP_LOG)"
  ASPNETCORE_URLS="$APP_URL" \
  ASPNETCORE_ENVIRONMENT="Development" \
    nohup dotnet run --project "$WEB_PROJECT" --no-launch-profile \
    >"$APP_LOG" 2>&1 &
  APP_PID=$!
  log "App pid: $APP_PID"
}

wait_for_ready() {
  log "Waiting for app to respond (up to 90s)"
  for i in $(seq 1 45); do
    if curl -sf "$APP_URL" >/dev/null 2>&1; then
      log "App ready after ${i}x2s"
      return 0
    fi
    if ! kill -0 "$APP_PID" 2>/dev/null; then
      err "App process died — see $APP_LOG"
      tail -n 50 "$APP_LOG" >&2 || true
      exit 1
    fi
    sleep 2
  done
  err "App did not become ready — see $APP_LOG"
  tail -n 50 "$APP_LOG" >&2 || true
  exit 1
}

main() {
  log "Repo: $REPO_ROOT"
  kill_port
  reset_db
  ensure_playwright
  start_app
  wait_for_ready
  log "Running capture script"
  APP_URL="$APP_URL" node .github/skills/screenshots-demo/capture.js
  log "Done. Artifacts in docs/screenshots/ and docs/demo/"
}

main "$@"
