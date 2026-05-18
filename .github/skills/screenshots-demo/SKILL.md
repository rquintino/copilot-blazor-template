---
name: screenshots-demo
description: Capture page screenshots (PNG) and record the full-flow demo video (WebM) for the app. Use when UI pages were added or changed, when refreshing the README's screenshot row, or when the user asks for a demo. Config-driven via `docs/screenshots.config.json` — edit that file (not the script) to change what gets captured. Entry point is `bash scripts/demo.sh`. Includes a required Playwright MCP pre-flight verification of every configured page before capture, and a required post-capture screenshot inspection. NEVER attempts the capture batch through Playwright MCP itself — see the MCP singleton-lock callout in the body.
---

# Screenshots & Demo Video Capture Skill

## Overview
Captures page screenshots (PNG) and records a full-flow demo video (WebM) for the app using Playwright. **Config-driven** — edit `docs/screenshots.config.json` to change what gets captured. No code changes needed when you add new pages.

## TL;DR — one command
```bash
bash scripts/demo.sh
```
That's it. The script kills any stale process on :5177, resets the SQLite DB so seed data is deterministic, installs Playwright + Chromium into `/tmp/pw-runner` if missing, starts the app, waits for it to respond, runs the capture, and stops the app on exit (including on Ctrl-C).

Outputs land in `docs/screenshots/<name>.png` and `docs/demo/<outputFile>`.

> **Capture goes through `scripts/demo.sh`, never through Playwright MCP.** The Copilot cloud agent's default Playwright MCP server uses a singleton chromium profile and returns `Browser is already in use for /root/.cache/ms-playwright/mcp-chrome` once it has been touched. `--isolated` is not user-configurable in that MCP. Do **not** try to clear lock files, kill chrome processes, or wipe the profile dir — none of it recovers within the session. The npm-Playwright install used by `scripts/demo.sh` lives in `/tmp/pw-runner` (pre-installed by `.github/workflows/copilot-setup-steps.yml`), so it is independent of the MCP profile and the first call is fast. Use MCP for **single-page pre-flight verification only**; use `scripts/demo.sh` for the actual capture batch.

## REQUIRED — pre-flight page verification

**Do not run `scripts/demo.sh` until every page listed in `docs/screenshots.config.json` (and every step in `demo.steps`) has been opened in Playwright MCP and visually confirmed working.** A passing build is not enough — runtime errors, blank components, missing data, broken nav, and auth redirects only show up in the rendered page. Past demo videos have shipped with broken pages because this step was skipped.

Procedure:

