# Agentic Development Audit

## Summary

This is a self-described "agent-ready" .NET 10 Blazor template, and the agentic surface area is genuinely well-organised for a starter: a tight `AGENTS.md`, three path-scoped Copilot instruction files, two agent role definitions, two skills, a format-on-edit hook, a Copilot coding-agent bootstrap workflow, and a sticky-comment CI summary. The signal density is high and there is almost no contradiction across files, which is rare. However, several assets are using **conventions that do not match the current (late-2025) specs** of the tools they claim to target: the `.github/agents/*.agent.md` files are not in any documented GitHub Copilot Coding Agent or AGENTS.md sub-agent format; the `.github/hooks/format-on-edit.json` schema does not match Claude Code's `settings.json` hooks format; the `.github/skills/*/SKILL.md` files lack the YAML frontmatter (`name`, `description`) that Claude Code's Agent Skills spec requires for discovery. The audit brief lists `.mcp.json` as a present asset, but the repo does **not** track an `.mcp.json` (the file on disk is a sandbox stub), so MCP wiring is effectively absent. Net: strong scaffolding, but the agentic configs are aspirational rather than executable — fixing schemas would unlock most of the value already designed in.

## Strengths

- `AGENTS.md` is short (60 lines), command-table-first, and faithful to the actual repo layout — exactly what every modern coding agent (Copilot, Claude Code, Cursor, Codex, Aider) wants on first read.
- Path-scoped Copilot instruction files (`applyTo:` frontmatter) follow GitHub's `.github/instructions/*.instructions.md` convention introduced in 2025 and are correctly globbed.
- Each instruction file is tightly focused (Blazor / EF Core / Playwright) and avoids duplicating `AGENTS.md` content.
- `.github/workflows/copilot-setup-steps.yml` follows the GitHub Copilot Coding Agent special-name convention so the agent provisions dotnet 10, Node 22, `dotnet-ef`, and Playwright browsers before it starts.
- CI posts a single sticky `<!-- ci-test-summary -->` comment with TRX results — this is the right pattern for agent-readable PR feedback (one stable artifact instead of comment spam).
- Seeded credentials and login form selectors are repeated in `AGENTS.md`, `playwright-tests.instructions.md`, and the skills — agents don't need to grep for them.
- `screenshots-demo` skill is genuinely useful (visual proof artifacts, uploaded by CI), and is reproducible from clone.
- `Dependabot` covers both `nuget` and `npm`.

## Inventory of agentic assets

| File | Purpose | Owner tool | Quality |
|---|---|---|---|
| `AGENTS.md` | Universal agent guide (structure, commands, conventions, seed creds) | Cross-vendor (AGENTS.md draft spec) | Good |
| `.github/agents/dev.agent.md` | "Dev" agent role definition | Unclear — not GitHub Copilot Coding Agent format | Weak (schema invented) |
| `.github/agents/test.agent.md` | "Test" agent role definition | Same as above | Weak (schema invented) |
| `.github/instructions/blazor.instructions.md` | Copilot custom rules for `**/*.razor` | GitHub Copilot (VS / VS Code) | Good |
| `.github/instructions/efcore.instructions.md` | Copilot rules for `**/Data/**,**/Entities/**` | GitHub Copilot | Good (glob has minor issue, see findings) |
| `.github/instructions/playwright-tests.instructions.md` | Copilot rules for `**/E2ETests/**` | GitHub Copilot | Good |
| `.github/skills/playwright-e2e/SKILL.md` | Reference doc for writing E2E tests | Intended as Claude Code Skill | Mediocre (no frontmatter, misnamed) |
| `.github/skills/screenshots-demo/SKILL.md` + `capture.js` | Capture screenshots + record demo webm | Intended as Claude Code Skill | Mediocre format, strong content |
| `.github/hooks/format-on-edit.json` | Auto-`dotnet format` after edits | Intended as Claude Code hook (?) | Broken (wrong schema, see findings) |
| `.github/hooks/scripts/format-csharp.sh` | Hook handler — runs `dotnet format` | shell | Risky (whole-repo format on every edit) |
| `.github/workflows/copilot-setup-steps.yml` | Provision toolchain for Copilot coding agent | GitHub Copilot Coding Agent | Good with caveats |
| `.github/workflows/ci.yml` | Build, test, sticky PR comment | GitHub Actions | Good |
| `.github/scripts/build_test_summary.py` | Parse TRX → markdown summary | CI | Good |
| `.github/dependabot.yml` | Weekly nuget + npm updates | Dependabot | Minimal (missing `github-actions`, grouping) |
| `scripts/setup-dev.sh` | Local dev bootstrap (mirrors copilot-setup-steps) | humans + agents | Adequate (duplication risk) |
| `scripts/record-demo.mjs` | Same as skill `capture.js` — duplicated | humans | Duplicate of skill |
| `README.md` | Project docs | humans | Has small "agent support" mention only |
| `.mcp.json` | (Not actually tracked in git — sandbox stub only) | — | **Missing** |
| No `.cursor/rules` / `.cursorrules` | — | Cursor | Missing |
| No `CLAUDE.md` / `.claude/` tracked | — | Claude Code | Missing (relies on AGENTS.md) |

