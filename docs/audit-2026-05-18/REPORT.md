# Repository Audit — Consolidated Report

**Date:** 2026-05-18
**Scope:** Full review of the `copilot-blazor-template` repo against modern (late-2025 / early-2026) Blazor + .NET 10 best practices.
**Method:** Five parallel theme sub-agents (code quality, repository organization, security, automated testing, agentic development) each produced an independent findings file. Themes were briefed to stay in their lane; cross-cutting items still surfaced naturally and are consolidated below.

**Per-theme findings (read these for full detail):**

1. [`01-code-quality.md`](01-code-quality.md)
2. [`02-repository-organization.md`](02-repository-organization.md)
3. [`03-security.md`](03-security.md)
4. [`04-automated-testing.md`](04-automated-testing.md)
5. [`05-agentic-development.md`](05-agentic-development.md)

---

## Executive summary

This repo is a small, well-organised .NET 10 Blazor Web App starter with a credibly modern stack: `.slnx` solution, `NuGetAudit=all`, lockfile restore, Dependabot, sticky-comment CI summary, recent E2E perf work (shared singleton + storage state), `IdentityRevalidatingAuthenticationStateProvider`, and a thoughtful agentic surface (`AGENTS.md`, path-scoped Copilot instructions, skills, hooks). The bones are right.

It is **not yet production-quality and not yet "template-quality"**. Five themes converge on the same root cause: **the project sets weaker defaults than what it asks downstream forks to inherit**. There is no `.editorconfig`, no `TreatWarningsAsErrors`, no central package management, no committed lockfiles (despite generating them), no LICENSE, no security headers, no account lockout, no Blazor circuit limits, no bUnit, no `WebApplicationFactory` tests, no working hook, no real `.mcp.json`, several agentic-config files written against invented schemas, and a `copilot-setup-steps.yml` that installs nonexistent packages with `|| true`.

The good news: nearly every gap is small in code terms. Roughly **2-3 focused days of work** would lift this from "scaffold" to "opinionated production-ready template" without changing the developer experience.

**Verdict by theme:**

| Theme | Verdict | Critical issues | Quick wins available |
|---|---|---|---|
| Code Quality | Adequate scaffold; misses .NET 10 defaults | 3 High | 5 |
| Repo Organization | Right shape, missing table-stakes | 4 High | 5 |
| Security | Demo-grade — must harden before any deploy | 5 High | 5 |
| Automated Testing | Inverted pyramid; middle layer empty | 4 High | 5 |
| Agentic Development | Strong intent, broken plumbing | 4 High | 5 |

---

## Cross-cutting themes (raised by ≥2 audits)

These are the highest-leverage fixes — one change resolves findings in multiple audit files.

### CC-1. Missing `.editorconfig` + no enforcement
Raised by: Code Quality (High), Repo Org (High), Agentic Dev (Low — re: CI format check).
A root `.editorconfig` + `Directory.Build.props` props (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `AnalysisMode=All`) + a `dotnet format --verify-no-changes` CI step closes all three at once.

