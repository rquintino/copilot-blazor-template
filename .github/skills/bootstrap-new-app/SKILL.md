---
name: bootstrap-new-app
description: MUST be invoked as the very first action whenever the working tree still contains the string `CopilotBlazorTemplate` AND the user's request describes building any new app, feature, or domain (e.g. "implement requirements for...", "build a... app", "create a... system"). Takes precedence over `task-orchestration` in this state — running `task-orchestration` first bakes template names into entities, migrations, namespaces, and tests, which then have to be unwound. The skill is short on purpose: it only fixes the three required bookend phases (rename, screenshots/demo, README); everything between them is the agent's plan to design.
---

# Bootstrap a new app from this template

**Precondition check (run this first):** if `grep -rIlq CopilotBlazorTemplate . --exclude-dir={.git,bin,obj,node_modules}` finds matches, this skill applies and must be honored **before** invoking `task-orchestration` or writing any code. If it finds nothing, the app is already bootstrapped — fall through to `task-orchestration` as normal.

When the agent plans a new-app bootstrap, the plan **must** include these phases. Everything between them is the agent's call. Track the plan in the agent's **internal todo tool** — do not create a persisted `tasks/` folder for the bootstrap (per `task-orchestration`: the main agent does not author persisted tasks).

## First phase — Rename & verify

1. Run `./scripts/init-app.sh <NewName>`.
2. Gate before continuing:
   - No paths or contents containing `CopilotBlazorTemplate` remain (excluding `.git/` and this script).
   - `dotnet build` and `dotnet test` green.
3. Commit as a single demarcation: `chore: initialize app as <NewName>`.

## Last-but-one phase — Screenshots & demo refresh

1. Update `docs/screenshots.config.json` for the new app's pages and headline flow.
2. Run the `screenshots-demo` skill end-to-end (honor its pre-flight and post-capture inspection requirements).
3. Commit the regenerated `docs/screenshots/` and `docs/demo/` outputs.

## Last phase — README rewrite

Replace `README.md` so it describes the new app, not the template: what it is, embedded screenshots, link to the demo, getting-started, project layout, tests.