## Findings

### [Severity: High] `.github/hooks/format-on-edit.json` is not a real Claude Code hook

**Where:** `.github/hooks/format-on-edit.json`

**Current:**
```json
{ "hooks": [ { "name": "format-csharp", "event": "postToolUse",
  "pattern": "**/*.cs", "command": ".github/hooks/scripts/format-csharp.sh" } ] }
```

**Recommended:** Claude Code's hook schema lives in `.claude/settings.json` (or `~/.claude/settings.json`), not in arbitrary `.github/hooks/*.json` files. The event name is `PostToolUse` (capitalised) and entries are matcher-keyed under `hooks.PostToolUse[].matcher` with `hooks[].type:"command"` and `hooks[].command`. Move this to `.claude/settings.json` and use the file matcher pattern via `Edit|Write|MultiEdit`, gating in the shell script on the filename. Example:

```json
{
  "hooks": {
    "PostToolUse": [
      { "matcher": "Edit|Write|MultiEdit",
        "hooks": [ { "type": "command",
          "command": ".github/hooks/scripts/format-csharp.sh \"$CLAUDE_FILE_PATH\"" } ] }
    ]
  }
}
```

For GitHub Copilot there is no equivalent client hook — formatting belongs in `dotnet format` invoked by the agent or in a pre-commit hook. Consider also adding a `pre-commit` config (`pre-commit-dotnet-format` or a `.husky` hook) so the same behaviour is enforced regardless of which agent edited the file.

**Why:** As written, Claude Code will silently ignore this file (wrong path, wrong event-name casing, missing matcher). No tool consumes it today.

**Effort:** S

---

### [Severity: High] `format-csharp.sh` runs `dotnet format` over the entire solution on every C# edit

**Where:** `.github/hooks/scripts/format-csharp.sh`

**Current:**
```bash
dotnet format --verbosity quiet 2>/dev/null || true
```

**Recommended:** Scope to the changed file and a specific project; suppress whitespace-only churn outside the edit:

```bash
file="${1:-}"
[ -z "$file" ] && exit 0
[ "${file##*.}" = "cs" ] || exit 0
dotnet format whitespace --include "$file" --verbosity quiet || true
dotnet format style      --include "$file" --verbosity quiet || true
```

Also: swallowing stderr with `2>/dev/null || true` hides real failures — at minimum write the error to `$TMPDIR/format.log` so an agent can spot regressions.

**Why:** A full-solution `dotnet format` after every Edit is multi-second and re-touches unrelated files, which (a) creates noisy diffs the agent later has to explain, (b) defeats `dotnet format --verify-no-changes` in CI if file ordering differs, and (c) compounds latency in a turn-by-turn agent loop. Per-file formatting is the documented best practice for editor/agent hooks in 2025.

**Effort:** S

---

### [Severity: High] `.github/agents/*.agent.md` use an undocumented schema

**Where:** `.github/agents/dev.agent.md`, `.github/agents/test.agent.md`

**Current:** Free-form markdown with no frontmatter, named `*.agent.md`. There is no GitHub Copilot, Claude Code, or AGENTS.md convention that consumes `.github/agents/*.agent.md`.

**Recommended:** Decide which tool these target and conform to its spec:

- **GitHub Copilot Coding Agent prompts:** these live as natural-language issue templates or `copilot-instructions.md` in `.github/`. There is no `agents/` directory contract.
- **Claude Code subagents:** spec is `.claude/agents/<name>.md` with YAML frontmatter (`name`, `description`, `tools`, `model`). Move + add frontmatter:

  ```yaml
  ---
  name: dev
  description: Implements features and fixes in the .NET 10 Blazor template. Always builds and tests before finishing.
  tools: Read, Edit, Write, Bash, Grep
  model: inherit
  ---
  ```
