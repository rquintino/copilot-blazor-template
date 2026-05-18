# CI & supply-chain hardening

## Goal
Harden the GitHub Actions surface: pin every action to a commit SHA, add `github-actions` to Dependabot with sensible grouping, add CodeQL static analysis, add a `dotnet format --verify-no-changes` gate, collect code coverage via `coverlet` + ReportGenerator, post a coverage delta in the sticky PR comment, and split CI into separate `unit-tests` and `e2e-tests` jobs with a Playwright browser cache.

## Scope
- Replace every `uses: <org>/<action>@vN` reference in `.github/workflows/*.yml` with `uses: <org>/<action>@<full-40-char-sha> # vN.M.P`.
- Extend `.github/dependabot.yml` with a `github-actions` ecosystem block plus grouping (`microsoft:` for `Microsoft.*`/`System.*`, `test-deps:` for `xunit*`/`playwright*`/`coverlet*`, `playwright:` for npm `playwright`/`@playwright/*`).
- Add `.github/workflows/codeql.yml` (GitHub-provided starter for `csharp` + `javascript`).
- Add a `dotnet format --verify-no-changes --no-restore` step to `ci.yml` (run after restore, before tests so failures surface fast).
- Add `--collect:"XPlat Code Coverage"` to the `dotnet test` calls, install ReportGenerator as a local tool (`.config/dotnet-tools.json`), generate a Cobertura summary, upload as artifact, and extend `.github/scripts/build_test_summary.py` to inject a coverage line into the sticky `<!-- ci-test-summary -->` comment.
- Split `ci.yml` into two jobs: `unit-tests` (UnitTests + IntegrationTests if task 06 has shipped) and `e2e-tests` (Playwright). Cache `~/.cache/ms-playwright` keyed on the Playwright version pulled from `tests/CopilotBlazorTemplate.E2ETests/CopilotBlazorTemplate.E2ETests.csproj`. Cache `~/.nuget/packages` keyed on lockfile hashes.
- Add `--locked-mode` to the CI `dotnet restore` step (depends on task 02 having committed lockfiles).

Out of scope:
- Editing the bootstrap workflow `copilot-setup-steps.yml` (owned by task 09).
- Adding secret scanning (enable in repo settings, not in this PR).
- Rate-limit / CSP iteration in the app itself (owned by task 03).

## Edit zone
- `.github/workflows/ci.yml` (rewrite into the split-job shape; this task owns the file)
- `.github/workflows/codeql.yml` (new)
- `.github/dependabot.yml` (additive — add a third ecosystem block + groups under existing ones)
- `.github/scripts/build_test_summary.py` (extend — add a `coverage` parser + line)
- `.config/dotnet-tools.json` (new — ReportGenerator)

## Independence guarantee
- This task is the canonical owner of `.github/workflows/ci.yml`. Task 09 owns `copilot-setup-steps.yml`. No overlap.
- If task 02 (lockfiles committed) has not shipped, the `--locked-mode` flag will fail CI. Wrap the lockfile-mode change behind a conditional check (or skip the `--locked-mode` flag and leave a TODO) so this task can still ship before task 02. Once task 02 lands, a follow-up adds `--locked-mode`.
- If task 06 (IntegrationTests) has not shipped, the `unit-tests` job runs only `CopilotBlazorTemplate.UnitTests`. Once task 06 lands, the job picks up the new project automatically via a `dotnet test` solution-wide call or a `--filter` that excludes `E2ETests`.
- If task 07 has not yet enabled trace-on-failure or migrated to xUnit v3, the `e2e-tests` job still works — only the artifact-upload step is conditional on `TestResults/traces/` existing.
- Dependabot's new `github-actions` ecosystem will start opening PRs after merge; those PRs will keep the SHAs current.
- CodeQL is a separate workflow file; it does not touch `ci.yml` and runs on its own schedule.
- `.config/dotnet-tools.json` is new; if a future task (e.g. `dotnet-ef` as a local tool) wants to add to it, they append to the existing manifest.
- The `dotnet format --verify-no-changes` step depends on a `.editorconfig` existing (task 02). Without one, the step is a no-op rather than a failure — safe to ship either order.

