# Docs & template scrub

## Goal
Make the repo discoverable for new contributors and clean to fork: extend `README.md` with the missing standard sections, reorganise `docs/` to include architecture + ADRs, document the `tasks/` workflow, surface root meta files in the `.slnx` solution view, define the template-scrub story (either a `dotnet new` `template.json` or a `rename-template.sh` script), and add the missing AGENTS.md sections plus cross-vendor symlinks.

## Scope
- Extend `README.md` with: **Prerequisites** (`.NET 10 SDK 10.0.100+`, `Node 22+`, Playwright deps), **Running Tests**, **Make It Yours** (rename steps), **License** (with shield), **Contributing** pointer, **Security** pointer, and a short **Using With AI Agents** section linking to `AGENTS.md`, `.github/instructions/`, `.github/skills/`.
- Reorganise `docs/`:
  - Add `docs/README.md` (index).
  - Add `docs/architecture/overview.md` (one diagram + paragraphs on render modes, auth flow, data flow).
  - Add `docs/adr/0001-record-architecture-decisions.md` (Michael Nygard template).
  - Keep existing `docs/demo/`, `docs/screenshots/`, `docs/audits/` untouched.
- Add `tasks/README.md` explaining the `backlog/ → current/ → done/` workflow and pointing to `.github/skills/task-orchestration/SKILL.md`.
- Update `.slnx` to add a `/Solution Items/` folder containing `README.md`, `AGENTS.md`, `Directory.Build.props`, `Directory.Packages.props` (if it exists), `global.json`, `.editorconfig` (if it exists).
- Define the template-scrub story. Pick **one**:
  - (a) Ship a `.template.config/template.json` for `dotnet new` packaging with `sourceName: "CopilotBlazorTemplate"`.
  - (b) Ship a `scripts/rename-template.sh` that runs `git mv` + find/replace for `CopilotBlazorTemplate`, regenerates the `UserSecretsId` GUID, and rewrites the README badge URL.
  - Recommendation: ship (b) first (lower-risk, no behaviour change). Add a short "Make It Yours" section in README that invokes it.
- Append three sections to `AGENTS.md`: **Instructions & Skills**, **Pre-flight**, **Definition of Done**.
- Add cross-vendor symlinks at the repo root: `CLAUDE.md → AGENTS.md`, `.cursorrules → AGENTS.md`. Document the symlinks in `AGENTS.md`.
- Add subtree AGENTS.md files where they earn their keep: `src/CopilotBlazorTemplate.Web/Components/AGENTS.md` (render-mode rules); note: `tests/AGENTS.md` may already be owned by task 07 — only ship if task 07 has not.

Out of scope:
- LICENSE / SECURITY.md / CONTRIBUTING.md / CODEOWNERS / PR template / CHANGELOG (owned by task 01).
- `<VersionPrefix>` in `Directory.Build.props` (owned by task 02).
- CI workflow rewrites (owned by task 08).
- New `.mcp.json` (owned by task 09).

## Edit zone
- `README.md` (this task owns rewrites; other tasks should append only)
- `AGENTS.md` (append only — three new sections at the bottom)
- `CLAUDE.md` (new — symlink to `AGENTS.md`)
- `.cursorrules` (new — symlink to `AGENTS.md`)
- `docs/README.md` (new)
- `docs/architecture/overview.md` (new)
- `docs/adr/0001-record-architecture-decisions.md` (new)
- `tasks/README.md` (new)
- `tasks/current/copilot-blazor-template/.gitkeep` (delete if empty and unused)
- `CopilotBlazorTemplate.slnx` (additive — append a `/Solution Items/` folder; do not modify the existing `tests/` or `src/` folders)
- `scripts/rename-template.sh` (new) **or** `.template.config/template.json` (new) — pick one
- `src/CopilotBlazorTemplate.Web/Components/AGENTS.md` (new)
- `tests/AGENTS.md` (new — only if task 07 has not shipped it)

## Independence guarantee
- AGENTS.md is touched **append-only**. The three new sections go at the bottom; the existing body is unchanged. This is the rule for any task that needs to add to AGENTS.md.
- `.slnx` edit is additive (one new `<Folder Name="/Solution Items/">` block). Task 06 may add a new `<Project>` line under `/tests/`; the two edits do not overlap.
- `tasks/README.md` is new; it complements `.github/skills/task-orchestration/SKILL.md` (owned by the skill brief) by being the human-facing entry point.
- Cross-vendor symlinks point at `AGENTS.md`. If task 07 ships `tests/AGENTS.md` first, the root `AGENTS.md` (and its symlinks) is unaffected.
- The `Subtree AGENTS.md` files use the hierarchical AGENTS.md convention — they extend rather than replace the root.
- If task 09 has shipped `.claude/agents/` and `.claude/skills/`, this task's README mentions them; if not, it points to `.github/agents/` and `.github/skills/`. Either branch is correct.
- Template-scrub script does not run automatically; it is opt-in. It must NOT touch `tasks/done/` history (immutable) — guard the find/replace with a directory exclude.
- README rewrites preserve existing sections (badges, architecture diagram, quick-start) and only add the missing ones in sensible positions.

