# Automated Testing Audit

## Summary
The template ships a credible but minimal two-tier test stack (xUnit unit tests + Playwright E2E) totalling ~26 test methods across 6 files. The recent E2E fixture refactor (commit `da95121`) is genuinely good engineering — a singleton in-process app + cached storage state cuts wall time dramatically while preserving class-level parallelism via `IClassFixture`. However the strategy has a glaring middle-layer gap: there is no Blazor component testing (bUnit) and no in-process integration testing (`WebApplicationFactory`), forcing every behavioral assertion into either a thin POCO unit test or a heavy Playwright run. xUnit is still on v2 (v3 is current as of 2025), assertions use raw `Assert.*` (no FluentAssertions/Shouldly), and there is no coverage gate, no flake/retry policy, no trace/video artifacts on failure, no accessibility test, and no mutation testing. The total surface tested vs. shipped (auth, role gating, EF Core mappings, migrations, render-mode transitions, antiforgery, reconnect, error boundary) is well under half.

## Strengths
- Two-project separation (Unit + E2E) is the right shape.
- `PlaywrightFixture` uses a static `Lazy<Task<SharedState>>` with `IClassFixture` so test classes parallelize without falling back to `[Collection]` serialization — a non-obvious xUnit-v2 pattern that's worth keeping.
- Storage-state reuse (`NewAdminContextAsync` / `NewUserContextAsync`) means most authorization tests skip the real login round-trip.
- Default timeouts clamped to 2s + `Assertions.SetDefaultExpectTimeout(2000)` — failures surface fast instead of hanging 30s.
- App subprocess is launched with `ASPNETCORE_URLS=http://127.0.0.1:0` and the listening URL parsed from stdout — random-port handling avoids port-conflict flakiness.
- Per-run isolated SQLite DB (`Path.GetTempPath()` + `Guid.NewGuid()`), cleanup wired to `ProcessExit`.
- Locators use semantic selectors (`GetByRole`, `GetByLabel`) over CSS in most places.
- `coverlet.collector` already referenced in both projects (groundwork for coverage).
- `Microsoft.AspNetCore.Mvc.Testing` is already a PackageReference in the E2E project (unused) — capability is there.
- CI uploads TRX results + screenshots, posts a sticky summary PR comment, and uses `continue-on-error` + a final fail-gate so both suites always report.
- `packages.lock.json` committed in both test projects (deterministic restore).

## Current state at a glance

| Layer | Framework | Test count | Key gaps |
|---|---|---|---|
| Pure unit | xUnit v2 + raw Assert | 5 (`ApplicationUserTests`=2, `SeedDataTests`=3) | No `Theory` data, no `ApplicationUser` business rules, no `AppDbContext` mapping tests, no migration test |
| Component (Blazor) | — | 0 | bUnit not referenced; no `Dashboard`, `Admin`, `AuthenticatedLayout`, `ReconnectModal`, `NotFound` component tests |
| Integration (in-process) | `Microsoft.Mvc.Testing` referenced but unused | 0 | No `WebApplicationFactory<Program>` tests for routing, antiforgery, Identity endpoints, redirect rules |
| E2E (browser) | Playwright 1.59 + xUnit v2 | 21 (`AuthTests`=6, `AuthorizationTests`=7, `HomeTests`=4, `NavigationTests`=4) | No trace-on-failure, no video, no axe-a11y, no visual baselines, no network mocking, single browser (chromium-only) |
| Coverage | `coverlet.collector` declared, never invoked | n/a | No `--collect:"XPlat Code Coverage"`, no ReportGenerator, no threshold gate |
| Mutation | — | n/a | Stryker.NET not present |

## Findings