This task may sit in `backlog/` for weeks. By the time it is picked up the workflow files, Dependabot config, and tool manifest may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before rewriting `ci.yml`, read it — actions may already be SHA-pinned, coverage may already be collected, jobs may already be split. Additively reconcile (add missing settings, leave existing ones intact). Same for `dependabot.yml` (`github-actions` ecosystem may already be added) and `.config/dotnet-tools.json` (it may already exist).
- **File moved/renamed.** Workflow files may have been renamed (e.g. `ci.yml` split into `test.yml` + `lint.yml`); the test summary script may live at a different path. Locate via `git ls-files .github/`.
- **Prerequisite work already done.** Quick checks: are all `uses:` already SHA-pinned (`grep -rE 'uses:.*@[a-f0-9]{40}' .github/workflows/`)? Is `codeql.yml` already present? Skip steps whose intent is already in place.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if jobs are already split and caching is configured, the goal is met; just add what's missing (e.g. coverage line).
- If intent conflicts (e.g. someone picked a different test runner image, or grouped Dependabot differently), surface in the PR description; don't silently reshuffle.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read every file under `.github/workflows/`, `.github/dependabot.yml`, `.github/scripts/build_test_summary.py`, and `.config/dotnet-tools.json` (if it exists). The snippets below describe the *intent* of each change — apply it to whatever shape the files actually have today. If a workflow has been renamed, work against the current name. Skip whatever is already in place; note in the PR description.
2. **Pin actions to SHAs.** For each `uses:` line in `.github/workflows/*.yml` that is not already SHA-pinned, look up the commit SHA for the target tag:
   - `gh api repos/<org>/<repo>/git/ref/tags/<tag>` returns the SHA (resolve annotated tag if necessary).
   - Replace `actions/checkout@v4` with `actions/checkout@<sha> # v4.x.y`. Repeat for `actions/setup-dotnet@v4`, `actions/setup-node@v4`, `actions/github-script@v7`, `actions/upload-artifact@v4`, `actions/cache@v4`.
3. **Dependabot:** edit `.github/dependabot.yml`. If the `github-actions` ecosystem block is absent, append a new entry under `updates:`:
   ```yaml
   - package-ecosystem: "github-actions"
     directory: "/"
     schedule: { interval: "weekly" }
     open-pull-requests-limit: 5
   ```
   Add `groups:` blocks to the existing `nuget` and `npm` entries:
   ```yaml
   - package-ecosystem: "nuget"
     directory: "/"
     schedule: { interval: "weekly" }
     open-pull-requests-limit: 5
     groups:
       microsoft:    { patterns: ["Microsoft.*", "System.*"] }
       test-deps:    { patterns: ["xunit*", "Microsoft.Playwright*", "coverlet*", "bunit*", "Shouldly", "Deque.*"] }
   - package-ecosystem: "npm"
     directory: "/"
     schedule: { interval: "weekly" }
     groups:
       playwright:   { patterns: ["playwright", "@playwright/*"] }
   ```
4. **CodeQL:** if `.github/workflows/codeql.yml` does not already exist, create it from the starter template — `language: ["csharp", "javascript"]`, on `push` to `main`, `pull_request` against `main`, and a weekly `schedule`. Pin `github/codeql-action/*` to SHAs too.
5. **Local tool manifest:** if `.config/dotnet-tools.json` does not already exist, run `dotnet new tool-manifest`. If the manifest already exists, additively add `dotnet-reportgenerator-globaltool` to it via `dotnet tool install dotnet-reportgenerator-globaltool` (which appends to the manifest). Commit.
6. **Rewrite `ci.yml`** into the two-job shape (if it has not already been split). Sketch:
   ```yaml
   jobs:
     unit-tests:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@<sha>
         - uses: actions/setup-dotnet@<sha>
           with: { dotnet-version: '10.x', cache: true, cache-dependency-path: '**/packages.lock.json' }
         - run: dotnet restore --locked-mode      # only after task 02 lands
         - run: dotnet format --verify-no-changes --no-restore
         - run: dotnet test tests/CopilotBlazorTemplate.UnitTests --no-restore --collect:"XPlat Code Coverage" --results-directory TestResults
         # if task 06 has shipped, also test IntegrationTests
         - run: dotnet tool restore
         - run: dotnet reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:TestResults/coverage -reporttypes:"Html;TextSummary;MarkdownSummary"
         - uses: actions/upload-artifact@<sha>
           with: { name: unit-coverage, path: TestResults/coverage }

     e2e-tests:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@<sha>
         - uses: actions/setup-dotnet@<sha>
           with: { dotnet-version: '10.x', cache: true, cache-dependency-path: '**/packages.lock.json' }
         - uses: actions/setup-node@<sha>
           with: { node-version: '22', cache: 'npm' }
         - run: dotnet restore --locked-mode
         - run: dotnet build --no-restore tests/CopilotBlazorTemplate.E2ETests
         - id: pw
           run: echo "version=$(grep -oP 'Microsoft\.Playwright"\s+Version="\K[^"]+' tests/CopilotBlazorTemplate.E2ETests/CopilotBlazorTemplate.E2ETests.csproj)" >> $GITHUB_OUTPUT
         - uses: actions/cache@<sha>
           with: { path: ~/.cache/ms-playwright, key: pw-${{ steps.pw.outputs.version }} }
         - run: pwsh tests/CopilotBlazorTemplate.E2ETests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
         - run: dotnet test tests/CopilotBlazorTemplate.E2ETests --no-build --logger trx
         - uses: actions/upload-artifact@<sha>
           if: always()
           with: { name: e2e-traces, path: tests/CopilotBlazorTemplate.E2ETests/TestResults/traces, if-no-files-found: ignore }

     summary:
       needs: [unit-tests, e2e-tests]
       if: always()
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@<sha>
         - uses: actions/download-artifact@<sha>
         - run: python3 .github/scripts/build_test_summary.py
         - uses: actions/github-script@<sha>
           with: { script: <existing sticky-comment script> }
   ```
   Preserve the existing sticky-comment marker (`<!-- ci-test-summary -->`).