- **VS Code "chat modes":** `.github/chatmodes/*.chatmode.md` with frontmatter `description:`, `tools:`, `model:` (preview feature).

Pick one (or both) and rename. Leaving the files as-is means no agent ever loads them; the role guidance ends up duplicating `AGENTS.md` for no consumer.

**Why:** As written these files are documentation for humans only. The naming implies machine consumption that doesn't happen, which is misleading to template users who fork this.

**Effort:** S

---

### [Severity: High] SKILL.md files are missing the Agent Skills frontmatter

**Where:** `.github/skills/playwright-e2e/SKILL.md`, `.github/skills/screenshots-demo/SKILL.md`

**Current:** Plain markdown headed `# Playwright E2E Testing Skill` / `# Screenshots & Demo Video Capture Skill`. No YAML frontmatter.

**Recommended:** The Anthropic Agent Skills spec (used by Claude Code, the Claude API, and `claude.ai`) requires a `SKILL.md` to start with YAML frontmatter containing at minimum a `name` and a `description` written in third person, plus optional `allowed-tools` and `license`:

```yaml
---
name: screenshots-demo
description: Captures landing/login/dashboard/admin screenshots and records a ~30-45s WebM demo of the Copilot Blazor Template using Playwright + Chromium. Use when the user asks for fresh README screenshots or a demo video.
allowed-tools: Bash, Read, Write
license: MIT
---
```

The `description` is the only field a model sees at skill-selection time — it must be self-contained ("**what** it does and **when** to invoke it"). Also: skills are discovered from `.claude/skills/` or `~/.claude/skills/` (or a configured plugin), not `.github/skills/`. Either move them or document a symlink/loader. If the intent is "shared docs both Claude Code and Copilot can read", consider the cross-vendor pattern of keeping them in `.github/skills/` **and** symlinking `.claude/skills -> ../.github/skills` so both ecosystems see them.

**Why:** Without the frontmatter, Claude Code will refuse to register these as skills (or treat them as plain markdown). With the frontmatter and correct path they become invocable. The `playwright-e2e` SKILL is also a weak skill candidate — it's really developer documentation, not a procedure — see next finding.

**Effort:** S

---

### [Severity: Med] `playwright-e2e` skill is documentation, not a skill

**Where:** `.github/skills/playwright-e2e/SKILL.md`

**Current:** Reads as a tutorial: "Test patterns", "Login Helper", "Route Testing", "Running Tests". Mostly overlaps with `.github/instructions/playwright-tests.instructions.md` and the AGENTS.md command table.

**Recommended:** Either (a) delete it and rely on the instruction file (single source of truth), or (b) reframe as an actual procedure: "Adds a new E2E test for an authenticated page. Pre-conditions: app builds. Steps: 1) duplicate `AuthTests.cs`, 2) replace selectors, 3) run a targeted `dotnet test --filter`, 4) add to `xunit.runner.json` parallel group." Skills are useful when they automate or templatise a *workflow*, not when they restate facts.

**Why:** Currently three files (AGENTS, instructions, skill) repeat the same Playwright facts. Triple-bookkeeping invites drift.

**Effort:** S

---

### [Severity: Med] `.mcp.json` is mentioned in the audit scope but is absent from the repo

**Where:** Project root (expected `.mcp.json`)

**Current:** No `.mcp.json` is tracked in git (`git ls-files | grep mcp` returns only the workflow). The on-disk file is a sandbox-blocked stub.

**Recommended:** Add a minimal, vendor-neutral `.mcp.json` that is genuinely useful for this stack. MCP servers worth considering for a Blazor/EF template:

```jsonc
{
  "$schema": "https://modelcontextprotocol.io/schemas/mcp.json",
  "mcpServers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem@<pinned>", "${workspaceFolder}"],
      "env": {}
    },
    "sqlite": {
      "command": "uvx",
      "args": ["mcp-server-sqlite@<pinned>",
               "--db-path", "${workspaceFolder}/src/CopilotBlazorTemplate.Web/app.db"]
    },
    "playwright": {
      "command": "npx",
      "args": ["-y", "@playwright/mcp@<pinned>"]
    }
  }
}
```

Guidance:
- **Pin every version** (no `@latest`) — supply-chain hygiene, also avoids cool-down violations.
- **Scope filesystem to the workspace**, never `/`.
- **Do not** add servers that require secrets (GitHub token, etc.) in a public template — document instead and let users opt in.
- Add a `.mcp.json` README block in `AGENTS.md` so agents know it's there.

