# Build & quality foundations

## Goal
Establish the build-level quality floor the template asks downstream forks to inherit: a root `.editorconfig`, analyzer + warnings-as-errors props in `Directory.Build.props`, Central Package Management via `Directory.Packages.props`, committed NuGet lockfiles, and a `.gitattributes` for cross-platform line-ending hygiene.

## Scope
- Add `.editorconfig` at the repo root (baseline from `dotnet new editorconfig` + 2-space indent for JSON/YAML/MD).
- Extend `Directory.Build.props` with `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `AnalysisMode=All`, `ManagePackageVersionsCentrally=true`, and `<VersionPrefix>0.1.0</VersionPrefix>`.
- Add `Directory.Packages.props` listing every `<PackageVersion>` the four csproj files currently declare; strip `Version=` attributes from those csproj `<PackageReference>` elements.
- Remove `packages.lock.json` from `.gitignore`; commit the four existing lockfiles (Core, Web, UnitTests, E2ETests).
- Add `.gitattributes` enforcing `* text=auto eol=lf` for code, `eol=crlf` for `.cmd`/`.bat`, `binary` for images / `.webm` / `.db`.

Out of scope:
- Adding the CI step that runs `dotnet format --verify-no-changes` or `--locked-mode` to restore (owned by task 08).
- Adding `<InternalsVisibleTo>` for UnitTests (defer to a follow-up).
- Local tool manifest `.config/dotnet-tools.json` (defer).

## Edit zone
- `.editorconfig` (new)
- `.gitattributes` (new)
- `Directory.Build.props` (modify — additive `<PropertyGroup>` for analyzer + version props)
- `Directory.Packages.props` (new)
- `src/CopilotBlazorTemplate.Core/CopilotBlazorTemplate.Core.csproj` (strip `Version=`)
- `src/CopilotBlazorTemplate.Web/CopilotBlazorTemplate.Web.csproj` (strip `Version=`)
- `tests/CopilotBlazorTemplate.UnitTests/CopilotBlazorTemplate.UnitTests.csproj` (strip `Version=`)
- `tests/CopilotBlazorTemplate.E2ETests/CopilotBlazorTemplate.E2ETests.csproj` (strip `Version=`)
- `.gitignore` (modify — remove the two `packages.lock.json` lines under `## NuGet`)
- `src/CopilotBlazorTemplate.Core/packages.lock.json` (newly tracked — already on disk)
- `src/CopilotBlazorTemplate.Web/packages.lock.json` (newly tracked)
- `tests/CopilotBlazorTemplate.UnitTests/packages.lock.json` (newly tracked)
- `tests/CopilotBlazorTemplate.E2ETests/packages.lock.json` (newly tracked)

## Independence guarantee
- `.editorconfig` and `.gitattributes` are new files — no collision.
- `Directory.Build.props` already exists. This task adds a new `<PropertyGroup>` at the bottom; it does not restructure existing groups. If another task wants to add more props, they should likewise append a new `<PropertyGroup>`.
- `Directory.Packages.props` is the canonical owner of all `<PackageVersion>` entries. Other tasks that introduce new NuGet packages (bUnit in task 05, `Microsoft.AspNetCore.Mvc.Testing` already referenced for task 06, etc.) **add `<PackageVersion>` here** — or, if this task has not yet shipped, they declare `<PackageReference Version="…">` directly in the csproj and leave a TODO referencing this task.
- Lockfile commit is harmless to other tasks: lockfiles regenerate on every `dotnet restore` and the only behavioural change is making restore reproducible.
- Turning on `TreatWarningsAsErrors` may surface latent warnings. If the build breaks, fix the warnings as part of this task (do not weaken the prop). Existing scaffolded code is generally clean; if a specific rule is too noisy, suppress it in `.editorconfig` rather than the prop.

This task may sit in `backlog/` for weeks. By the time it is picked up the repo may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before editing `Directory.Build.props`, `.gitignore`, or any csproj, read it. If it already contains the property/setting this task adds (e.g. another task already turned on `TreatWarningsAsErrors`), leave it. If it contains an *incompatible* version of the same setting, merge the intents (e.g. another task set `AnalysisLevel=latest`; reconcile rather than overwrite). Never blindly replace.
- **File moved/renamed.** Csproj files may have been renamed (project rename) or split. Locate them by `git ls-files '*.csproj'` rather than hardcoding `src/CopilotBlazorTemplate.Core/...`. If `Directory.Packages.props` already exists, additively add missing `<PackageVersion>` entries instead of overwriting the file.
- **Prerequisite work already done.** Quick checks before each sub-step: does `.editorconfig` already exist at the root? Does `Directory.Packages.props` exist? Are the four `packages.lock.json` files already tracked (`git ls-files | grep packages.lock.json | wc -l`)? Skip whatever is already in place, and note the skips in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if `TreatWarningsAsErrors=true` is already set anywhere in `Directory.Build.props`, the goal is met; move on.
- If intent conflicts (e.g. someone disabled `EnforceCodeStyleInBuild` deliberately), surface it in the PR description; don't silently flip it back.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read every file you plan to touch before editing it. Specifically: `ls -la` the repo root for `.editorconfig`, `.gitattributes`, `Directory.Packages.props`; read `Directory.Build.props` end-to-end; read `.gitignore`; list csprojs via `git ls-files '*.csproj'`; run `git ls-files | grep packages.lock.json`. The snippets below describe the *intent* of each change — apply that intent to whatever the files currently contain. If a file referenced here no longer exists or has moved, locate the new home (e.g. `git ls-files | grep -i editorconfig`) and update accordingly.
2. **`.editorconfig`** — if missing, generate a baseline (`cd /tmp && dotnet new editorconfig`, copy to repo root) and then tune. If present, additively reconcile with this task's intent.
   Intent:
   - Root-level: `root = true`, `end_of_line = lf`, `insert_final_newline = true`, `charset = utf-8`, `trim_trailing_whitespace = true`.
   - C#/VB: 4-space indent, `csharp_new_line_before_open_brace = all`, `dotnet_sort_system_directives_first = true`.
   - JSON/YAML/MD/Razor/HTML/CSS/JS/TS and MSBuild files (csproj/slnx/props/targets): 2-space indent.
   - Suppress noisy rules with `dotnet_diagnostic.<ID>.severity = suggestion` rather than weakening `TreatWarningsAsErrors`.
