---
name: task-orchestration
description: Move existing tasks through the `tasks/backlog/ → current/ → done/` lifecycle. Use when a task folder already exists, when the user asks to dispatch backlog tasks, work the next task, or check task status. The main agent **does not create persisted tasks** — those are authored upstream (user, audits, dispatch from another agent, the bootstrap-new-app hand-off). For sub-step decomposition inside a single run, use the agent's built-in todo tool, not new TASK.md files. This skill defines: lifecycle moves the main agent IS allowed to perform, the TASK.md contract that upstream authors should follow, independence rules, and shared-file ownership principles.
---

# Task orchestration

A lightweight, repo-local protocol for moving discrete units of work through three folders so multiple humans and agents can pick up, run, and finish tasks without colliding.

## Why this exists

Engineering work generated from audits, design reviews, or planning sessions tends to arrive as a bundle. Without a shared protocol:

- Two agents grab the same task and produce conflicting commits.
- A task assumes a prerequisite task has shipped; if it has not, the second task breaks.
- Shared files (`Directory.Build.props`, `Program.cs`, `AGENTS.md`, CI workflows) become merge-conflict hotspots because every task touches them.
- "Done" is ambiguous — there is no checklist a reviewer or follow-up agent can verify against.

This skill defines a small contract that fixes all four problems with zero tooling. The state lives in folders; the rules live in TASK.md files.

## Lifecycle

```
tasks/
├── backlog/       # queued, not yet picked up
│   └── NN-<slug>/
│       └── TASK.md
├── current/       # in progress — exactly one owner at a time
│   └── NN-<slug>/
│       └── TASK.md
└── done/          # completed; created when the first task finishes
    └── NN-<slug>/
        └── TASK.md
```

Rules:

1. **A task is a folder, not a file.** The folder name is `NN-<kebab-slug>` (e.g. `04-blazor-component-hygiene`). The folder may contain a `TASK.md`, scratch notes, scripts, diffs — anything the worker generates.
2. **Backlog is the queue.** Anything in `tasks/backlog/` is ready to be picked. Order is by `NN-` prefix when the user has no preference, but any task can be picked out of order — independence is guaranteed by the TASK.md contract (see below).
3. **Current is the work-in-progress lane.** Move the entire folder (`git mv tasks/backlog/NN-foo tasks/current/NN-foo`) to claim it. Presence in `current/` means "someone is on it" and is the lock.
4. **Done is the archive.** When acceptance criteria pass, move the folder to `tasks/done/`. **Do not pre-create `tasks/done/`** — it appears the first time a task is completed. This keeps the tree clean for forks that have not started any work yet.
5. **One owner per task folder.** If two workers want the same task, the second must pick a different one. If they truly need to collaborate, they coordinate inside the existing `current/NN-<slug>/` folder rather than creating duplicates.

## TASK.md contract

Every TASK.md file MUST contain these sections, in this order:

```markdown
# <human-readable task title>

## Goal
1-2 sentences. What does shipping this task accomplish?

## Scope
- bullet list of what is in-scope
- explicit "out of scope" sub-list

## Edit zone
- absolute or repo-relative paths this task may create/modify
- other tasks SHOULD NOT touch these files; if they must, they append only

## Independence guarantee
A short paragraph or bullet list explaining how this task runs cleanly
regardless of whether other tasks have shipped. Use the patterns below.

## Steps
1. Numbered, concrete steps with file paths and code snippets.
2. Steps should be small enough that a fresh worker can resume mid-task.

## Acceptance criteria
- [ ] Bullet checklist a reviewer (or subagent) can verify.
- [ ] Include "build green", "tests green", "format clean" as appropriate.
- [ ] `README.md` and `AGENTS.md` reviewed and updated (or noted `no changes required`).

## References
- Links to source material (audit findings, design docs, ADRs, issues).
```

Optional sections (use when relevant): `## Risks`, `## Open questions`, `## Notes`, `## Follow-ups`.

## Independence rules

Tasks MUST be runnable in any order. Encode missing prerequisites instead of declaring hard dependencies:

- **`if exists do A else do B`** — when a task wants to add to a file that may not yet exist:
  > If `Directory.Packages.props` exists (because task 02 shipped first), append the new `<PackageVersion>` entries to it. Otherwise, add the `<PackageReference Version="…">` directly in the csproj and leave a TODO referencing this task so a follow-up sweep can centralise it once task 02 lands.
- **`[Skip(reason)]` on tests for not-yet-shipped behaviour** — when a test asserts a feature another task introduces:
  ```csharp
  [Fact(Skip = "Re-enable once task 03 ships account lockout in Program.cs")]
  public async Task Login_Fails_5_Times_Locks_Account() { … }
  ```
- **Additive-only edits on shared files** — when a task must touch a file another task owns, only append (new sections, new lines at the bottom). Never rewrite or restructure a file another task owns.
- **Feature flags / config gates** — when shipping risky behaviour, gate it behind an env var or config flag so other tasks can opt in/out independently.
- **No "blocks" or "depends on" fields** — if a task genuinely cannot run without another, merge them. The protocol has no dependency graph by design.

