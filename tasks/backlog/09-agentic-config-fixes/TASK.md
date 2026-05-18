# Agentic config fixes

## Goal
Fix the broken plumbing in the repo's agentic surface: move the format-on-edit hook into a real Claude Code schema and scope it to the edited file; relocate/reframe `.github/agents/*.agent.md` so a real consumer loads them; add YAML frontmatter to the existing skills; fix the `efcore.instructions.md` glob; ship a real, pinned `.mcp.json`; clean up `copilot-setup-steps.yml` (drop fictional packages, add caching, remove `|| true`); and deduplicate `scripts/record-demo.mjs` against `.github/skills/screenshots-demo/capture.js`.

## Scope
- Move the hook from `.github/hooks/format-on-edit.json` into `.claude/settings.json` using the documented schema (`hooks.PostToolUse[].matcher`, `hooks[].type: command`).
- Rewrite `.github/hooks/scripts/format-csharp.sh` to format only the changed file, write errors to a log instead of swallowing them.
- Decide and rename `.github/agents/dev.agent.md` and `.github/agents/test.agent.md`. Recommended: move to `.claude/agents/dev.md` and `test.md` with proper YAML frontmatter (`name`, `description`, `tools`, `model`). Leave a tombstone note in the old location's `.github/agents/README.md` pointing to the new location.
- Add YAML frontmatter to `.github/skills/playwright-e2e/SKILL.md` and `.github/skills/screenshots-demo/SKILL.md`. Symlink (or duplicate) into `.claude/skills/` so Claude Code discovers them.
- Reframe `playwright-e2e` SKILL.md from documentation to procedure (or delete it and rely on `.github/instructions/playwright-tests.instructions.md`). Rewrite both `playwright-tests.instructions.md` and the skill to match the real fixture (singleton, `NewAdminContextAsync` / `NewUserContextAsync`, semantic locators, storage-state caching, 2 s default timeouts). Drop references to `WebApplicationFactory` and CSS selectors that the fixture does not use.
- Fix the `efcore.instructions.md` `applyTo:` glob from comma-separated `**/Data/**,**/Entities/**` to brace form `**/{Data,Entities,Migrations}/**`.
- Create `.mcp.json` at the repo root pinning `filesystem` (scoped to `${workspaceFolder}`), `sqlite` (scoped to `src/CopilotBlazorTemplate.Web/app.db`), and `playwright`.
- Clean up `.github/workflows/copilot-setup-steps.yml`: drop `@playwright/cli`, drop `playwright-cli install --skills`, drop `@blazorblueprint/mcp`; add `cache: true` to `setup-dotnet`, `cache: 'npm'` to `setup-node`; remove `|| true` from bootstrap steps; install Playwright browsers via the official `pwsh playwright.ps1 install --with-deps chromium` line.
- Delete `scripts/record-demo.mjs` and replace it with a thin shell that invokes the skill's `capture.js` (or vice versa). Update the screenshot SKILL.md frontmatter to reflect both outputs (screenshots + video).
- Fix `screenshots-demo/SKILL.md` step that hardcodes `/home/runner/work/...` — replace with `cd "$(git rev-parse --show-toplevel)"`.

