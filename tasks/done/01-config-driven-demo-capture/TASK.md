# Config-driven demo capture

## Goal
Make the screenshots + demo-video flow page-agnostic so agents building features on top of this template (banking app, todo app, etc.) update a config file instead of rewriting `capture.js`. Eliminate the manual app-startup dance that ate ~10 min of wall time in the recent banking-app run.

## Scope
- Introduce `docs/screenshots.config.json` describing pages to capture and the demo storyboard.
- Rewrite `.github/skills/screenshots-demo/capture.js` to read the config.
- Add `scripts/demo.sh`: one-shot entrypoint (clean DB → start app → wait-for-ready → capture → stop).
- Update `.github/skills/screenshots-demo/SKILL.md` to point at the one-shot.
- Keep the current template's pages (landing, login, dashboard, admin) as the default config so existing output is unchanged.

### Out of scope
- Deleting `scripts/record-demo.mjs` (redundant with capture.js's demo path) — leave for a follow-up cleanup.
- Switching off MCP Playwright entirely — the script becomes the canonical path; MCP stays for ad-hoc browsing.
- Per-page custom interactions beyond `auth` and `waitMs` (good enough for the 90% case; can be extended later).

## Edit zone
- `.github/skills/screenshots-demo/capture.js`
- `.github/skills/screenshots-demo/SKILL.md`
- `docs/screenshots.config.json` (new)
- `scripts/demo.sh` (new)
- `tasks/current/01-config-driven-demo-capture/TASK.md`

## Acceptance
- `scripts/demo.sh` produces `docs/screenshots/*.png` and `docs/demo/*.webm` from a cold state in one invocation.
- Editing `docs/screenshots.config.json` to add a new page entry produces a new PNG without code changes.
- SKILL.md's quick-start is now a single command, not four steps.

## Notes from the banking-app run (why this exists)
- Agent burned ~5 turns on app-startup retries (`webapp`, `webapp2`, `webapp3`, `webapp4`) fighting DB lock + launch profile.
- Agent bypassed the existing skill and `npm install`ed playwright into the project root because the hardcoded pages didn't match the banking app.
- Both problems disappear if the skill is one command and the page list is config.