## Shared-file ownership principle

Some files are touched by many tasks (CI workflows, root configs, `AGENTS.md`, README, `Program.cs`). Apply this principle:

- **Each task declares its Edit zone in the TASK.md.** The Edit zone is the contract: other tasks defer to it.
- **One task owns rewrites of a given file; all other tasks do additive appends.** The owner is whichever task most directly changes the file's structure. If two tasks want to rewrite the same file, split the work or merge the tasks — never both rewrite.
- **When in doubt, append.** A new section at the bottom of a markdown file or a new `<PropertyGroup>` at the bottom of an MSBuild file rarely conflicts with a rewrite happening above it.
- **CI workflows and other YAML files are especially fragile.** Prefer adding new jobs/steps over modifying existing ones; if you must modify an existing step, the owning task gets that change and other tasks open a follow-up.

This principle is intentionally generic. Specific ownership tables belong inside the individual TASK.md files (in their `Edit zone` section), not in this skill, because ownership shifts every cycle.

## Task authorship — who creates persisted tasks

**The main agent does not create persisted tasks.** Folders under `tasks/` are upstream artifacts; the main agent consumes them, moves them, and updates their progress, nothing more.

**Who creates persisted tasks (allowed authors):**
- The **user** (directly, or by running an audit/planning session).
- The **`bootstrap-new-app`** flow, when it hands off a single follow-up task at the end of bootstrap.
- An **explicit dispatch from another agent** acting in an orchestrator role.

**What the main agent IS allowed to do:**
- `git mv` a folder `backlog/ → current/` to claim work.
- `git mv` a folder `current/ → done/` to close work.
- Edit the active TASK.md inside `current/` to update progress, tick acceptance-criteria boxes, append notes.
- Use the **built-in todo tool** to decompose the current run into sub-steps. These todos are ephemeral — they live in the agent's session, not on disk.

**Un-bootstrapped template clone:** before doing anything, run `grep -rIlq CopilotBlazorTemplate . --exclude-dir={.git,bin,obj,node_modules}`. If it finds matches and the user's request describes building a new app/feature/domain, defer to `.github/skills/bootstrap-new-app/SKILL.md` first — its rename phase must run before any task-tracked work, or template names get baked into the new app.

**No existing task and the user asks for something non-trivial?** Do not silently scaffold one. Work directly against the user's request, using the internal todo tool to track sub-steps. If the user explicitly asks for a persisted TASK.md, follow the contract in *Authoring a new TASK.md* below — but only on request, not by default.

**Why this rule:** the workshop scenario (clone → ask Copilot to build an app) does not benefit from a dozen markdown TASK.md files for sub-steps the agent invents. Persisted tasks are for work the user or an orchestrator wants tracked across sessions; intra-session decomposition belongs in the agent's own todo list.

## Dispatch workflow

When the user says "work the next task", "dispatch the backlog", or similar:

1. **List the queue.** `ls tasks/backlog/` and report what is available with a one-line summary per task (read the `Goal` from each TASK.md).
2. **Pick the next task.** Default to the lowest `NN-` prefix unless the user picks one. If the user asks to run several in parallel, pick non-overlapping Edit zones first.
3. **Claim it.** `git mv tasks/backlog/NN-<slug> tasks/current/NN-<slug>`. Commit the move on its own (small, reversible).
4. **Read the TASK.md end-to-end before starting.** Pay attention to Edit zone, Independence guarantee, and Acceptance criteria.
5. **Launch a worker.** For an autonomous run, hand the task off to a subagent with the full TASK.md and instructions to honour the Edit zone. For an interactive run, work through Steps yourself.
6. **Verify acceptance criteria.** Run the listed commands; check each box only after observing green output.
7. **Finish.** `git mv tasks/current/NN-<slug> tasks/done/NN-<slug>` (creating `tasks/done/` if this is the first completion). Commit. If there are leftover scratch notes or partial work, leave them in the task folder for posterity — done means done, not deleted.

### Parallel dispatch

If running multiple tasks at once:

- Verify the Edit zones are disjoint (do a textual diff of the `## Edit zone` bullet lists).
- Launch each in its own working tree or branch.
- When merging, the additive-only rule on shared files prevents conflicts in 95% of cases. The remaining 5% (genuine overlaps) get resolved by the orchestrator in a single follow-up commit.

## Authoring a new TASK.md

**Upstream/dispatch use only.** The main agent does not author TASK.md files during normal work (see *Task authorship* above). Use this template when the user explicitly asks for a persisted task, when populating a backlog from an audit, or when an orchestrator hands off work to a sub-agent:

