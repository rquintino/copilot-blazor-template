# Validator agent + orchestration wiring

## Goal
Close two gaps the recent banking-app run exposed: (a) the orchestrator's "verify at each step with a validation agent" instruction had nothing to call, and (b) the agent burned turns trying to `git push` and to invoke Copilot code review / CodeQL mid-work, both of which fail in the Copilot coding-agent environment.

## Scope
- Add `.github/agents/validator.agent.md` with a narrow, refuse-out-of-scope brief.
- Edit `.github/skills/task-orchestration/SKILL.md` to add a finalization protocol that (1) requires screenshots refresh when UI pages change, (2) forbids `git push` mid-work, (3) forbids invoking CodeQL / Copilot code review mid-work — those run as PR checks.

Out of scope:
- Creating the orchestrator agent (separate, larger change — improvement #4).
- A feature-build TASK template (separate, improvement #5).
- Modifying the existing `dev.agent.md` / `test.agent.md`.

## Edit zone
- `.github/agents/validator.agent.md` (new)
- `.github/skills/task-orchestration/SKILL.md`
- `tasks/current/02-validator-agent-and-orchestration-wiring/TASK.md`

## Independence guarantee
- New file `validator.agent.md` doesn't conflict with anything.
- The orchestration SKILL edit adds new sections at the end; no rewrite of existing content. Other task workers who read the SKILL still see the lifecycle rules unchanged.

## Steps
1. Create `validator.agent.md` modeled on the existing dev/test agent shape (short, focused, one-page).
2. Append a `## Finalization protocol` section to `task-orchestration/SKILL.md` covering screenshots refresh + push/CodeQL rules.
3. Update the TASK.md template inside the SKILL so the Acceptance criteria template includes a "screenshots regenerated (if UI changed)" checkbox.

## Acceptance criteria
- [ ] `validator.agent.md` exists and reads like a peer of `dev.agent.md` / `test.agent.md`.
- [ ] `task-orchestration/SKILL.md` has a Finalization protocol section explicitly forbidding `git push` and CodeQL/code-review mid-work, with one-sentence reasons.
- [ ] The Acceptance criteria template in the SKILL has a screenshots checkbox.
- [ ] No other files modified in this commit.

## References
- Conversation: improvements #2 and #3 from the post-banking-app review.
- Banking-app run logs: `Push changes` / `Try alternative push` / "push is failing due to credentials" → `Check changes with Copilot code review and CodeQL` → eventually `gh pr create` succeeded. Confirms commit allowed, push blocked, PR-creation allowed; CodeQL/code-review mid-work is a redundant fallback (also runs as PR check).