**Why:** The template positions itself as "agent-ready". For Claude Code and any MCP-aware client, a curated `.mcp.json` is the single biggest force multiplier — it gives the agent direct DB inspection, browser automation, and bounded FS access without re-asking the user. Currently every agent has to shell out to `sqlite3` or `dotnet ef dbcontext info` instead.

**Effort:** M

---

### [Severity: Med] `copilot-setup-steps.yml` installs fictional/unstable bits and lacks caching

**Where:** `.github/workflows/copilot-setup-steps.yml`

**Current:**
```yaml
- name: Install Playwright CLI
  run: npm install -g @playwright/cli@latest --minimum-release-age=0
- name: Install Playwright skills
  run: |
    playwright-cli install || true
    playwright-cli install --skills || true
- name: Install Blazor Blueprint MCP
  run: npx @blazorblueprint/mcp@latest || true
```

Issues:
1. `@playwright/cli` is not the canonical package — Playwright ships its CLI inside `playwright` (`npx playwright …`). `--minimum-release-age=0` deliberately defeats the supply-chain cool-down for no stated reason.
2. `playwright-cli install --skills` is not a real subcommand and is masked by `|| true`.
3. `@blazorblueprint/mcp` is not a recognised package — masked install failures will silently leave the agent without MCP.
4. No `actions/cache` for NuGet (`~/.nuget/packages`) or npm — every Copilot agent run pays full restore cost.
5. `dotnet restore || true` and `dotnet build || true` hide bootstrap failures the coding agent must know about.

**Recommended:**
```yaml
- uses: actions/setup-dotnet@v4
  with: { dotnet-version: "10.x", cache: true, cache-dependency-path: "**/*.csproj" }
- uses: actions/setup-node@v4
  with: { node-version: "22", cache: "npm" }
- run: dotnet tool install --global dotnet-ef --version 10.*
- run: dotnet restore
- run: dotnet build --no-restore
- name: Install Playwright browsers
  run: |
    cd tests/CopilotBlazorTemplate.E2ETests
    pwsh bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

Drop the `playwright-cli`/`blueprint` lines, or replace them with the real MCP servers you actually intend to ship (and pin them).

**Why:** The Copilot Coding Agent runs this workflow once per session to pre-warm the environment; if steps silently fail or install nonexistent packages, the agent starts each session in a degraded state and spends turns reinstalling. `|| true` on bootstrap is an anti-pattern — let the workflow fail loudly.

**Effort:** S

---

### [Severity: Med] `scripts/record-demo.mjs` and `.github/skills/screenshots-demo/capture.js` are 95% duplicates

**Where:** `scripts/record-demo.mjs` vs `.github/skills/screenshots-demo/capture.js`

**Current:** Two near-identical Playwright recorders. Same selectors, same banner code, same flow. The skill version also captures screenshots (the script does not).

**Recommended:** Delete `scripts/record-demo.mjs` and have it become a one-liner that invokes the skill's `capture.js` (or vice versa). One source of truth; the other is a thin shell entry-point. Update the skill's frontmatter `description` to mention both outputs (screenshots + video).

**Why:** Drift between the two is inevitable — a selector change in one is invisible to the other and will only surface when the README screenshots stop refreshing.

**Effort:** S

---

### [Severity: Med] `AGENTS.md` is missing several discoverability hooks

**Where:** `AGENTS.md`

**Current:** Good content, but lacks:
- A pointer to the path-scoped instruction files (so an agent knows they'll automatically apply).
- A "Pre-flight" section: which commands must succeed before commit (`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`).
- A list of MCP servers (when added) and where to read more.
- Test-suite quirks (Playwright fixture, shared singleton + storage-state — see recent commits) — an agent will otherwise rebuild login state per test.
- A "Definition of Done" / PR checklist (CI green, sticky comment passes, no `dotnet format` diff).
- An "if you get stuck" pointer to `docs/` and recent commits.

**Recommended:** Append three short sections to `AGENTS.md` (target: still under 120 lines total):

```
## Instructions & Skills
- Path-scoped guidance lives in `.github/instructions/*.instructions.md`
  (auto-applied by Copilot when the file glob matches).
- Reusable procedures live in `.github/skills/*/SKILL.md`.

## Pre-flight (before committing)
1. `dotnet build`
2. `dotnet test`
3. `dotnet format --verify-no-changes`