This task may sit in `backlog/` for weeks. By the time it is picked up the docs surface may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before rewriting README, AGENTS.md, or `.slnx`, read them — sections this task plans to add may already be present (e.g. another task added a Prerequisites block, or task 01 already added a License section). Additively merge: insert only what's missing, leave existing content alone.
- **File moved/renamed.** Docs may have moved (e.g. `docs/screenshots/` reorganised). Locate via `ls docs/` or `git ls-files docs/`. If the solution file extension has changed (`.sln` ↔ `.slnx`), work against whichever exists today.
- **Prerequisite work already done.** Quick checks: does `tasks/README.md` already exist? Does `CLAUDE.md` already resolve to `AGENTS.md`? Does `scripts/rename-template.sh` already exist? Skip whatever is done and note in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if README already has Prerequisites, Running Tests, and License sections, just add the missing ones.
- If intent conflicts (e.g. someone wrote a `dotnet new` template at `.template.config/template.json` instead of a bash rename script), surface in the PR description; don't ship both.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read `README.md`, `AGENTS.md`, `CopilotBlazorTemplate.slnx`, the `docs/` tree, and `tasks/` end-to-end before editing. Check for existing symlinks (`ls -la CLAUDE.md .cursorrules`) and existing scripts (`ls scripts/`). The snippets below describe the *intent* of each addition — apply that intent to whatever exists today. If a file has been moved/renamed (e.g. the solution file converted to `.sln`), work against the current name. Skip steps whose intent is already in place and note in the PR description.
2. **README.md** restructure. Target order (insert missing sections; do NOT rewrite existing sections that already cover the topic):
   - Title + badges (keep current).
   - One-paragraph elevator pitch.
   - Quick start (keep current).
   - **Prerequisites** (new).
   - Architecture overview (keep current table).
   - Screenshots (keep).
   - **Running Tests** (new — `dotnet test`; `pwsh tests/CopilotBlazorTemplate.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium` first).
   - **Using With AI Agents** (new — link AGENTS.md, `.github/instructions/`, `.github/skills/`).
   - **Make It Yours** (new — point at `scripts/rename-template.sh`).
   - Tech stack (keep).
   - **Contributing** (link `CONTRIBUTING.md` if task 01 shipped, otherwise the section says "TODO link once task 01 lands").
   - **Security** (link `SECURITY.md`, same conditional).
   - **License** (shield + link `LICENSE`, same conditional).
   - Footer (keep).
3. **`docs/` reorganisation** (create files only where they do not already exist):
   - `docs/README.md` — one-screen index listing `architecture/`, `adr/`, `audits/`, `demo/`, `screenshots/`.
   - `docs/architecture/overview.md` — a Mermaid diagram of the request lifecycle (browser → ASP.NET Core → Razor components → EF Core → SQLite), plus short paragraphs on (a) per-component render modes (point at `Components/AGENTS.md`), (b) auth flow (cookie + Identity revalidation), (c) data flow (Web → Core, no reverse reference). 60-120 lines.
   - `docs/adr/0001-record-architecture-decisions.md` — verbatim Michael Nygard template ("We will use ADRs to record architectural decisions…").
4. **`tasks/README.md`** — short doc (skip if it already exists with equivalent content):
   ```markdown
   # Tasks

   Backlog → current → done. See `.github/skills/task-orchestration/SKILL.md`
   for the full contract. Each task folder owns its own files and declares
   an Edit zone in TASK.md so tasks can be picked in any order without
   collision.

   ## Quick start
   - To see what's queued: `ls tasks/backlog/`
   - To pick one: `git mv tasks/backlog/NN-foo tasks/current/NN-foo`
   - To finish: `git mv tasks/current/NN-foo tasks/done/NN-foo`
   ```
   If `tasks/current/copilot-blazor-template/.gitkeep` exists and is empty, remove it (the new `current/` lane is for real in-progress tasks).
5. **`.slnx` Solution Items** (append to the slnx file in use today; if the project has switched to `.sln`, add an equivalent Solution Items folder there instead):
   ```xml
   <Folder Name="/Solution Items/">
     <File Path="README.md" />
     <File Path="AGENTS.md" />
     <File Path="Directory.Build.props" />
     <File Path="Directory.Packages.props" />
     <File Path="global.json" />
     <File Path=".editorconfig" />
   </Folder>
   ```
   Append below the existing `/src/` and `/tests/` folders. Omit lines whose target files do not yet exist.