### CC-2. Central Package Management
Raised by: Code Quality (Med), Repo Org (High).
`Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and `<PackageVersion>` entries; strip `Version=` from the four csproj files. Eliminates `10.0.8` duplication across three projects.

### CC-3. Lockfiles generated but gitignored
Raised by: Repo Org (High). Also implied by Security (supply-chain section) and Testing (deterministic restore).
Remove `packages.lock.json` from `.gitignore`, commit the four lockfiles, add `--locked-mode` to `dotnet restore` in CI.

### CC-4. Seeded credentials & auto-migration unconditional in all envs
Raised by: Code Quality (High — startup hygiene), Security (High — credential exposure + DDL at runtime).
Gate `SeedData.InitializeAsync` + `Database.Migrate()` on `IsDevelopment()` (or an explicit config flag); switch to `MigrateAsync` with logging/try-catch; remove the credential hint from `Login.razor` in non-Dev; bind seed passwords to `IOptions` from user-secrets/env.

### CC-5. `DbContext` lifetime + N+1 + missing `AsNoTracking`
Raised by: Code Quality (High `IDbContextFactory`, Med `Admin.razor` N+1), Testing (Med — `Admin.razor` is also untested).
Switch to `AddDbContextFactory<AppDbContext>`, refactor `Admin.razor` to a single projection with `AsNoTracking()`, and add bUnit + integration tests for the role-listing path.

### CC-6. Render-mode strategy is implicit, no `PersistentComponentState`, no `ErrorBoundary`
Raised by: Code Quality (Med ×3), Agentic Dev (Med — should be documented in `AGENTS.md` so agents stop "optimising" away `forceLoad`).
Decide per-page `@rendermode`, add `[StreamRendering]` to async pages, wrap `@Body` in `<ErrorBoundary>`, document the convention in `AGENTS.md`.

### CC-7. Skill/instruction docs drift from real code
Raised by: Testing (Med), Agentic Dev (High — wrong schema + wrong content).
The Playwright skill and `.github/instructions/playwright-tests.instructions.md` both describe `WebApplicationFactory<Program>` and CSS selectors that the fixture doesn't use. Copilot will generate broken tests from these. Rewrite both to match the singleton fixture, `NewAdminContextAsync`/`NewUserContextAsync` helpers, semantic locators, and storage-state caching. Add YAML frontmatter to `SKILL.md` files (Anthropic Agent Skills spec).

### CC-8. No standard repo meta files
Raised by: Repo Org (High LICENSE, Med SECURITY/CONTRIBUTING/PR templates), Security (Med — no `SECURITY.md` is itself a security signal), Agentic Dev (Low — README should surface agent story).
Add `LICENSE` (MIT), `SECURITY.md`, `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/CODEOWNERS`. Each is <30 lines.

### CC-9. Dependabot scope incomplete
Raised by: Security (Med), Agentic Dev (Low).
Add `github-actions` ecosystem; add grouping for `Microsoft.*`/`System.*`, `xunit*`, `playwright*` to cut PR noise.

### CC-10. CI hardening: SHA-pinning + CodeQL + format check + coverage
Raised by: Security (Med ×2 — pin actions, add SAST), Testing (Med — coverage gate), Agentic Dev (Low — `dotnet format --verify-no-changes`).
One PR can pin all actions to SHAs (Dependabot maintains), add `codeql.yml`, add `dotnet format --verify-no-changes` step, add `--collect:"XPlat Code Coverage"` + ReportGenerator + a coverage line in the sticky PR comment.

---

## Sprint 0 — top 10 quick wins (≤1 day total)

Combined and ranked across all five theme audits. Each is <30 minutes unless noted.

1. **Add `LICENSE` (MIT).** Unblocks legitimate forking. (Repo Org)
2. **Add `.editorconfig` + bump `Directory.Build.props`** (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `AnalysisMode=All`, `ManagePackageVersionsCentrally=true`) **+ create `Directory.Packages.props`.** (Code Quality + Repo Org, CC-1, CC-2)
3. **Commit lockfiles** (remove from `.gitignore`) and add `--locked-mode` to CI restore. (Repo Org, CC-3)
4. **Gate `SeedData` + credential hint on `Login.razor` behind `IsDevelopment()`**; switch `Database.Migrate()` to async + logged + Dev-only. (Code Quality + Security, CC-4)
5. **Flip `lockoutOnFailure: true`** and configure `Password.RequiredLength = 12`, `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15min` in `Program.cs`. (Security)
6. **Add a security-headers middleware** (`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, CSP `frame-ancestors 'self'` as a starting CSP). (Security)
7. **Configure circuit/hub limits** (`MaximumReceiveMessageSize`, `DisconnectedCircuitMaxRetained`, `ClientTimeoutInterval`). (Security)
8. **Sync `playwright-tests.instructions.md` + `playwright-e2e/SKILL.md` with the real fixture**, and **add YAML frontmatter to both `SKILL.md` files** per the Agent Skills spec. (Testing + Agentic Dev, CC-7)
9. **Fix the format-on-edit hook** — move to `.claude/settings.json` with `PostToolUse` matcher, scope script to the edited file only. (Agentic Dev)
10. **Clean up `copilot-setup-steps.yml`** — drop fictional `@playwright/cli`/`@blazorblueprint/mcp`, add NuGet/npm caching, remove `|| true` on critical steps. (Agentic Dev)