### [High] No Blazor component tests — middle of the pyramid is empty
**Where:** `tests/CopilotBlazorTemplate.UnitTests/` (no bUnit reference)
**Current:** Every rendering assertion (sidebar visibility, role-based UI gating, dashboard layout, `NotFound` page, `ReconnectModal`) is exercised only via a full Playwright browser round-trip. Component logic (parameter binding, `EventCallback`, `CascadingParameter`, `@if`/role checks inside markup) has zero direct coverage.
**Recommended:** Add a third test project `CopilotBlazorTemplate.ComponentTests` (or fold into UnitTests) using **bUnit** (the de-facto Blazor component testing library, actively maintained, .NET 10 compatible). Use `TestContext` + `AddTestAuthorization()` to drive role-aware rendering deterministically and cheaply (<10ms per test).
```csharp
using var ctx = new TestContext();
var authCtx = ctx.AddTestAuthorization();
authCtx.SetAuthorized("admin@template.local", AuthorizationState.Authorized);
authCtx.SetRoles("Admin");
var cut = ctx.RenderComponent<AuthenticatedLayout>();
cut.Find("nav.sidebar-nav").MarkupMatches(/* expected */);
```
**Why:** Modern guidance (Microsoft's "Test Razor components" docs, .NET 10) explicitly recommends bUnit for component-level coverage. Playwright is the wrong tool to verify a conditional `@if (context.User.IsInRole("Admin"))` — it costs ~500ms+ and a subprocess where bUnit costs sub-millisecond and pinpoints the exact failing render.
**Effort:** M (add project, write ~15-25 tests covering each Razor component).

### [High] xUnit v2 is end-of-life direction; xUnit v3 is the current line
**Where:** Both csproj files — `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4
**Current:** xUnit v2.9.3 (released 2024) — last v2 line. v3 (1.x GA in 2024, multiple releases through 2025) is now the supported track and introduces per-process test isolation, a new runner that doesn't depend on VSTest, native `TestContext`, async lifetime improvements, and assembly-level fixtures.
**Recommended:** Migrate to `xunit.v3` and replace `xunit.runner.visualstudio` with `xunit.v3.runner.visualstudio`. The breaking changes are small for this codebase (mostly `IAsyncLifetime` → `IAsyncLifetime` with `ValueTask`, plus removal of `[CollectionDefinition]` quirks). The fixture's static `Lazy<Task<SharedState>>` pattern continues to work.
**Why:** Staying on v2 means missing better Microsoft Testing Platform (MTP) support, native parallelism controls, and the ecosystem's direction. Templates are reference material — they should ship the current major.
**Effort:** S–M (mostly package swap + handful of namespace/signature updates).

### [High] No `WebApplicationFactory` integration tests despite the package being referenced
**Where:** `tests/CopilotBlazorTemplate.E2ETests/CopilotBlazorTemplate.E2ETests.csproj` line 12 — `Microsoft.AspNetCore.Mvc.Testing` is referenced but never used. The `PlaywrightFixture` shells out to `dotnet exec` instead.
**Current:** All HTTP-level behavior (auth challenge redirects, antiforgery enforcement on the Identity logout form, `ReturnUrl` handling, `[Authorize(Roles="Admin")]` 302/403, route matching, error middleware) is tested only through a browser. Many of these don't need a browser at all.
**Recommended:** Add `CopilotBlazorTemplate.IntegrationTests` using `WebApplicationFactory<Program>` + `HttpClient` for HTTP-shaped assertions, and keep Playwright for actual rendering/interaction. Example:
```csharp
public class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Dashboard_Unauthenticated_Returns_302_To_Login()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/dashboard");
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location!.OriginalString.Should().Contain("/Account/Login");
    }
}
```
This also lets you swap `AppDbContext` to in-memory or a Testcontainers SQL Server via `ConfigureTestServices`, getting deterministic seeded data per fixture without a subprocess.
**Why:** Recommended pyramid for a Blazor Web App in 2025 is unit → component (bUnit) → in-process integration (`WebApplicationFactory`) → E2E (Playwright). The template currently jumps from unit straight to browser, which is slow, flaky, and overkill for HTTP semantics.
**Effort:** M (new project, ~15-20 tests).

### [High] EF Core test coverage is dangerously thin
**Where:** `tests/CopilotBlazorTemplate.UnitTests/SeedDataTests.cs` (only 3 tests, all on `SeedData`)
**Current:**
- `AppDbContext` configuration (entity mappings, indexes, `ApplicationUser.DisplayName` column constraints) is never asserted.
- The single Identity migration `20260518120430_InitialCreate` has no apply/round-trip test.
- `SeedDataTests` uses `UseInMemoryDatabase` — Microsoft has formally deprecated `InMemory` for testing for years; behavior diverges from real providers (no constraint enforcement, no relational SQL, different query translation).
**Recommended:**
1. Switch `SeedDataTests` to **SQLite in-memory** (`new SqliteConnection("DataSource=:memory:")` + `UseSqlite(connection)`) — same provider semantics as production.
2. Add a **migration smoke test**: apply all migrations to a fresh SQLite DB and assert the resulting schema matches `AppDbContextModelSnapshot` via `context.Database.GenerateCreateScript()` or `ModelDiffer`.
3. Add mapping tests for `ApplicationUser.DisplayName` (max length, default, persistence round-trip).
4. For richer EF tests later, recommend **Testcontainers** with SQL Server / PostgreSQL when the template grows past SQLite.
**Why:** "Don't test against InMemory" is now the official EF Core guidance (efcore docs, .NET 8+). Migration drift is one of the most common production-breaking changes — a one-line test catches it.
**Effort:** S (per item).

### [Med] Playwright tests have no trace/video/screenshot-on-failure
**Where:** `tests/CopilotBlazorTemplate.E2ETests/PlaywrightFixture.cs`
**Current:** `NewContextAsync` does not configure `RecordVideoDir`, `RecordHarPath`, or `Tracing.StartAsync`. When a CI run fails, the only artifact is the TRX message — debugging requires re-running locally.
**Recommended:** Wrap each test in a base helper or extend the fixture to start a trace per context and persist on failure:
```csharp
await ctx.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
try { /* test body */ }
finally
{
    var path = $"TestResults/traces/{TestContext.Current.Test.DisplayName}.zip";
    await ctx.Tracing.StopAsync(new() { Path = path });
}
```
Upload `TestResults/traces/` as a CI artifact. xUnit v3 makes `TestContext.Current` first-class; on v2 you can pass test name via constructor + `IDisposable`.
**Why:** Standard Playwright guidance (playwright.dev "CI" + "Trace Viewer" 2025 docs) — trace-on-retry/failure is the single highest-ROI flake-debugging investment.
**Effort:** S.

### [Med] No retry / flake policy on E2E
**Where:** `xunit.runner.json`, `PlaywrightFixture.cs`
**Current:** Tests run once. Any transient WebSocket / Blazor SignalR reconnect glitch = red CI.
**Recommended:** Add `[Retry]` via `Xunit.RetryFact` (community attribute) or migrate to v3 which has first-class retry support, or use Polly-wrapped helpers. Enable Playwright trace-on-retry. Be conservative — retry only at the E2E layer, and surface retry counts in the PR summary so flakes don't hide silently.
**Why:** InteractiveServer + SignalR + shared singleton app are real flake vectors; one-shot runs underestimate stability.
**Effort:** S.

### [Med] Assertions: raw `Assert.*` with no library
**Where:** all `*Tests.cs` files
**Current:** Mix of `Assert.Equal`, `Assert.True(..., "msg")`, `Assert.Contains(string, string)` — readable but verbose, and complex object comparisons will get ugly fast.
**Recommended:** Pick one and apply consistently. **Shouldly** (BSD, lightweight, great failure messages) or **AwesomeAssertions** (community fork of FluentAssertions after FA went commercial in v8). Avoid FluentAssertions 8.x for OSS templates due to its Xceed paid license. Document the choice in `AGENTS.md` testing section.
**Why:** Test maintainability + readability + onboarding — a template should set the assertion convention so downstream users don't bikeshed.
**Effort:** S.

### [Med] `xunit.runner.json` only present in E2E, not Unit project
**Where:** `tests/CopilotBlazorTemplate.E2ETests/xunit.runner.json` exists; UnitTests has none.
**Current:** E2E caps `maxParallelThreads: 4` (sensible — shared singleton). UnitTests inherits defaults (fully parallel across collections, threads = CPU count) — fine, but undocumented and inconsistent.
**Recommended:** Add an explicit `xunit.runner.json` to UnitTests with `parallelizeAssembly: true`, `parallelizeTestCollections: true`, leave `maxParallelThreads: 0` (unbounded). Makes intent explicit.
**Why:** Clarity + reproducibility across machines. Templates teach by example.
**Effort:** S (one file).

### [Med] No code coverage collection or threshold
**Where:** csprojs reference `coverlet.collector` but CI never collects.
**Current:** No `--collect:"XPlat Code Coverage"` in CI, no Cobertura output, no ReportGenerator, no minimum threshold.
**Recommended:** Add to CI:
```yaml
dotnet test ... --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```
Add **ReportGenerator** step that merges unit+e2e coverage and emits HTML + summary. Use **`Codecov` or `Coverlet.MSBuild` threshold** to fail the build below e.g. 70% line / 60% branch. Report coverage delta in the existing sticky PR comment (extend `build_test_summary.py`).
**Why:** Coverage isn't quality, but coverage *decline* in PRs is a signal worth gating. Currently invisible.
**Effort:** M.

### [Med] No accessibility testing
**Where:** E2E tests
**Current:** Sidebar/links/buttons are semantically tested via `GetByRole` (good!), but no axe-core checks. Templates have outsized influence on what users ship — should ship a11y green by default.
**Recommended:** Add `Deque.AxeCore.Playwright` NuGet package, add one a11y test per top-level page (`/`, `/Account/Login`, `/dashboard`, `/admin`) failing on serious/critical violations:
```csharp
var results = await new AxeBuilder(page).AnalyzeAsync();
results.Violations.Where(v => v.Impact is "serious" or "critical")
    .Should().BeEmpty();