6. **`scripts/rename-template.sh`** — a portable bash script (skip if a rename mechanism already exists; do not ship both a script and a `dotnet new` template):
   ```bash
   #!/usr/bin/env bash
   set -euo pipefail
   new_name="${1:?usage: rename-template.sh <NewName>}"
   old=CopilotBlazorTemplate

   # 1. Replace strings in tracked files (skip tasks/done/, docs/audits/, .git, binaries)
   git ls-files | grep -vE '^(tasks/done/|docs/audits/|.*\.(png|jpg|webm|db))$' \
     | xargs sed -i "s/${old}/${new_name}/g"

   # 2. Rename files/folders
   git ls-files | grep "$old" | while read -r f; do
     git mv "$f" "${f//$old/$new_name}"
   done

   # 3. Regenerate UserSecretsId
   guid=$(uuidgen)
   sed -i "s/aspnet-${new_name}_Web-[a-f0-9-]*/aspnet-${new_name}_Web-${guid}/" \
       src/${new_name}.Web/${new_name}.Web.csproj

   # 4. Rewrite README badge URL
   sed -i "s|github.com/rquintino/copilot-blazor-template|github.com/<your-org>/${new_name,,}|" README.md

   echo "Done. Review with: git status && git diff --stat"
   ```
   Mark executable (`chmod +x`). The script intentionally excludes `tasks/done/` and `docs/audits/` to keep history immutable.
7. **AGENTS.md appendix** — append three sections at the bottom (skip any section whose intent is already covered above the appendix point):
   ```markdown
   ## Instructions & Skills
   - Path-scoped guidance lives in `.github/instructions/*.instructions.md`
     (auto-applied by GitHub Copilot when the file glob matches).
   - Reusable procedures live in `.github/skills/*/SKILL.md`.
   - Claude Code subagents live in `.claude/agents/` and skills in `.claude/skills/`
     (added by task 09 if shipped).

   ## Pre-flight (before committing)
   1. `dotnet build`
   2. `dotnet test`
   3. `dotnet format --verify-no-changes`

   ## Definition of Done
   - CI green on the PR.
   - Sticky `<!-- ci-test-summary -->` comment shows all green.
   - No new TODO/FIXME without a tracking task in `tasks/backlog/`.
   - This file is the canonical agent guide. `CLAUDE.md` and `.cursorrules`
     are symlinks; do not edit them directly.
   ```
8. **Cross-vendor symlinks** (skip if they already exist and resolve correctly):
   ```bash
   ln -s AGENTS.md CLAUDE.md
   ln -s AGENTS.md .cursorrules
   ```
   Commit the symlinks (`git ls-files` will show them).
9. **`src/CopilotBlazorTemplate.Web/Components/AGENTS.md`** (skip if present; locate via `git ls-files`) — short (≤30 lines) note documenting:
   - Default render mode is static SSR.
   - Opt in to `@rendermode InteractiveServer` only when needed.
   - Use `@attribute [StreamRendering]` for async data on init.
   - Wrap `@Body` in `<ErrorBoundary>` in layouts.
10. **`tests/AGENTS.md`** — only if task 07 has not shipped it. Short note pointing to the singleton `PlaywrightFixture`, the cached storage-state contexts, and the assertion library convention.

## Acceptance criteria
- [ ] README.md contains Prerequisites, Running Tests, Using With AI Agents, Make It Yours, Contributing, Security, License sections.
- [ ] `docs/README.md`, `docs/architecture/overview.md`, `docs/adr/0001-record-architecture-decisions.md` exist.
- [ ] `tasks/README.md` exists and links to `.github/skills/task-orchestration/SKILL.md`.
- [ ] `CopilotBlazorTemplate.slnx` has a `/Solution Items/` folder listing the root meta files.
- [ ] Either `scripts/rename-template.sh` (executable) or `.template.config/template.json` exists.
- [ ] AGENTS.md has Instructions & Skills, Pre-flight, and Definition of Done sections appended (existing content unchanged).
- [ ] `CLAUDE.md` and `.cursorrules` symlinks exist and resolve to `AGENTS.md`.
- [ ] `src/CopilotBlazorTemplate.Web/Components/AGENTS.md` exists.
- [ ] `dotnet build` and `dotnet test` still pass (no source code touched).
- [ ] `git ls-files` shows the new and renamed files; no `tasks/done/` or `docs/audits/` files were modified.

## References
- Audit Sprint-1 items 23 (meta files — owned by task 01), 24 (AGENTS.md sections + symlinks), 25 (template-scrub story): `../../../docs/audits/2026-05-18/REPORT.md`.
- Repo Organization findings "Template-scrub story is undefined", "README missing standard meta sections", "`tasks/` is committed empty with undocumented workflow", "`docs/` is screenshots-only", "`.slnx` solution folders don't surface `docs/`, `build/`, or root meta files": `../../../docs/audits/2026-05-18/findings/02-repository-organization.md`.
- Agentic Development findings "AGENTS.md is missing several discoverability hooks", "No Cursor / no `CLAUDE.md`", "README.md 'agent support' line is buried": `../../../docs/audits/2026-05-18/findings/05-agentic-development.md`.
- AGENTS.md spec — <https://agents.md/>
- Michael Nygard's ADR template — <https://github.com/joelparkerhenderson/architecture-decision-record>
- `dotnet new` template authoring — <https://learn.microsoft.com/dotnet/core/tutorials/cli-templates-create-item-template>
