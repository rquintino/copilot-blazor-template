# Repository Organization Audit

## Summary
The repo follows the modern src/ + tests/ layout, uses the new `.slnx` solution format, and has a small, clean project graph (Web -> Core, two test projects). Solid bones for a 2026-era .NET 10 Blazor template. However, several "table-stakes" files for a public template are missing (LICENSE, CONTRIBUTING, SECURITY, .editorconfig, .gitattributes, issue/PR templates), `Directory.Packages.props` (Central Package Management) is not adopted despite five `PackageReference` entries duplicating version `10.0.8` across four csproj files, and `packages.lock.json` is contradictorily both generated (`RestorePackagesWithLockFile=true`) and gitignored. The `tasks/` directory is essentially empty in source control (one `.gitkeep`) with no documented workflow, and `docs/` is screenshots-only with no architecture/ADR/runbook content. Template-scrub story is undefined: `CopilotBlazorTemplate` is hard-coded into ~50 files plus a `UserSecretsId` GUID and a real GitHub badge URL.

## Strengths
- Clean `src/` vs `tests/` separation with matching `.slnx` solution folders.
- `.slnx` (XML solution) format adopted — current MS recommendation.
- `Directory.Build.props` already present with `NuGetAudit` enabled at `low` and lockfile restore turned on.
- `global.json` pins SDK with `rollForward: latestFeature` (good balance for a template).
- Project boundaries look correct at a glance: `Core` has Entities/Data/Migrations only and no ASP.NET Core/Blazor references; `Web` references `Core` one-way.
- Test project naming mirrors target assemblies (`*.UnitTests`, `*.E2ETests`) — discoverable.
- `Dependabot` configured for both NuGet and npm with sensible weekly cadence + PR limit.
- README has badges, screenshots, quick-start, seeded credentials, architecture table, and a "How to Use This Template" section.
- AGENTS.md documents project structure, commands and conventions — duplicates some README content but useful for agents.
- `.npmrc` enforces `min-release-age=15` (good supply-chain hygiene at the repo level).
- `InternalsVisibleTo` from Web to E2ETests is declared in the csproj rather than via assembly attribute — modern style.

## Findings

### [High] Missing LICENSE file
**Where:** repository root
**Current:** No `LICENSE` / `LICENSE.md` tracked (`git ls-files` shows none).
**Recommended:** Add an OSI-approved license file (MIT is the de-facto default for .NET templates; Apache-2.0 if patent grant matters). Reference it in `README.md` and in csproj via `PackageLicenseExpression` if you ever publish.
**Why:** Without a LICENSE, the code is "all rights reserved" by default. Template repos are forked routinely; downstream users have no legal right to do so. GitHub also surfaces the license in the sidebar — its absence is a strong "unprofessional" signal.
**Effort:** S

### [High] No Central Package Management (`Directory.Packages.props`)
**Where:** repo root (file missing); versions live in each csproj
**Current:** Four csproj files redeclare `Microsoft.EntityFrameworkCore.*` `10.0.8`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`, `coverlet.collector 6.0.4`, `Microsoft.NET.Test.Sdk 17.14.1`, etc.
**Recommended:** Add `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and move all `Version=` attributes there. Leave `<PackageReference Include="…" />` (no version) in csproj files.
**Why:** CPM has been the recommended pattern for multi-project .NET repos since .NET 7 and is the de-facto standard in 2025/2026. Eliminates version drift, simplifies dependabot diffs, and makes audit/upgrades atomic.
**Effort:** S

### [High] `packages.lock.json` is gitignored but also generated
**Where:** `.gitignore` line 26-27; `Directory.Build.props` line 6
**Current:** `RestorePackagesWithLockFile=true` produces lockfiles in every project, but `.gitignore` ends with `## NuGet` + `packages.lock.json`, so they are never committed. CI cannot use `--locked-mode` reproducibly.
**Recommended:** Remove `packages.lock.json` from `.gitignore`, commit the four existing lockfiles, and add `--locked-mode` (or set `RestoreLockedMode=true` in CI) to the `dotnet restore` step in `.github/workflows/ci.yml`.
**Why:** The whole point of `RestorePackagesWithLockFile` is reproducible restores — they only work when the lockfile is committed. This is also called out in your global CLAUDE.md ("Always commit lockfiles").
**Effort:** S

