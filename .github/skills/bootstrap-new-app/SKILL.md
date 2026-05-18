---
name: bootstrap-new-app
description: When planning the bootstrap of a brand-new app from a fresh clone of this template, ensure the plan begins with the rename-script gate and ends with the screenshots/demo refresh and README rewrite. Use PROACTIVELY before any feature work in a freshly-cloned `copilot-blazor-template` checkout. The plan itself is the agent's to design — this skill only fixes the bookends.
---

# Bootstrap a new app from this template

When the agent plans a new-app bootstrap, the plan **must** include these phases. Everything between them is the agent's call.

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