## Definition of Done
- CI green on the PR
- Sticky `<!-- ci-test-summary -->` comment shows all green
- No new TODO/FIXME without a tracking issue
```

**Why:** AGENTS.md is the one file every modern agent reads first. Adding 20 lines saves the agent from re-discovering the rest of the structure across multiple turns.

**Effort:** S

---

### [Severity: Low] `efcore.instructions.md` glob `**/Data/**,**/Entities/**` may misbehave across tools

**Where:** `.github/instructions/efcore.instructions.md`, frontmatter `applyTo:`

**Current:** `applyTo: "**/Data/**,**/Entities/**"`

**Recommended:** The comma-separated form is supported by current Copilot but is brittle. The portable form (also documented) is brace expansion: `applyTo: "**/{Data,Entities}/**"`. Test with both VS and VS Code to confirm. Also note: the `Migrations/` directory is *inside* `Data/` in this project only by convention (actually it's a sibling) — verify the glob actually catches migration files if that's intended.

**Why:** Glob syntax inconsistency is a known papercut in 2025 Copilot custom-instruction docs; brace form is universally supported.

**Effort:** S

---

### [Severity: Low] Dependabot config is minimal

**Where:** `.github/dependabot.yml`

**Current:** weekly nuget + npm, no `github-actions`, no grouping, no security-update preference.

**Recommended:**
```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule: { interval: "weekly" }
    open-pull-requests-limit: 5
    groups:
      microsoft:
        patterns: ["Microsoft.*", "System.*"]
      test-deps:
        patterns: ["xunit*", "Microsoft.Playwright*", "coverlet*"]
  - package-ecosystem: "npm"
    directory: "/"
    schedule: { interval: "weekly" }
    groups:
      playwright:
        patterns: ["playwright", "@playwright/*"]
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule: { interval: "weekly" }
```

**Why:** Without `github-actions`, the workflow pins (`actions/checkout@v4`, etc.) drift behind security advisories. Grouping reduces PR noise that an agent would otherwise have to triage.

**Effort:** S

---

### [Severity: Low] No Cursor / no `CLAUDE.md` — cross-vendor coverage is partial

**Where:** project root

**Current:** No `.cursor/rules/*.mdc`, no `.cursorrules`, no `CLAUDE.md`.

**Recommended:** Since AGENTS.md exists and is good, the cheapest cross-vendor move is symlinks at the repo root:

```bash
ln -s AGENTS.md CLAUDE.md
ln -s AGENTS.md .cursorrules    # legacy Cursor format
mkdir -p .cursor/rules && ln -s ../../AGENTS.md .cursor/rules/AGENTS.mdc
```

(Commit the symlinks; document in `AGENTS.md` "this is the canonical file — others are symlinks".)

**Why:** AGENTS.md adoption is growing but not universal. Until then, symlinks give zero-maintenance multi-tool coverage. Anthropic's Claude Code currently still prefers `CLAUDE.md`; Cursor still reads `.cursorrules` for legacy projects.

**Effort:** S

---

### [Severity: Low] `README.md` "agent support" line is buried at the bottom of a feature list

**Where:** `README.md`

**Current:** "Copilot agent support (AGENTS.md, instructions, skills)" appears as the 8th bullet of "What's Included", with no explanatory link.

**Recommended:** Add a short "Using With AI Agents" section linking to `AGENTS.md`, `.github/instructions/`, and `.github/skills/`. The README is what humans see first when forking the template; if the agentic story is the differentiator, surface it.

**Why:** A user forking this template needs to know the agentic assets exist and where to point their agent.

**Effort:** S

---

### [Severity: Low] CI does not run `dotnet format --verify-no-changes`

**Where:** `.github/workflows/ci.yml`

**Current:** No format-check step. With a broken format-on-edit hook (above), nothing actually enforces style.

**Recommended:**
```yaml
- name: Verify formatting
  run: dotnet format --verify-no-changes --no-restore
```
Run it before tests so an agent gets the failure fast.

**Why:** Either the hook should work or CI should enforce; currently neither does, and the project has `nullable` + file-scoped namespaces conventions that only stay clean if something checks.

**Effort:** S

---

### [Severity: Low] Skill `capture.js` hard-codes `/home/runner/work/...` in `SKILL.md` step 2

**Where:** `.github/skills/screenshots-demo/SKILL.md` lines 22-23

**Current:** `cd /home/runner/work/copilot-blazor-template/copilot-blazor-template` — only valid on a GitHub Actions runner.

**Recommended:** Use `cd "$(git rev-parse --show-toplevel)"` or document that the skill is CI-only.

**Why:** A local agent following step 2 verbatim will fail. Small but reduces friction.

**Effort:** S

## Suggested additions (new assets)

- **`.mcp.json`** (root) — pinned `filesystem` (scoped to workspace), `sqlite` (scoped to `app.db`), `playwright` MCP servers. Single biggest agent UX upgrade.
- **`.github/copilot-instructions.md`** — repo-wide Copilot rules complementing path-scoped ones (Copilot reads this as a default). Could be 10 lines pointing at AGENTS.md.
- **`.claude/agents/dev.md` and `test.md`** — actual Claude Code subagent definitions with proper frontmatter (replace or supplement `.github/agents/*`).
- **`.claude/skills/migration-helper/SKILL.md`** — automates the `dotnet ef migrations add → update database → commit` loop, which is the most repetitive turn in this template.
- **`.claude/skills/release/SKILL.md`** — bump version in `Directory.Build.props`, tag, push, draft GitHub release notes from sticky CI comments.
- **`.github/skills/spec-driven/SKILL.md`** — `/spec` style workflow: scaffold `tasks/current/<feature>/spec.md` from a one-line prompt, then `plan.md`, then implementation diffs. Pairs well with the existing `tasks/backlog`+`tasks/current` directory structure already in the repo.
- **`pre-commit` config or `lefthook.yml`** — enforce `dotnet format` regardless of which agent edited; complements (or replaces) the hook.
- **`AGENTS.md` per subtree** — `tests/AGENTS.md` ("how to add a test, fixture quirks") and `src/CopilotBlazorTemplate.Web/Components/AGENTS.md` ("render-mode rules, layout selection"). AGENTS.md spec allows hierarchical files; closer-to-code beats one big root file.
- **`docs/agentic.md`** — a single page documenting the whole agentic stack (what each file is, which tool consumes it). The audit you're reading now is essentially the first draft.
- **CI step: ai-bot label gating** — for PRs labelled `copilot` or `claude`, run an extra `dotnet format --verify-no-changes` and an `Are migrations up to date?` check, and post a checklist in the sticky comment.

## Quick wins (top 5)

1. **Fix the hook schema and scope** — move to `.claude/settings.json`, scope `dotnet format` to the edited file. Removes silent failure + slowness. (15 min)
2. **Add YAML frontmatter to both `SKILL.md` files** and move them under `.claude/skills/` (or symlink). Makes them actually loadable. (10 min)
3. **Add a real `.mcp.json`** with `filesystem` + `sqlite` + `playwright`, all pinned. Biggest agent capability uplift. (30 min)
4. **Clean up `copilot-setup-steps.yml`** — drop fictional `@playwright/cli`/`@blazorblueprint/mcp`, add caching, remove `|| true` on critical steps. (15 min)
5. **Append the "Instructions & Skills", "Pre-flight", and "Definition of Done" sections to `AGENTS.md`**, and symlink `CLAUDE.md` + `.cursorrules` to it. Free cross-vendor coverage. (10 min)

## References

- AGENTS.md proposal/spec: <https://agents.md/>
- GitHub Copilot custom instructions (`.github/copilot-instructions.md`, `.github/instructions/*.instructions.md`, `applyTo:` frontmatter): GitHub Docs → "Adding repository custom instructions for GitHub Copilot"
- GitHub Copilot Coding Agent setup (`.github/workflows/copilot-setup-steps.yml` special-named workflow): GitHub Docs → "Customizing the development environment for GitHub Copilot coding agent"
- Anthropic Agent Skills spec (`SKILL.md` frontmatter `name`/`description`, third-person guidance): <https://docs.anthropic.com/en/docs/agents-and-tools/agent-skills>
- Claude Code subagents (`.claude/agents/<name>.md` with `name`, `description`, `tools`, `model`): <https://docs.claude.com/en/docs/claude-code/sub-agents>
- Claude Code hooks (`PostToolUse`, matcher, `command` shape, `.claude/settings.json` location): <https://docs.claude.com/en/docs/claude-code/hooks>
- Model Context Protocol — server registry & security guidance: <https://modelcontextprotocol.io/>
- VS Code chat modes (`.github/chatmodes/*.chatmode.md`): VS Code Insiders docs
- Dependabot grouping and `github-actions` ecosystem: GitHub Docs → "Configuration options for the dependabot.yml file"
- Playwright official CLI (`npx playwright …`, not `@playwright/cli`): <https://playwright.dev/docs/intro>