After these 10, the repo flips from "demo template" to "opinionated 2026-era starter".

---

## Sprint 1 — high-severity remaining work (1-2 days)

Items that need a bit more thought than Sprint 0 but are clearly worth doing.

| # | Item | Theme | Effort |
|---|---|---|---|
| 11 | Switch `AddDbContext` → `AddDbContextFactory<AppDbContext>`; refactor `Admin.razor` (kills N+1 + tracking together). | Code Quality (CC-5) | S |
| 12 | Add bUnit + 5 starter component tests (`Dashboard`, `Admin`, `AuthenticatedLayout`, `NotFound`, sidebar role gating). | Testing | M |
| 13 | Add `WebApplicationFactory<Program>` integration test project (~15-20 tests for auth redirects, antiforgery, role gating). | Testing | M |
| 14 | Swap `UseInMemoryDatabase` → SQLite in-memory in `SeedDataTests`; add one migration apply test. | Testing | S |
| 15 | Add a real, pinned `.mcp.json` (`filesystem` scoped to workspace, `sqlite` scoped to `app.db`, `playwright`). | Agentic Dev | M |
| 16 | Fix `.github/agents/*.agent.md` — either promote to `.claude/agents/*.md` with frontmatter or rename to `.github/chatmodes/*.chatmode.md`. | Agentic Dev | S |
| 17 | Pin all GitHub Actions to commit SHAs; add `github-actions` to Dependabot; add `codeql.yml`. | Security + Agentic Dev (CC-9, CC-10) | S |
| 18 | Add `<ErrorBoundary>` to layouts; add `@attribute [StreamRendering]` to `Dashboard.razor` / `Admin.razor`; decide explicit per-page `@rendermode`. | Code Quality (CC-6) | M |
| 19 | Add `AddAuthorizationBuilder().SetFallbackPolicy(RequireAuthenticatedUser)` + `[AllowAnonymous]` on the public surface. | Security | M |
| 20 | Decide Bootstrap vs custom theme for Identity Account pages; do one pass to make them consistent. | Code Quality | L |
| 21 | Enable Playwright trace-on-failure + upload `TestResults/traces/` as CI artifact. | Testing | S |
| 22 | Add `coverlet` collection in CI + ReportGenerator + coverage line in sticky PR comment. | Testing (CC-10) | M |
| 23 | Add `SECURITY.md`, `CONTRIBUTING.md`, `.github/PULL_REQUEST_TEMPLATE.md`, `.github/CODEOWNERS`. | Repo Org (CC-8) | S |
| 24 | Append "Instructions & Skills", "Pre-flight", "Definition of Done" sections to `AGENTS.md`; symlink `CLAUDE.md` and `.cursorrules` to it. | Agentic Dev | S |
| 25 | Define template-scrub story: ship a `dotnet new` `template.json`, or a `scripts/rename-template.sh` for the ~50 `CopilotBlazorTemplate` strings + `UserSecretsId` GUID + README badge URL. | Repo Org | M |

---

## Sprint 2 — medium-severity backlog (later)

Worth doing, no rush. Grouped roughly by theme; see per-theme files for details.

**Code Quality:** `AddIdentityCore` + `AddIdentityCookies` migration; `[PersonalData]`/`[MaxLength]` on `ApplicationUser.DisplayName`; flow `CancellationToken` through `OnInitializedAsync`; convert `SeedData` to a `DatabaseSeeder` hosted service with strongly-typed deps + `IdentityResult` checks; resolve the `BlazorDisableThrowNavigationException` vs `IdentityRedirectManager.RedirectTo` mismatch; add `OpenTelemetry` + `/healthz`; add `InternalsVisibleTo` for UnitTests.

