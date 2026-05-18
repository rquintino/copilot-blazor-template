# Screenshots & Demo Video Capture Skill

## Overview
Captures page screenshots (PNG) and records a full-flow demo video (WebM) for the Copilot Blazor Template app using Playwright.

## Prerequisites
- Node.js 20+ installed
- `npx playwright` available (comes with Node.js)
- .NET 10 SDK (to build and run the app)
- App builds successfully (`dotnet build`)

## Quick Execution (copy-paste ready)

### Step 1: Install Playwright + Chromium
```bash
npm install playwright --prefix /tmp/pw-runner
npx playwright install chromium
```

### Step 2: Start the App
```bash
cd /home/runner/work/copilot-blazor-template/copilot-blazor-template
rm -f src/CopilotBlazorTemplate.Web/app.db
ASPNETCORE_URLS="http://localhost:5177" ASPNETCORE_ENVIRONMENT="Development" nohup dotnet run --project src/CopilotBlazorTemplate.Web --no-launch-profile > /tmp/app.log 2>&1 &
APP_PID=$!
echo "App PID: $APP_PID"
# Wait for ready (up to 60s)
for i in $(seq 1 30); do curl -sf http://localhost:5177 > /dev/null 2>&1 && echo "APP READY" && break; sleep 2; done
```

### Step 3: Run the Capture Script
```bash
node /home/runner/work/copilot-blazor-template/copilot-blazor-template/.github/skills/screenshots-demo/capture.js
```

### Step 4: Stop the App
```bash
kill $APP_PID 2>/dev/null
```

## Output Files
| File | Description |
|------|-------------|
| `docs/screenshots/landing.png` | Landing/home page (1280×720+) |
| `docs/screenshots/login.png` | Login page with credential hints |
| `docs/screenshots/dashboard.png` | Dashboard (logged in as admin) |
| `docs/screenshots/admin.png` | Admin panel with user table |
| `docs/demo/copilot-blazor-template-demo.webm` | Full flow video (~30-45s, 1280×720) |

## Key Details
- **Port**: 5177 (avoids conflicts with common ports)
- **Launch profile**: Must use `--no-launch-profile` and set `ASPNETCORE_URLS` env var
- **nohup**: Required so the app stays alive across shell invocations
- **DB reset**: Delete `app.db` before starting to get clean seed data
- **Login credentials**: admin@template.local / Admin123!
- **Login form selectors**: `input[name="Input.Email"]`, `input[name="Input.Password"]`, `button[type="submit"]`
- **Dashboard redirect**: After login, URL matches `**/dashboard**`
- **InteractiveServer pages**: Need 2s wait after navigation for Blazor SignalR to connect
- **Chromium only**: No need for Firefox/WebKit

## Troubleshooting
- If `ERR_CONNECTION_REFUSED`: app died — restart with nohup, verify with `curl -sf http://localhost:5177`
- If login doesn't redirect: check `page.url()` — may need to navigate manually to `/dashboard`
- If video is empty: ensure `page.close()` + `context.close()` are called (triggers video save)