```
**Why:** axe-playwright is the standard integration in 2025; cost is minimal; catches real regressions (missing labels, contrast, ARIA misuse).
**Effort:** S.

### [Med] Skill / instruction files contradict actual implementation
**Where:** `.github/skills/playwright-e2e/SKILL.md` lines 9, 14-21; `.github/instructions/playwright-tests.instructions.md` lines 8-9, 17
**Current:** Both docs say "App startup: `WebApplicationFactory<Program>`" and demonstrate CSS-attribute selectors (`input[name='Input.Email']`, `button[type='submit']`). The fixture actually uses `dotnet exec` subprocess and the tests use `GetByLabel`/`GetByRole`. Each test does **not** use `Browser.NewPageAsync()` — it goes via per-test `IBrowserContext` to inherit storage state.
**Recommended:** Rewrite both files to match reality: document the singleton fixture, `NewAdminContextAsync`/`NewUserContextAsync`/`NewAnonymousContextAsync`/`LoginAsAsync` helpers, semantic locator preference, 2s default timeout, the storage-state caching trick, and how to add new tests without breaking parallelism.
**Why:** Skills + instructions drive Copilot/agent behavior. Mismatched docs will generate broken tests and undo the fixture's perf design.
**Effort:** S.

### [Low] No `Theory` / parameterized tests anywhere
**Where:** All 26 tests are `[Fact]`.
**Current:** Cases like "invalid password", "unknown email", "empty submission" in `AuthTests` are three near-identical `[Fact]`s. `Authenticated_User_Redirected_From_Home_To_Dashboard` could iterate over both seeded roles.
**Recommended:** Use `[Theory] + [InlineData]` or `[MemberData]` for matrix tests; reduces duplication, surfaces gaps, faster to extend.
**Why:** Standard xUnit best practice; also makes Copilot-generated test expansion safer.
**Effort:** S.

### [Low] No `TimeProvider` abstraction in tests
**Where:** Currently no `DateTime.UtcNow`/`DateTimeOffset.Now` in tested code paths — but the moment auth lockout, refresh tokens, or audit timestamps land, time-dependent tests will flake.
**Recommended:** Pre-emptively document (in `AGENTS.md` or a testing instruction file) that production code must depend on `TimeProvider` (built into .NET 8+) and tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`). Add one example test now.
**Why:** Cheapest to bake in before time-dependent code exists.
**Effort:** S.