**Repo Organization:** `.gitattributes`; `CHANGELOG.md` + `<VersionPrefix>`; `docs/` reorganisation with `docs/adr/0001-record-architecture-decisions.md`; local tool manifest (`.config/dotnet-tools.json`) for `dotnet-ef`; tasks workflow doc or removal; `.slnx` Solution Items folder.

**Security:** `__Host-`-prefixed cookie + explicit cookie config pinning; document `RequireConfirmedAccount`/`IdentityNoOpEmailSender` conflict; MFA enforcement policy for `Admin`; tighten `IdentityRedirectManager` open-redirect guard to also block `//evil.com`; sanitise `returnUrl` in `RedirectToLogin.razor`; SQLite file permissions guidance; document `BlazorDisableThrowNavigationException` semantics.

**Testing:** xUnit v2 → v3 migration; pick an assertion library (Shouldly or AwesomeAssertions); add `xunit.runner.json` to UnitTests project; convert duplicate `[Fact]`s to `[Theory]`; add `Deque.AxeCore.Playwright` a11y smoke per top-level page; split CI into `unit-tests` + `e2e-tests` jobs with Playwright browser cache; document `TimeProvider` + `FakeTimeProvider` pattern.

**Agentic Dev:** Reframe or delete `playwright-e2e` skill (currently restates instructions); de-duplicate `scripts/record-demo.mjs` vs `screenshots-demo/capture.js`; add `.github/copilot-instructions.md` (10-line pointer to AGENTS.md); fix `efcore.instructions.md` glob to brace form; fix CI-only hardcoded path in `screenshots-demo/SKILL.md`; add subtree-level `AGENTS.md` files (`tests/AGENTS.md`, `src/CopilotBlazorTemplate.Web/Components/AGENTS.md`); add `docs/agentic.md`.

---

## Strengths to preserve

These came up across multiple audits as work worth keeping intact through any refactor.

- `.slnx` solution + clean src/tests split + one-way Web→Core reference.
- `NuGetAudit=all`, `RestorePackagesWithLockFile=true`, Dependabot for nuget+npm, `.npmrc` `min-release-age=15`.
- `IdentityRevalidatingAuthenticationStateProvider`, antiforgery wired correctly (including the recent logout hardening), passkey endpoints with explicit antiforgery, `TypedResults.LocalRedirect` on logout, `.RequireAuthorization()` at the Manage group, 100-passkey cap.
- HSTS + HTTPS redirect + `UseExceptionHandler("/Error")`; no `MarkupString` or raw SQL anywhere; status cookie locked down (`SameSite=Strict`, 5s `MaxAge`).
- The `PlaywrightFixture` singleton + `Lazy<Task<SharedState>>` + cached storage-state pattern (commit `da95121`) — non-obvious xUnit-v2 trick that gives class-level parallelism without `[Collection]` serialization; tight 2s default timeouts; random-port subprocess startup.
- CI sticky `<!-- ci-test-summary -->` comment with TRX-derived markdown — the right pattern for agent-readable PR feedback.
- `AGENTS.md` is short, accurate, command-table-first; path-scoped Copilot instruction files use the correct `applyTo:` frontmatter; `copilot-setup-steps.yml` correctly uses the special-named workflow convention for the Copilot Coding Agent.
- File-scoped namespaces, nullable + ImplicitUsings on, primary-constructor DI, `sealed` applied consistently to internal scaffold types, collection expressions, `MapStaticAssets`, `BlazorDisableThrowNavigationException` enabled (with one footnote — see Code Quality finding on `IdentityRedirectManager`).

---

## How to use this report

- **Today / this PR:** Sprint 0 (items 1-10). One commit per item or one bundled "Template hardening" PR.
- **This week:** Sprint 1 (items 11-25). These shift the architecture toward the recommended testing pyramid and lock in the agentic plumbing.
- **Backlog:** Sprint 2 items go into `tasks/backlog/` as individual issues or task files when picked up.
- **For agents:** any change above should keep AGENTS.md and the instruction files in sync (CC-7 lesson — drift is the failure mode).

Per-theme files have file:line citations, code snippets, and references for every item summarised above. Use them when implementing.