3. **`Directory.Build.props`** — wherever the project anchors build-wide MSBuild props, ensure the following are present (append a new `<PropertyGroup>` rather than restructuring existing groups; if a setting is already present, leave it):
   - `TreatWarningsAsErrors=true`
   - `EnforceCodeStyleInBuild=true`
   - `AnalysisLevel=latest-recommended`
   - `AnalysisMode=All`
   - `ManagePackageVersionsCentrally=true`
   - `CentralPackageTransitivePinningEnabled=true`
   - `VersionPrefix=0.1.0`
4. **Central Package Management.** Inventory current versions across all csprojs:
   `grep -rh 'PackageReference' src tests | grep -oP 'Include="[^"]+" Version="[^"]+"'` → deduplicate `(id, version)` pairs.
   - If `Directory.Packages.props` does not exist, create it with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and one `<PackageVersion Include="…" Version="…" />` per deduplicated package.
   - If it already exists, additively merge: add only the entries it is missing.
   - Wherever the project still has `<PackageReference Include="…" Version="…">` in a csproj, strip the `Version="…"` attribute. Use grep/sed across whatever csproj files exist today, not a hardcoded four-file list.
5. **`.gitignore`** — wherever it currently ignores `packages.lock.json`, remove that pattern so lockfiles can be tracked. Keep surrounding sections intact.
6. **Lockfiles.** Run `dotnet restore` from the repo root so each project regenerates its lockfile, then `git add` every `packages.lock.json` that exists under `src/` and `tests/`.
7. **`.gitattributes`** — if missing, create it. Intent:
   - Default: `* text=auto eol=lf`
   - Windows shell scripts: `*.{cmd,bat} text eol=crlf`
   - Binaries: images, `.webm`, `.mp4`, `.webp`, `.db` marked `binary`
   - Diff drivers: `*.cs diff=csharp`, `*.razor diff=html`
8. **Build verification.** Run `dotnet restore`, `dotnet build`, `dotnet test`. If analyzer warnings now break the build (because `TreatWarningsAsErrors` is on), fix the warnings, or suppress narrowly in `.editorconfig`. Do not weaken the prop. If `dotnet restore` complains about CPM, double-check that every `<PackageReference>` had its `Version=` attribute removed.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] The repo has an `.editorconfig` and `.gitattributes` at the root (location matters because tooling looks there).
- [ ] Build-wide MSBuild props enforce: warnings-as-errors, style enforcement in build, recommended analyzers, central package management, transitive pinning, and a `VersionPrefix`. Verified by `dotnet build -getProperty:TreatWarningsAsErrors,ManagePackageVersionsCentrally,VersionPrefix` or by grepping `Directory.Build.props`.
- [ ] No `<PackageReference>` in any csproj carries a `Version=` attribute (`grep -rE 'PackageReference[^>]*Version=' src tests` returns nothing).
- [ ] `git ls-files | grep packages.lock.json` lists one entry per project (count matches the number of csprojs).
- [ ] `.gitignore` no longer ignores `packages.lock.json`.
- [ ] `dotnet restore --locked-mode` succeeds.
- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes the full suite.

## References
- Audit cross-cutting themes CC-1 (`.editorconfig` + enforcement), CC-2 (CPM), CC-3 (lockfiles): `../../../docs/audits/2026-05-18/REPORT.md`.
- Code Quality findings "No `IDbContextFactory<AppDbContext>`…", "No `.editorconfig`, no analyzer/style enforcement, no `TreatWarningsAsErrors`", "No central package management": `../../../docs/audits/2026-05-18/01-code-quality.md`.
- Repo Organization findings "No Central Package Management", "`packages.lock.json` is gitignored but also generated", "Missing `.editorconfig`", "Missing `.gitattributes`": `../../../docs/audits/2026-05-18/02-repository-organization.md`.
- Central Package Management — <https://learn.microsoft.com/nuget/consume-packages/central-package-management>
- Roslyn analyzer modes — <https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview>