### [Low] No visual regression / snapshot testing
**Where:** No screenshot baselines or `Verify.Xunit` usage.
**Current:** N/A
**Recommended:** Optional — add **Verify.Xunit** for component snapshot tests (`Verify(cut.Markup)`) once bUnit is in place. Add Playwright `toHaveScreenshot()` baselines for landing + dashboard if a designed UI is a template promise. Skip if scope creep is a concern; flag in docs.
**Why:** High-signal for layout-heavy templates; otherwise low priority.
**Effort:** M.

### [Low] No mutation testing
**Where:** Stryker.NET not present.
**Current:** With 5 unit tests, coverage % could look fine but mutants survive. With component + integration tests added, Stryker becomes meaningful.
**Recommended:** Add `dotnet-stryker` as a manual (workflow_dispatch) job once test count >50. Don't gate PRs on it — use as a quarterly health check.
**Why:** Mutation testing reveals test-suite weakness that coverage hides; deferred until the suite is bigger.
**Effort:** M (later).

### [Low] CI runs E2E in same job as unit; no sharding; chromium-only
**Where:** `.github/workflows/ci.yml`
**Current:** Single job, sequential `dotnet test` calls. ~all E2E tests on one runner, single browser.
**Recommended:** Split into `unit-tests` and `e2e-tests` jobs (parallel), cache Playwright browsers (`~/.cache/ms-playwright`) keyed on Playwright version. Once suite >50 E2E tests, consider Playwright's xUnit-side sharding (manual partition by class). Cross-browser (firefox/webkit) is overkill for a server-rendered template — keep chromium-only.
**Why:** Faster PR feedback; the current ~unit + browser-install + E2E chain is sequential.
**Effort:** S.