1. Start the app the same way `scripts/demo.sh` would: kill :5177, delete `src/CopilotBlazorTemplate.Web/Data/app.db`, then `dotnet run --project src/CopilotBlazorTemplate.Web` against `http://localhost:5177` (keep the app running for the whole pre-flight).
2. For every screenshot entry and every `demo.steps[].path` in the config — **especially pages you added or changed this task** — use the Playwright MCP server to:
   - Navigate to the page (logging in first if `auth` is set; use the credentials from the config).
   - Wait long enough for the Blazor InteractiveServer SignalR connection to settle (~2s; match the entry's `waitMs` if set).
   - Take a snapshot / accessibility tree and confirm: no error banner, no empty `<main>`, expected headings/components present, expected data rows visible, interactive elements clickable.
   - For pages with actions in `demo.steps` (forms, buttons, nav), exercise the action and confirm the resulting state.
3. If any page fails: fix it first, then restart pre-flight from step 1. **Never** run `scripts/demo.sh` against a known-broken page just to "see how it looks" — re-recording is cheap, but a broken demo wastes review time.
4. Stop the app before running `scripts/demo.sh` (the script will start its own).

## REQUIRED — post-capture screenshot inspection

After `scripts/demo.sh` finishes, **read each generated `docs/screenshots/*.png` with the Read tool** (it surfaces images visually) before declaring the demo done. Look for: blank/white regions where content should be, error toasts, login redirects on pages that should be authenticated, off-viewport content, console-error overlays. If a screenshot is wrong, fix the page (or the config — `waitMs`, `auth`, `path`) and re-run.

The video itself isn't directly inspectable from the agent, so the screenshots are your proxy: if every screenshot of every changed page looks right, the video almost always does too.

## Adding or changing pages
Edit `docs/screenshots.config.json`. Example: to add a `/transfers` screenshot when building a banking feature:

```json
{
  "screenshots": [
    { "name": "transfers", "path": "/transfers", "auth": "admin", "waitMs": 2000 }
  ]
}
```

Schema:
- `baseUrl` — defaults to `http://localhost:5177`; can be overridden via `APP_URL` env var.
- `viewport` — `{ width, height }`. Defaults to 1280×720.
- `credentials` — named credential pairs, referenced by `auth` fields.
- `screenshots[]` — list of pages. Fields: `name` (filename stem, required), `path` (URL path, required), `auth` (credentials key, optional), `waitMs` (extra wait after `networkidle`, optional — useful for InteractiveServer pages that need ~2s for the SignalR connection).
- `demo.steps[]` — storyboard for the video. Each step accepts `banner` (overlay text), `path` (navigate), `action` (`"login"` / `"logout"`), `auth`, `waitMs`.

The script logs in once per auth context, so order screenshots by `auth` to minimize redundant logins (unauthenticated first, then per-user).

## Configuration overrides
| Env var | Default | Purpose |
|---|---|---|
| `PORT` | `5177` | Port the app starts on. |
| `APP_URL` | `http://localhost:$PORT` | What `capture.js` connects to. |
| `SCREENSHOTS_CONFIG` | `docs/screenshots.config.json` | Alternate config file path. |
| `PW_PREFIX` | `/tmp/pw-runner` | Where Playwright is installed when not in project node_modules. |
| `APP_LOG` | `/tmp/demo-app.log` | App stdout/stderr destination. |

## Why config-driven
Previously this script hardcoded the template's pages (landing, login, dashboard, admin). When an agent built a different app on top (banking, todo, etc.) it had to either rewrite the script or improvise — both burned wall time. Now: add a line to the config, re-run `scripts/demo.sh`, done.

## Manual fallback (rarely needed)
Use only if `scripts/demo.sh` fails for an environment-specific reason. The script encodes the same flow.

```bash
# 1. Install Playwright + Chromium
npm install playwright --prefix /tmp/pw-runner
"/tmp/pw-runner/node_modules/.bin/playwright" install chromium

# 2. Reset DB and start app
rm -f src/CopilotBlazorTemplate.Web/Data/app.db
ASPNETCORE_URLS="http://localhost:5177" ASPNETCORE_ENVIRONMENT="Development" \
  nohup dotnet run --project src/CopilotBlazorTemplate.Web --no-launch-profile \
  > /tmp/demo-app.log 2>&1 &
APP_PID=$!
for i in $(seq 1 45); do curl -sf http://localhost:5177 >/dev/null 2>&1 && break; sleep 2; done

# 3. Run capture
node .github/skills/screenshots-demo/capture.js

# 4. Stop app
kill $APP_PID
```

## Troubleshooting
- `App not running at http://localhost:5177`: `scripts/demo.sh` should have started it — check `/tmp/demo-app.log`.
- `App process died`: usually a build error or a DB migration failure. Run `dotnet build` to surface it.
- Empty/short video: ensure the `demo.steps` list isn't empty and the closing card path is reachable.
- Login doesn't redirect to expected URL: the script now waits on `networkidle` instead of a hardcoded `/dashboard` pattern, so apps with different post-login routes still work.
- Need to capture against an already-running app: skip `scripts/demo.sh` and run `node .github/skills/screenshots-demo/capture.js` directly (set `APP_URL` if not on :5177).