### [High] Missing `.editorconfig`
**Where:** repository root
**Current:** None tracked. Format-on-edit hook exists (`.github/hooks/format-on-edit.json`) and `dotnet format` is in AGENTS.md, but with no `.editorconfig` the rules come from defaults only.
**Recommended:** Add a root `.editorconfig` covering: indentation (4 spaces C#, 2 spaces JSON/YAML/MD/JS), `end_of_line = lf`, `insert_final_newline = true`, `charset = utf-8`, and the standard C# `dotnet_style_*` / `csharp_style_*` analyzer rules. `dotnet new editorconfig` generates a sensible baseline.
**Why:** Without it, IDE settings vary between contributors, `dotnet format` produces near-empty diffs, and Roslyn analyzer severities default to "suggestion" rather than enforced.
**Effort:** S

### [Med] Missing `.gitattributes`
**Where:** repository root
**Current:** None tracked. Windows contributors on a Blazor template (very likely audience) can introduce CRLF/LF churn, especially in `.razor`/`.cshtml`/`.css` files.
**Recommended:** Add `.gitattributes` enforcing `* text=auto eol=lf` for code (`.cs`, `.razor`, `.csproj`, `.slnx`, `.json`, `.yml`, `.md`, `.sh`), `eol=crlf` for `.cmd`/`.bat`, `binary` for images, `.webm`, and `*.db`. Mark `*.razor diff=html`, `*.cs diff=csharp` for nicer diffs.
**Why:** Standard hygiene for any cross-platform .NET repo; prevents the "the entire file changed" PR noise.
**Effort:** S

### [Med] Missing `SECURITY.md` and `CONTRIBUTING.md`
**Where:** root or `.github/`
**Current:** Neither file exists.
**Recommended:** Add `SECURITY.md` (vulnerability disclosure address + supported versions matrix; you can be brief for a template) and `CONTRIBUTING.md` (branch policy, commit message style, `dotnet test`/`dotnet format` requirements, how to run E2E tests). Both can live under `.github/` to keep root clean.
**Why:** GitHub surfaces both in the "Security" tab and the contribute affordance. For a publicly-forked template, the absence of SECURITY.md is also a Dependabot/CodeQL signal.
**Effort:** S

### [Med] No issue or PR templates
**Where:** `.github/ISSUE_TEMPLATE/`, `.github/PULL_REQUEST_TEMPLATE.md`
**Current:** Neither exists.
**Recommended:** Add at minimum a `PULL_REQUEST_TEMPLATE.md` with a checklist (build green, tests added, format applied, screenshots for UI). Add one or two issue templates (`bug_report.yml`, `feature_request.yml`) using the YAML form schema.
**Why:** Templates set expectations for contributors and structure incoming triage. For an agent-friendly template, a PR template doubles as a checklist agents can self-validate against.
**Effort:** S

### [Med] `tasks/` is committed empty with undocumented workflow
**Where:** `tasks/current/copilot-blazor-template/.gitkeep` is the only tracked file; `tasks/backlog/` is new (only this audit lives there).
**Current:** Two-tier `current/` vs `backlog/` folders exist but nothing explains them. README/AGENTS.md don't mention `tasks/` at all.
**Recommended:** Either (a) document the workflow in `tasks/README.md` (one paragraph: file naming, lifecycle current -> done, who reads them) and ship one example file in `current/`, or (b) move task tracking to GitHub Issues/Projects and remove the directory from the template. As a template, shipping an opinionated empty folder structure without explanation is more confusing than helpful.
**Why:** Template forkers will inherit `tasks/` and ask "what is this?". Either commit to it as a documented convention or drop it.
**Effort:** S

### [Med] `docs/` is screenshots-only — no architecture, ADRs, or runbooks
**Where:** `docs/demo/`, `docs/screenshots/`
**Current:** One `.webm` demo + four PNGs. No `docs/architecture.md`, no `docs/adr/`, no `docs/getting-started.md`.
**Recommended:** Reorganize as `docs/architecture/` (one diagram + paragraph on render modes, auth flow, data flow), `docs/adr/` (with `0001-record-architecture-decisions.md` from Michael Nygard), `docs/screenshots/`, `docs/demo/`. Add a `docs/README.md` index.
**Why:** A "template" that ships with no architectural narrative forces every forker to re-derive intent. ADRs are the modern (2025) way to record the "why" — and they are particularly valuable in agent-assisted repos where AI needs the rationale, not just the code.
**Effort:** M

### [Med] Template-scrub story is undefined
**Where:** ~50 files reference `CopilotBlazorTemplate`; `src/CopilotBlazorTemplate.Web/CopilotBlazorTemplate.Web.csproj` line 7 hard-codes `UserSecretsId=aspnet-CopilotBlazorTemplate_Web-962658d4-5019-4545-bd46-e76e0b54d305`; README has hard-coded `github.com/rquintino/copilot-blazor-template` badge URL; AGENTS.md and SeedData embed `@template.local` defaults (acceptable, but undocumented).
**Current:** No `template.json` (for `dotnet new` packaging), no rename script, no documented "find/replace these N strings" recipe in README.
**Recommended:** Pick one of (a) ship a `dotnet new` template under `.template.config/template.json` with `sourceName` set so the namespace is renamed automatically on instantiation, or (b) add a `scripts/rename-template.sh` that performs `git mv` + `find/replace` for `CopilotBlazorTemplate`, regenerates the `UserSecretsId`, and rewrites the README badge URL, with a short "Make it yours" section in README. Also call out the seeded passwords explicitly as "rotate before any non-dev deploy".
**Why:** Real value of a template is repeatable instantiation. Today a forker has 50 places to manually edit, plus a stale UserSecretsId that will collide with other forks on the same machine.
**Effort:** M

### [Med] README missing standard meta sections
**Where:** `README.md`
**Current:** Has badge, architecture, quick-start, screenshots, tech stack. Missing: prerequisites (no SDK/Node/Playwright versions), running tests, license shield/section, contributing pointer, security pointer, "make it yours" rename steps.
**Recommended:** Add (in order) Prerequisites (`.NET 10 SDK 10.0.100+`, `Node 22+`, optional Playwright deps), Running Tests (`dotnet test` + `playwright install` note), Make It Yours (rename steps), License section with shield, Contributing pointer, Security pointer. Move "Scaffolded by …" footer above a horizontal rule above license.
**Why:** A new contributor today has to read the CI workflow to discover the Node version required. Quick wins for sub-5-minute onboarding.
**Effort:** S

### [Low] No `CHANGELOG.md` / versioning conventions
**Where:** root; `Directory.Build.props` has no `<Version>` / `<VersionPrefix>`
**Current:** Neither a CHANGELOG nor a version property anywhere. README footer notes `plan-dotnet-app v1.3.0` but the template itself has no version.
**Recommended:** Adopt Keep-a-Changelog format in `CHANGELOG.md` and add `<VersionPrefix>0.1.0</VersionPrefix>` to `Directory.Build.props`. Optionally adopt Conventional Commits (already partially used: `fix:`, `perf:`, `feat:`) and document in CONTRIBUTING.md. Consider `MinVer` or `Nerdbank.GitVersioning` if you ever produce build artifacts.
**Why:** Forkers want to know what changed between template versions when rebasing onto upstream. Low cost.
**Effort:** S

### [Low] `build_test_summary.py` has shebang but no executable bit
**Where:** `.github/scripts/build_test_summary.py` (git mode 100644)
**Current:** Has `#!/usr/bin/env python3` but mode is 100644 in git. Other scripts (`setup-dev.sh`, `record-demo.mjs`, `format-csharp.sh`) are correctly 100755.
**Recommended:** `git update-index --chmod=+x .github/scripts/build_test_summary.py`. If only ever invoked via `python3 …` in CI, drop the shebang and remove confusion.
**Why:** Consistency; allows running ad-hoc as `./.github/scripts/build_test_summary.py`.
**Effort:** S

### [Low] `setup-dev.sh` performs disallowed global installs and skips supply-chain guardrails
**Where:** `scripts/setup-dev.sh` lines 5, 8
**Current:** `dotnet tool install --global dotnet-ef` and `npm install -g @playwright/cli@latest --minimum-release-age=0`. The npm line explicitly bypasses the cool-down, and the `playwright-cli install --skills` flag is non-standard.
**Recommended:** Install `dotnet-ef` as a *local* tool via `dotnet new tool-manifest` + `dotnet tool install dotnet-ef` (creates `.config/dotnet-tools.json`, restorable per-clone). Use `npx playwright@<pinned-version>` instead of a global install. Remove the `--minimum-release-age=0` override or document why it's required. Delete the `playwright-cli install --skills` line (not a real flag).
**Why:** Local tool manifests are the .NET-standard pattern since .NET Core 3.1; they avoid host-state pollution and keep CI/dev parity. Globals also violate the user's stated CLAUDE.md policy ("NEVER install packages globally").
**Effort:** S

### [Low] `appsettings.json` ConnectionString writes the SQLite file inside a `Data/` folder at runtime cwd
**Where:** `src/CopilotBlazorTemplate.Web/appsettings.json` line 3
**Current:** `DataSource=Data/app.db;Cache=Shared` — relative path, creates `Data/app.db` next to the running binary (or worse, next to whatever the working directory is when `dotnet run` is invoked from the repo root).
**Recommended:** Either use `ContentRootPath`-based prefixing in `Program.cs`, or use `%LOCALAPPDATA%`-style platform path, or add `Data/` and `*.db` to `.gitignore` (already covered) and document explicitly in README that the DB is created in the working directory. As a template default, consider `DataSource=app.db` at content root for simplicity. Tangential to repo org but affects "does it work after git clone".
**Why:** New users running `dotnet run --project src/CopilotBlazorTemplate.Web` from the repo root vs from inside the project end up with the DB in different places. Minor papercut.
**Effort:** S

### [Low] `.slnx` solution folders don't surface `docs/`, `build/`, or root meta files
**Where:** `CopilotBlazorTemplate.slnx`
**Current:** Only `src/` and `tests/` folders with their csproj children. Root files (`Directory.Build.props`, `global.json`, `README.md`, `AGENTS.md`) and `docs/` are invisible in IDE solution view.
**Recommended:** Add a top-level `<Folder Name="/Solution Items/">` with `<File Path="README.md" />`, `<File Path="AGENTS.md" />`, `<File Path="Directory.Build.props" />`, `<File Path="Directory.Packages.props" />` (once added), `<File Path="global.json" />`, `<File Path=".editorconfig" />`. Visual Studio and Rider both honor this.
**Why:** Discoverability inside the IDE — contributors editing in VS don't realize `Directory.Build.props` exists.
**Effort:** S

### [Low] `src/CopilotBlazorTemplate.Core` namespace allows broad imports — folder structure should anticipate growth
**Where:** `src/CopilotBlazorTemplate.Core/{Data,Entities,Migrations}`
**Current:** Three folders only. No `Abstractions/`, `Services/`, `DTOs/`, or `Configuration/`. Fine for "minimal" but the README invites users to "Add services" without saying where.
**Recommended:** Either (a) ship empty placeholder folders with a `.gitkeep` + comment, or (b) extend AGENTS.md "How to Extend" to spell out where new layers live (e.g., "domain services in `Core/Services`, EF configurations in `Core/Data/Configurations`"). Don't pre-create folders nobody will use — option (b) is preferred.
**Why:** Template clarity. Without guidance, two forkers will create `Services/` vs `services/` vs nothing at all.
**Effort:** S

### [Low] No `FUNDING.yml` / no `.github/CODEOWNERS`
**Where:** `.github/`
**Current:** Neither exists.
**Recommended:** Add a stub `.github/CODEOWNERS` mapping `*` to the maintainer; this auto-assigns PR reviewers and is a 2-line file. `FUNDING.yml` is optional — only add if accepting sponsorship.
**Why:** CODEOWNERS is a low-effort win that improves PR triage even on a solo-maintainer template.
**Effort:** S

## Suggested target structure

```
copilot-blazor-template/
├── .config/
│   └── dotnet-tools.json                 # NEW: local tool manifest (dotnet-ef)
├── .editorconfig                         # NEW
├── .gitattributes                        # NEW
├── .github/
│   ├── CODEOWNERS                        # NEW
│   ├── ISSUE_TEMPLATE/                   # NEW
│   │   ├── bug_report.yml
│   │   └── feature_request.yml
│   ├── PULL_REQUEST_TEMPLATE.md          # NEW
│   ├── SECURITY.md                       # NEW (or root)
│   ├── agents/                           # (kept; owned by other audit)
│   ├── dependabot.yml
│   ├── hooks/
│   ├── instructions/
│   ├── scripts/
│   ├── skills/
│   └── workflows/
├── .gitignore                            # MODIFIED: remove packages.lock.json
├── .npmrc
├── .template.config/                     # NEW (optional): for `dotnet new` packaging
│   └── template.json
├── AGENTS.md
├── CHANGELOG.md                          # NEW
├── CONTRIBUTING.md                       # NEW
├── CopilotBlazorTemplate.slnx            # MODIFIED: add /Solution Items/
├── Directory.Build.props                 # MODIFIED: add <VersionPrefix>
├── Directory.Packages.props              # NEW: Central Package Management
├── LICENSE                               # NEW
├── README.md                             # MODIFIED: add prereqs/license/contributing/scrub steps
├── docs/
│   ├── README.md                         # NEW: index
│   ├── adr/                              # NEW
│   │   └── 0001-record-architecture-decisions.md
│   ├── architecture/                     # NEW
│   │   └── overview.md
│   ├── demo/
│   └── screenshots/
├── global.json
├── scripts/
│   ├── record-demo.mjs
│   ├── rename-template.sh                # NEW (if not using dotnet new)
│   └── setup-dev.sh                      # MODIFIED: drop global installs
├── src/
│   ├── CopilotBlazorTemplate.Core/
│   │   ├── CopilotBlazorTemplate.Core.csproj   # MODIFIED: no Version= attrs
│   │   ├── Data/
│   │   ├── Entities/
│   │   ├── Migrations/
│   │   └── packages.lock.json            # NEW (now committed)
│   └── CopilotBlazorTemplate.Web/
│       ├── CopilotBlazorTemplate.Web.csproj    # MODIFIED
│       └── packages.lock.json            # NEW (now committed)
├── tasks/
│   └── README.md                         # NEW: explain current/ vs backlog/  (or remove tasks/)
└── tests/
    ├── CopilotBlazorTemplate.E2ETests/
    │   └── packages.lock.json            # NEW (now committed)
    └── CopilotBlazorTemplate.UnitTests/
        └── packages.lock.json            # NEW (now committed)
```

## Missing standard files (checklist)
- [ ] LICENSE
- [ ] CONTRIBUTING.md
- [ ] SECURITY.md
- [ ] CODE_OF_CONDUCT.md (optional but recommended for public repos)
- [ ] CHANGELOG.md
- [ ] .editorconfig
- [ ] .gitattributes
- [ ] Directory.Packages.props (Central Package Management)
- [ ] .github/PULL_REQUEST_TEMPLATE.md
- [ ] .github/ISSUE_TEMPLATE/bug_report.yml
- [ ] .github/ISSUE_TEMPLATE/feature_request.yml
- [ ] .github/ISSUE_TEMPLATE/config.yml
- [ ] .github/CODEOWNERS
- [ ] .github/FUNDING.yml (optional)
- [ ] .config/dotnet-tools.json (local tool manifest for dotnet-ef)
- [ ] .template.config/template.json (if shipping as `dotnet new` template)
- [ ] docs/README.md (index)
- [ ] docs/architecture/overview.md
- [ ] docs/adr/0001-record-architecture-decisions.md
- [ ] tasks/README.md (or delete the empty tasks/ tree)
- [ ] packages.lock.json committed in all four projects (already generated, just untrack from .gitignore)

## Quick wins (top 5)
1. **Add LICENSE (MIT).** Single file, unblocks legal use by forkers, surfaces in GitHub sidebar. ~2 min.
2. **Add `Directory.Packages.props` with CPM enabled** and strip versions from the four csproj files. ~15 min, eliminates duplication of `10.0.8` across three projects.
3. **Remove `packages.lock.json` from `.gitignore` and commit the four existing lockfiles**, then add `--locked-mode` to the `dotnet restore` step in `.github/workflows/ci.yml`. ~5 min, makes restores reproducible.
4. **Add a root `.editorconfig`** (start with `dotnet new editorconfig` output, tweak for 2-space JSON/YAML/MD). ~10 min, immediately makes `dotnet format` and IDE behavior consistent.
5. **Add `.github/PULL_REQUEST_TEMPLATE.md` + `SECURITY.md` + `CONTRIBUTING.md` stubs** (each can be <30 lines). ~20 min total, addresses three GitHub repo-health flags at once.
