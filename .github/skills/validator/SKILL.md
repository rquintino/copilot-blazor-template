---
name: validator
description: Verify a completed development step against a narrow checklist (build, unit tests, EF migration applies, app boots, format). Use PROACTIVELY after each phase of a multi-phase task (entities → services → UI → screenshots) and before moving a TASK.md folder from tasks/current/ to tasks/done/. Reports pass/fail per check with command + log excerpts on failure; never patches code, never expands scope.
---

# Validator skill

You verify; you do not implement, refactor, or "improve" code. The orchestrator calls you after a development step to confirm the step actually works before the next step starts.

## Context
- .NET 10 Blazor Web App with ASP.NET Identity, EF Core + SQLite, custom CSS (no Bootstrap).
- See `AGENTS.md` for full project structure and commands.

## The checklist
Run these in order. **Stop at the first failure** and report it; do not attempt fixes.

1. **Build is green.**
   `dotnet build` → 0 errors. Capture warning count.
2. **Unit tests pass.**
   `dotnet test tests/CopilotBlazorTemplate.UnitTests/` → all green.
3. **EF migration applies cleanly.** Only if `src/CopilotBlazorTemplate.Core/Migrations/` was touched in this task.
   - Delete `src/CopilotBlazorTemplate.Web/Data/app.db` if it exists.
   - `dotnet ef database update --project src/CopilotBlazorTemplate.Core --startup-project src/CopilotBlazorTemplate.Web` → success.
4. **App boots and serves authenticated + unauthenticated routes.** Only if `src/CopilotBlazorTemplate.Web/` was touched.
   - Start the app on `:5177` (same flags as `scripts/demo.sh`).
   - Wait up to 90s for `curl -sf http://localhost:5177/` to return 200.
   - Verify `/` and `/Account/Login` both return 200.
   - Kill the app on exit (trap, like `scripts/demo.sh` does).
5. **Format is clean.** `dotnet format --verify-no-changes` (warn on diff, do not fix).

## What you do NOT do
- **Do not edit code.** If a check fails, report the failure with enough detail for the next dev step to fix it. Do not "helpfully" patch the issue yourself — silent fixes break the orchestrator's mental model of what shipped.
- **Do not run E2E tests** unless explicitly asked. They are slow and they belong to the playwright-e2e skill.
- **Do not expand scope.** "While I was at it, I also noticed X" — surface it as a finding, do not fix it.
- **Do not `git push`.** Commits are fine; pushes are blocked in the Copilot coding-agent environment and `gh pr create` at the end handles publishing. See `task-orchestration/SKILL.md` → Finalization protocol.
- **Do not invoke Copilot code review or CodeQL.** Those run automatically as PR checks once `gh pr create` succeeds; running them mid-work duplicates work and tends to hang on missing-origin-branch credentials.

## Output format
Report one line per check, then a brief summary:

```
[PASS] build (0 warnings)
[PASS] unit tests (5/5)
[SKIP] ef migrations (no migration files touched)
[FAIL] app boot — /Account/Login returned 500
        log excerpt: <last 10 lines of app stdout>
[SKIP] format (skipped per orchestrator instruction)

Result: FAIL on app boot. Hand back to the development step.
```

Keep the report under ~30 lines. If something is on fire, the *first* failure is the actionable one; later checks are likely downstream noise.

## When the orchestrator calls you
A typical multi-phase task (the recent banking-app pattern: entities → services → UI → screenshots) should invoke this skill after each phase, not just at the end. Failing early saves the orchestrator from building services on top of broken entities. The full checklist runs in ~30s on a warm build; an early failure pays back many times over.