### [Low] No network mocking pattern documented
**Where:** N/A yet — but once the template grows API calls, `page.RouteAsync` becomes essential.
**Recommended:** Add a one-paragraph section to the Playwright instruction file showing the `RouteAsync` pattern for mocking outbound HTTP from server-rendered components.
**Effort:** S.

### [Low] Logout test uses real login instead of cached storage
**Where:** `NavigationTests.cs` line 31 — `LoginAsAsync` then logout
**Current:** Comment explains the choice ("post-logout cookie state on this context is observable"), which is correct for *this* test. However a cheaper path exists: `NewUserContextAsync()` (cached), then issue logout from there — the storage-state context has a valid cookie too.
**Recommended:** Verify whether the cached-context logout works (the antiforgery token regeneration on logout might force a real session). If it does, swap; saves ~1 login round-trip. Leave a comment either way.
**Effort:** S.

## Testing pyramid target

Recommended balance for a Blazor Web App template at maturity (~6-12 months out):

```
                         /\
                        /E2\         Playwright   ~15-25 tests   "happy paths + auth boundary"
                       /----\
                      /Integ.\       WebAppFactory ~25-40 tests   "HTTP, auth redirects, antiforgery"
                     /--------\
                    /Component \     bUnit         ~40-70 tests   "every razor file's render branches"
                   /------------\
                  /    Unit      \   xUnit + DI   ~30-60 tests   "entities, services, validators, mappers"
                 /----------------\
```

Current state: roughly `5 / 0 / 0 / 21` — top-heavy and middle-empty (inverted pyramid for the parts that exist).

Target ratio (after Highs are addressed): **~30-40% unit, ~30% component, ~20% integration, ~10-15% E2E**.

## Quick wins (top 5)

1. **Sync `playwright-tests.instructions.md` and `playwright-e2e/SKILL.md` with reality.** They currently teach the wrong patterns (`WebApplicationFactory`, CSS selectors) — Copilot will generate broken tests. (S, immediate)
2. **Add bUnit + 5 starter component tests** for `Dashboard`, `Admin`, `AuthenticatedLayout`, `NotFound`, sidebar role gating. Establishes the missing pyramid layer with a small footprint. (M)
3. **Swap `UseInMemoryDatabase` for SQLite in-memory** in `SeedDataTests` + add one migration apply test. (S)
4. **Enable trace-on-failure** in `PlaywrightFixture` and upload `TestResults/traces/` as a CI artifact. Highest-ROI debugging investment. (S)
5. **Add `coverlet` collection to CI + ReportGenerator step + extend `build_test_summary.py`** to show coverage % in the PR comment. (M)

## References

- bUnit documentation — https://bunit.dev/ (Blazor component testing, .NET 10 supported)
- ASP.NET Core integration tests — https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
- EF Core testing guidance (avoid InMemory) — https://learn.microsoft.com/en-us/ef/core/testing/
- xUnit v3 migration — https://xunit.net/docs/getting-started/v3/
- Playwright .NET — Test fixtures, tracing, CI — https://playwright.dev/dotnet/docs/test-runners and /trace-viewer
- `Deque.AxeCore.Playwright` — https://github.com/dequelabs/axe-core-nuget
- Microsoft TimeProvider + FakeTimeProvider — https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview
- Stryker.NET — https://stryker-mutator.io/docs/stryker-net/introduction/
- Verify (snapshot) — https://github.com/VerifyTests/Verify
- Shouldly — https://docs.shouldly.org/ ; AwesomeAssertions — https://github.com/AwesomeAssertions/AwesomeAssertions (FluentAssertions fork before commercial license)
- Testcontainers for .NET — https://dotnet.testcontainers.org/
- Repo commit `da95121` — "perf(test): cut E2E wall time with shared singleton + storage state" (good prior art to preserve)