7. **Extend `build_test_summary.py`** (or whatever the current summary script is called) to parse the Cobertura coverage file (or the `TextSummary.txt` from ReportGenerator) and emit one line: `Coverage: 64.2% lines / 51.0% branches (Δ +1.3pp vs main)`. Compute delta by downloading the main-branch coverage artifact if available, otherwise just emit the absolute number. Preserve existing summary content additively.
8. **Run locally:** `act -j unit-tests` (if `act` is set up) or push a draft PR to validate. Confirm the sticky comment includes the new coverage line.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] Every `uses:` line in `.github/workflows/*.yml` references a 40-char SHA (grep `uses:.*@v[0-9]` returns nothing).
- [ ] Dependabot covers the three ecosystems used by this repo (NuGet, npm, GitHub Actions) and groups noisy patterns.
- [ ] A CodeQL workflow runs on PR and on a schedule.
- [ ] CI has separate jobs (or comparably-isolated steps) for unit-style tests and Playwright E2E tests.
- [ ] CI fails fast on a formatting violation (a `dotnet format --verify-no-changes` gate runs before tests).
- [ ] Coverage output is generated, summarised, and uploaded as an artifact; the sticky PR comment includes a coverage line.
- [ ] Playwright browsers are cached across runs keyed on the Playwright version pinned in the E2E csproj.
- [ ] NuGet packages are cached via `actions/setup-dotnet`'s `cache: true`.
- [ ] A local tool manifest exists at `.config/dotnet-tools.json` and lists `dotnet-reportgenerator-globaltool`.
- [ ] A PR run finishes CI within the same wall-clock budget as before (or better, due to caching).

## References
- Audit cross-cutting themes CC-9 (Dependabot scope), CC-10 (CI hardening — SHAs, CodeQL, format check, coverage): `../../../docs/audits/2026-05-18/REPORT.md`.
- Security findings "GitHub Actions are pinned by tag, not SHA", "Dependabot does not cover GitHub Actions ecosystem", "No SAST/CodeQL or secret-scanning in CI": `../../../docs/audits/2026-05-18/03-security.md`.
- Automated Testing findings "No code coverage collection or threshold", "CI runs E2E in same job as unit; no sharding; chromium-only": `../../../docs/audits/2026-05-18/04-automated-testing.md`.
- Agentic Development findings "CI does not run `dotnet format --verify-no-changes`", "Dependabot config is minimal": `../../../docs/audits/2026-05-18/05-agentic-development.md`.
- GitHub Actions hardening — <https://docs.github.com/actions/security-guides/security-hardening-for-github-actions>
- Dependabot grouping — <https://docs.github.com/code-security/dependabot/dependabot-version-updates/configuration-options-for-the-dependabot.yml-file#groups>
- ReportGenerator — <https://reportgenerator.io/>