Out of scope:
- The new `task-orchestration` skill (already owned by the separate brief that created `.github/skills/task-orchestration/`; this task's Edit zone explicitly excludes that folder).
- README updates for the agentic story (owned by task 10).
- New AGENTS.md sections (owned by task 10).
- Pre-commit / lefthook integration (defer).

## Edit zone
- `.claude/settings.json` (new)
- `.claude/agents/dev.md` (new — promoted from `.github/agents/`)
- `.claude/agents/test.md` (new)
- `.claude/skills/` (new — symlinks or copies of `.github/skills/playwright-e2e/` and `.github/skills/screenshots-demo/`)
- `.github/agents/README.md` (new — tombstone note)
- `.github/agents/dev.agent.md` (delete, or leave as a redirect — prefer delete since the new location is canonical)
- `.github/agents/test.agent.md` (delete)
- `.github/hooks/format-on-edit.json` (delete — superseded by `.claude/settings.json`)
- `.github/hooks/scripts/format-csharp.sh`
- `.github/skills/playwright-e2e/SKILL.md`
- `.github/skills/screenshots-demo/SKILL.md`
- `.github/skills/screenshots-demo/capture.js` (only if dedup with `scripts/record-demo.mjs` requires it)
- `.github/skills/**` — EVERYTHING under `.github/skills/` EXCEPT `.github/skills/task-orchestration/`, which is owned by the separate skill-creation brief and MUST NOT be modified by this task.
- `.github/instructions/efcore.instructions.md`
- `.github/instructions/playwright-tests.instructions.md`
- `.github/workflows/copilot-setup-steps.yml`
- `.mcp.json` (new)
- `scripts/record-demo.mjs` (delete or replace with a thin wrapper)

## Independence guarantee
- `.claude/settings.json` is new; the existing `.github/hooks/format-on-edit.json` is removed but the rest of `.github/hooks/` (the shell script) is reused — Claude Code will pick up the new schema at next launch.
- `.github/skills/task-orchestration/` is excluded from this Edit zone by an explicit carve-out. Do not touch it.
- If task 02 (lockfiles, CPM) has not yet shipped, the changes to `copilot-setup-steps.yml` still work — they do not depend on lockfile mode.
- If task 08 has shipped new `ci.yml` action SHA pins, this task touches a separate workflow (`copilot-setup-steps.yml`) only. No collision.
- The `.mcp.json` MCP server pins: pick versions older than 7 days (the repo's cool-down rule for npm; mirror for `uvx`). If the latest is too new, pin to the previous stable.
- `efcore.instructions.md` glob change is backward-compatible with current Copilot (both forms are accepted; brace form is universal).
- The deletion of `record-demo.mjs` (or its conversion to a wrapper) is harmless to CI as long as no workflow invokes it directly — verify by grep before deletion. If a workflow does invoke it, update that call site in this task.
- The Playwright skill / instruction rewrite must match the fixture *as it currently is* (singleton + storage state). If task 07 changes the fixture (e.g. xUnit v3 migration adds a `TestContext.Current` reference), this task lands first with the current shape; a follow-up sweeps the docs once task 07 is in.

This task may sit in `backlog/` for weeks. By the time it is picked up the agent-config surface may have drifted significantly. Handle the three drift modes explicitly:

- **File already changed by another task.** Before creating `.claude/settings.json` or editing `copilot-setup-steps.yml`, read whatever is at those paths today. The hook may already have been migrated; `.claude/agents/` may already exist; `.mcp.json` may already be present. Additively merge any missing intent; do not overwrite working config.
- **File moved/renamed.** Skills/agents may have been renamed (`.skill.md` → `SKILL.md` or vice versa); the format hook script may live at a different path. Locate via `git ls-files .github/` / `git ls-files .claude/`. If `copilot-setup-steps.yml` has been split or renamed, work against the current file.
- **Prerequisite work already done.** Quick checks: does `.claude/settings.json` already declare the format hook? Are skill frontmatters already present? Has `record-demo.mjs` already been deleted or wrapped? Skip whatever is done and note in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if the format hook is wired up and only formats the changed file, the goal is met; move on.
- If intent conflicts (e.g. someone enabled the hook for all extensions including `.razor`), surface in the PR description; don't silently restrict it back to `.cs`.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read `.github/hooks/format-on-edit.json`, `.github/hooks/scripts/format-csharp.sh`, `.github/agents/*.agent.md`, `.github/skills/*/SKILL.md`, `.github/instructions/*.instructions.md`, `.github/workflows/copilot-setup-steps.yml`, `scripts/record-demo.mjs`, and any existing `.claude/` or `.mcp.json`. The snippets below describe the *intent* of each change; apply that intent to whatever the files look like today. If a file has been moved or renamed, locate the new home via `git ls-files`. Skip whatever is already done and note in the PR description.
2. **Hook migration:**
   - If `.claude/settings.json` is absent, create it with a `PostToolUse` hook entry. If it exists with other hooks, additively add the format-on-edit entry:
     ```json
     {
       "hooks": {
         "PostToolUse": [
           {
             "matcher": "Edit|Write|MultiEdit",
             "hooks": [
               {
                 "type": "command",
                 "command": ".github/hooks/scripts/format-csharp.sh \"$CLAUDE_FILE_PATH\""
               }
             ]
           }
         ]
       }
     }
     ```
   - Delete `.github/hooks/format-on-edit.json` (`git rm`) once the new schema is in place.
3. **Rewrite `format-csharp.sh`** so it formats only the file passed as `$1` (do not regenerate from scratch if it already does this):
   ```bash
   #!/usr/bin/env bash
   set -euo pipefail
   file="${1:-}"
   [ -z "$file" ] && exit 0
   [ "${file##*.}" = "cs" ] || exit 0
   log="${TMPDIR:-/tmp}/format-csharp.log"
   dotnet format whitespace --include "$file" --verbosity quiet 2>"$log" || true
   dotnet format style      --include "$file" --verbosity quiet 2>>"$log" || true
   ```
   Keep the file executable (`chmod +x` if needed).
4. **Promote agents:**
   - Write `.claude/agents/dev.md`:
     ```yaml
     ---
     name: dev
     description: Implements features and fixes in the .NET 10 Blazor template. Always builds and tests before finishing. Use for app-code edits in src/.
     tools: Read, Edit, Write, Bash, Grep, Glob
     model: inherit
     ---
     ```
     Body: the prose currently in `.github/agents/dev.agent.md`.
   - Same for `.claude/agents/test.md` (description: "Writes and runs tests… Use for tests/ edits.").
   - Write `.github/agents/README.md` noting the move.
   - `git rm .github/agents/dev.agent.md .github/agents/test.agent.md`.
5. **SKILL.md frontmatter:**
   - `.github/skills/screenshots-demo/SKILL.md`:
     ```yaml
     ---
     name: screenshots-demo
     description: Captures landing/login/dashboard/admin screenshots and records a ~30-45s WebM demo of the Copilot Blazor Template using Playwright + Chromium. Use when the user asks for fresh README screenshots or a demo video.
     allowed-tools: Bash, Read, Write
     license: MIT
     ---
     ```
   - Fix the `cd /home/runner/work/...` line to `cd "$(git rev-parse --show-toplevel)"`.
   - `.github/skills/playwright-e2e/SKILL.md`: reframe into a procedure ("Add a new E2E test for an authenticated page"). Steps: (1) duplicate `AuthTests.cs`; (2) use `NewAdminContextAsync` / `NewUserContextAsync` / `NewAnonymousContextAsync` from the singleton fixture; (3) prefer `GetByRole` / `GetByLabel`; (4) honour the 2s default timeout; (5) run `dotnet test --filter`. Add frontmatter:
     ```yaml
     ---
     name: playwright-e2e
     description: Add a new Playwright E2E test for the Copilot Blazor Template. Uses the singleton PlaywrightFixture + cached storage-state contexts. Invoke when the user wants to cover a new page or user flow at the browser level.
     allowed-tools: Read, Edit, Write, Bash
     license: MIT
     ---
     ```
6. **`.claude/skills/` discovery:** symlink (`ln -s ../../.github/skills/playwright-e2e .claude/skills/playwright-e2e`) so Claude Code finds them under its expected path. If symlinks are problematic on Windows forks, copy instead and document the duplication. Skip if already in place.
7. **`efcore.instructions.md`:** wherever the `applyTo:` frontmatter uses a comma-separated glob, replace with brace form (e.g. `applyTo: "**/{Data,Entities,Migrations}/**"`). If the glob is already in brace form, leave it.
8. **`playwright-tests.instructions.md`:** rewrite the file body to match the real fixture *as it stands today*. Read `PlaywrightFixture.cs` first to confirm the helper names and the storage-state caching strategy. Drop any `WebApplicationFactory<Program>` reference and any CSS-selector examples that the fixture does not use; prefer `Page.GetByLabel("Email").FillAsync(...)` / `Page.GetByRole(...)` patterns. Keep the file <80 lines.
9. **`.mcp.json`** at repo root (skip if already present and listing the same servers):
   ```jsonc
   {
     "$schema": "https://modelcontextprotocol.io/schemas/mcp.json",
     "mcpServers": {
       "filesystem": {
         "command": "npx",
         "args": ["-y", "@modelcontextprotocol/server-filesystem@<pinned-version>", "${workspaceFolder}"]
       },
       "sqlite": {
         "command": "uvx",
         "args": ["mcp-server-sqlite@<pinned-version>",
                  "--db-path", "${workspaceFolder}/src/CopilotBlazorTemplate.Web/app.db"]
       },
       "playwright": {
         "command": "npx",
         "args": ["-y", "@playwright/mcp@<pinned-version>"]
       }
     }
   }
   ```
   Replace `<pinned-version>` with concrete versions that are at least 7 days old per the repo's cool-down rule.
10. **`copilot-setup-steps.yml`:** wherever the workflow references `@playwright/cli`, `playwright-cli install --skills`, or `@blazorblueprint/mcp`, remove those steps. Ensure `cache: true` is set on `setup-dotnet` and `cache: 'npm'` on `setup-node`. Remove `|| true` from `dotnet restore` / `dotnet build` so failures surface. Install Playwright browsers via:
   ```yaml
   - name: Install Playwright browsers
     run: pwsh tests/CopilotBlazorTemplate.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
   ```
11. **Dedup `record-demo.mjs`:** if it still exists, verify no workflow invokes it (`grep -r 'record-demo' .github/`). If safe, `git rm scripts/record-demo.mjs` and add a `scripts/record-demo.sh` one-liner:
    ```bash
    #!/usr/bin/env bash
    exec node .github/skills/screenshots-demo/capture.js "$@"
    ```
    (or simply delete `scripts/record-demo.mjs` and update any README pointers — owned by task 10).
12. Verify `dotnet build` and `dotnet test` still pass (no source-code changes here, so this is a sanity check). Invoke a Claude Code session and confirm the format hook fires on a `.cs` edit.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] Claude Code's format-on-edit hook fires on a `.cs` edit during a real session and formats only that file (no full-solution sweep). The legacy `.github/hooks/format-on-edit.json` is gone.
- [ ] The format script writes errors to a log instead of silently swallowing them.
- [ ] Claude Code discovers `dev` and `test` subagents (under `.claude/agents/` or wherever Claude Code looks today) with valid YAML frontmatter. Old `.agent.md` files are gone; a tombstone/redirect exists in the old location.
- [ ] Every SKILL.md under `.github/skills/` (except `.github/skills/task-orchestration/`) has YAML frontmatter with `name:` and `description:`.
- [ ] Claude Code discovers the skills via `.claude/skills/` (symlink or copy).
- [ ] The Playwright skill and instruction docs match the real fixture today — no `WebApplicationFactory<Program>` reference, no CSS selectors the fixture doesn't use.
- [ ] The EF Core instructions glob uses brace form (so all four current globbing engines accept it).
- [ ] An `.mcp.json` exists at the repo root pinning each MCP server to a concrete version (no `@latest`).
- [ ] `copilot-setup-steps.yml` no longer references `@playwright/cli`, `--skills`, or `@blazorblueprint/mcp`; `setup-dotnet` and `setup-node` use built-in caching; no `|| true` masks failure.
- [ ] `scripts/record-demo.mjs` is either gone or a thin wrapper around the skill's `capture.js` (no 95%-duplicated logic).
- [ ] `.github/skills/task-orchestration/` is unchanged.
- [ ] `dotnet build` and `dotnet test` still pass.

## References
- Audit cross-cutting theme CC-7 (Skill/instruction docs drift): `../../../docs/audits/2026-05-18/REPORT.md`.
- Agentic Development findings "`.github/hooks/format-on-edit.json` is not a real Claude Code hook", "`format-csharp.sh` runs `dotnet format` over the entire solution", "`.github/agents/*.agent.md` use an undocumented schema", "SKILL.md files are missing the Agent Skills frontmatter", "`playwright-e2e` skill is documentation, not a skill", "`.mcp.json` is mentioned in the audit scope but is absent", "`copilot-setup-steps.yml` installs fictional/unstable bits", "`scripts/record-demo.mjs` and `.github/skills/screenshots-demo/capture.js` are 95% duplicates", "`efcore.instructions.md` glob may misbehave", "Skill `capture.js` hard-codes `/home/runner/work/...`": `../../../docs/audits/2026-05-18/05-agentic-development.md`.
- Anthropic Agent Skills — <https://docs.anthropic.com/en/docs/agents-and-tools/agent-skills>
- Claude Code subagents — <https://docs.claude.com/en/docs/claude-code/sub-agents>
- Claude Code hooks — <https://docs.claude.com/en/docs/claude-code/hooks>
- Model Context Protocol — <https://modelcontextprotocol.io/>