```markdown
# <title>

## Goal
<1-2 sentences.>

## Scope
- <in-scope bullet>
- <in-scope bullet>

Out of scope:
- <explicitly out-of-scope bullet>

## Edit zone
- `<path/to/file>`
- `<path/to/dir/>` (everything under this directory)

## Independence guarantee
This task runs cleanly with or without other backlog tasks. Specifically:
- If `<file other task may create>` exists, <do X>; otherwise <do Y>.
- Tests asserting <not-yet-shipped behaviour> are marked `[Skip("re-enable after task NN")]`.
- Edits to shared files (`<file>`) are additive only.

## Steps
1. <first concrete step>
2. <…>

## Acceptance criteria
- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` succeeds.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] If UI pages were added or changed: `docs/screenshots.config.json` updated and `bash scripts/demo.sh` regenerated artifacts in `docs/screenshots/` and `docs/demo/`.
- [ ] `README.md` and `AGENTS.md` reviewed and updated to reflect this task's changes (or noted `no changes required`).
- [ ] <task-specific check>

## References
- <link to design doc, audit finding, issue>
```

Keep TASK.md files short (~50-200 lines). If a task is growing past that, split it.

## Finalization protocol

These rules apply at the **end** of every task, just before moving the folder to `tasks/done/` and opening the PR. They exist because the GitHub Copilot coding-agent environment has constraints that differ from local development; respecting them removes a chunk of wall-time from every run.

1. **Refresh screenshots when UI changed.** If the task added or changed any `.razor` page (or anything that visibly alters an existing page), update `docs/screenshots.config.json` with the new pages and run `bash scripts/demo.sh`. Commit the regenerated `docs/screenshots/*.png` and `docs/demo/*.webm` in the same task commit. The config schema and one-shot are documented in `.github/skills/screenshots-demo/SKILL.md`.
2. **Commit, do not push.** In the Copilot coding-agent environment, `git push` is blocked at the credential layer — the recent banking-app run wasted turns on `Push changes` → `Try alternative push` → "failing due to credentials". Commits are fine; they accumulate locally. The PR-creation step at the end of the task (`gh pr create`) handles publishing the branch and the commits in a single operation.
3. **Do not invoke Copilot code review or CodeQL mid-task.** Both run automatically as PR checks once `gh pr create` succeeds. Running them locally before the PR exists tends to hang on missing-origin-branch credentials (same reason `git push` fails), and even on success it duplicates work the PR will redo. Skip them and let the PR pipeline do its job.
4. **Run the validator skill before the move to `done/`.** Invoke `.github/skills/validator/SKILL.md` and confirm every applicable check passes. If a check fails, the task is not done — fix and re-validate. The validator is read-only; it reports, it does not patch.
5. **Review `README.md` and `AGENTS.md` — critical, non-negotiable.** A task is not done until both files have been read end-to-end and updated to reflect the changes this task introduced. Pages added, entities introduced, commands changed, environment variables added, seed credentials changed, project layout shifted — any of these invalidate the existing docs. If the task changes nothing the docs describe, state that explicitly in the task notes (`README/AGENTS reviewed: no changes required`) — silence is not a pass. This gate exists because stale README/AGENTS docs poison every future agent session that starts from them.
6. **Open the PR last.** `gh pr create` is the publishing step. It is the *only* operation in the task lifecycle that contacts origin. If `gh pr create` fails, do not retry with `git push` — read the gh error, fix the cause, retry `gh pr create`.

## Anti-patterns

- **"depends-on: 03"** — replace with an `if exists/else` in the Independence guarantee.
- **"this task touches everything"** — split into smaller tasks, each with a tight Edit zone.
- **Editing files outside the declared Edit zone** — open a follow-up task or coordinate with the owning task instead.
- **Pre-creating `tasks/done/` with a `.gitkeep`** — done is for completed work, not scaffolding.
- **Long-running tasks in `current/`** — if a task sits in `current/` for more than a sprint, demote it back to `backlog/` and document why.
- **Editing a TASK.md after the task is moved to `done/`** — done is immutable history; create a follow-up task instead.
- **`git push` during the task.** Blocked in the Copilot coding-agent environment; only `gh pr create` publishes. See Finalization protocol.
- **Running Copilot code review / CodeQL before the PR exists.** Redundant — they run as PR checks. See Finalization protocol.
- **Moving a task to `done/` without reviewing `README.md` and `AGENTS.md`.** Critical — stale docs poison every future agent session. If the task changes nothing those docs describe, state `README/AGENTS reviewed: no changes required` in the task notes; never skip the review silently.
- **Scaffolding tasks in an un-bootstrapped template clone.** If `grep -r CopilotBlazorTemplate .` finds matches and the user is asking for a new app, defer to `.github/skills/bootstrap-new-app/SKILL.md` before any task creation — its rename phase must run first.
- **Main agent authoring new persisted tasks for its own sub-steps.** Sub-decomposition stays in the agent's internal todo tool. Persisted `tasks/` folders are upstream artifacts — created by the user, by an audit/planning session, or by the bootstrap-new-app hand-off, never by the main agent in the middle of a run. A workshop attendee should see one task at most, not five auto-generated ones.

## References

- AGENTS.md spec — <https://agents.md/>
- Anthropic Agent Skills spec (`SKILL.md` frontmatter shape) — <https://docs.anthropic.com/en/docs/agents-and-tools/agent-skills>
- Claude Code subagents — <https://docs.claude.com/en/docs/claude-code/sub-agents>
- Conventional Commits (if the repo uses them for the task commit messages) — <https://www.conventionalcommits.org/>
