# E2E test improvements

## Goal
Make the existing Playwright suite production-grade: trace-on-failure with CI artifact upload, a conservative retry policy, real SQLite in-memory in `SeedDataTests` (no more `UseInMemoryDatabase`), an `xunit.runner.json` parity fix for UnitTests, an assertion-library decision, axe-core a11y smoke tests, and a planned migration to xUnit v3.

## Scope
- Enable `Tracing.StartAsync` per `IBrowserContext` in `PlaywrightFixture` (Screenshots + Snapshots + Sources), stop the trace on failure and persist to `TestResults/traces/`.
- Add a one-retry policy for E2E tests (via `Xunit.RetryFact` community attribute on the most flake-prone classes, or by migrating to xUnit v3 which has first-class retry).
- Pick an assertion library — recommend **Shouldly** for the .NET 10 era (lightweight, open-source, good failure messages). Add to both test projects' `Directory.Packages.props` entry. Rewrite **one** existing test to demonstrate the pattern; document the convention in `tests/AGENTS.md` (new).
- Add `xunit.runner.json` to `CopilotBlazorTemplate.UnitTests` if task 05 has not already shipped it.
- Replace `UseInMemoryDatabase` in `SeedDataTests` with SQLite in-memory (`new SqliteConnection("DataSource=:memory:")` + `UseSqlite`). Add a new migration-smoke test that applies all migrations to a fresh SQLite DB and asserts the schema matches `AppDbContextModelSnapshot`.
- Add `Deque.AxeCore.Playwright`. Add four a11y smoke tests (`/`, `/Account/Login`, `/dashboard`, `/admin`) failing on `serious`/`critical` violations.
- Plan and execute the xUnit v2 → v3 migration in both test projects (`xunit` → `xunit.v3`, `xunit.runner.visualstudio` → `xunit.v3.runner.visualstudio`). Update any `IAsyncLifetime` signatures.

Out of scope:
- Coverage collection / ReportGenerator / sticky-comment delta (owned by task 08).
- Splitting CI into separate `unit-tests` and `e2e-tests` jobs (owned by task 08).
- Visual regression / `Verify` / Stryker (defer).
- Rewriting the Playwright skill + instruction docs (owned by task 09).

## Edit zone
- `tests/CopilotBlazorTemplate.E2ETests/PlaywrightFixture.cs`
- `tests/CopilotBlazorTemplate.E2ETests/*.cs` (one test rewritten to Shouldly; a11y tests added)
- `tests/CopilotBlazorTemplate.E2ETests/CopilotBlazorTemplate.E2ETests.csproj` (add packages)
- `tests/CopilotBlazorTemplate.E2ETests/xunit.runner.json` (already exists — additive `methodDisplay` / `methodDisplayOptions` if needed)
- `tests/CopilotBlazorTemplate.UnitTests/CopilotBlazorTemplate.UnitTests.csproj` (add packages)
- `tests/CopilotBlazorTemplate.UnitTests/SeedDataTests.cs`
- `tests/CopilotBlazorTemplate.UnitTests/MigrationSmokeTests.cs` (new)
- `tests/CopilotBlazorTemplate.UnitTests/xunit.runner.json` (new, if task 05 has not shipped it)
- `tests/AGENTS.md` (new — short doc for the convention)
- `Directory.Packages.props` (if it exists — add `Shouldly`, `Deque.AxeCore.Playwright`, `Microsoft.EntityFrameworkCore.Sqlite`, `xunit.v3*`)

## Independence guarantee
- If task 02 (CPM) has shipped, add new `<PackageVersion>` entries to `Directory.Packages.props`. Otherwise, declare versions inline in the csproj and TODO.
- If task 05 (bUnit) has shipped, it has already added an `xunit.runner.json` to UnitTests — skip that file in this task. If not, this task creates it.
- The xUnit v3 migration is the only structurally invasive change. Do it last in this task's Steps so the rest can land independently if v3 hits an issue. If task 05 / 06 are merged on v2 first, this task's v3 migration upgrades all three test projects together.
- `Tracing` and retry attribute changes are confined to E2E files; no impact on UnitTests / IntegrationTests.
- The `SeedDataTests` SQLite swap is internal to one file; if task 03 changed the seed behaviour, this task's test rewrites simply use whatever the new behaviour is.
- Adding `Deque.AxeCore.Playwright` is additive; no existing test relies on its absence.
- `tests/AGENTS.md` is new; if a parent task (10) also touches AGENTS.md, this child file remains independent per the AGENTS.md hierarchical spec.

This task may sit in `backlog/` for weeks. By the time it is picked up the test infrastructure may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before editing `PlaywrightFixture.cs`, `SeedDataTests.cs`, or the csprojs, read them. Tracing may already be enabled; `xunit.runner.json` may exist; SQLite may already be in use. Add only what's missing.
- **File moved/renamed.** Tests may have been reorganised. Locate `PlaywrightFixture` by symbol (`grep -rln 'class PlaywrightFixture' tests/`); locate `SeedDataTests` similarly. If the project layout changed (e.g. `tests/CopilotBlazorTemplate.E2ETests/` renamed), use the actual paths.
- **Prerequisite work already done.** Quick checks: does `Shouldly` already appear in any csproj? Are there already a11y tests under E2E? Is the project on `xunit.v3` already? Skip whatever is in place; note in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if `Shouldly` is referenced and several tests use it, just bring the rest in line over time (per the convention "leave existing `Assert.*` calls until touched").
- If intent conflicts (e.g. someone chose FluentAssertions instead of Shouldly), surface in the PR description; don't silently swap libraries.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read `PlaywrightFixture.cs`, `SeedDataTests.cs`, the test project csprojs, and any existing `xunit.runner.json` files. The snippets below describe the *intent* of each change — apply that intent to whatever the files actually contain today. If the fixture's context-helper API has been renamed, adapt the calls. Skip steps whose intent is already in place; note the skip in the PR description.
2. **Trace-on-failure in the Playwright fixture:** wherever per-test contexts are created (today: `NewAdminContextAsync` / `NewUserContextAsync` / `NewAnonymousContextAsync`), start a trace immediately after `BrowserContext` creation. Provide a `StopTraceAsync(IBrowserContext ctx, string testName, bool success)` helper that stops the trace, persists to `TestResults/traces/{testName}.zip` on failure (configurable to always-on), and discards on success. Plumb the call through a test-base or `IAsyncLifetime` so individual tests do not have to remember.
3. **Retry policy:** install `Xunit.RetryFact` (or whatever xUnit v2-compatible retry attribute is current). Apply `[RetryFact(2)]` only to known-flaky classes (identify by reading the current E2E classes — `AuthTests`, `NavigationTests` are the historical hot spots). Keep stable tests on `[Fact]`. Document retry count in the sticky CI summary (check `build_test_summary.py` to see what's already emitted).
4. **Shouldly:**
   - Add a `Shouldly` reference to both test projects (centralised in `Directory.Packages.props` if task 02 has shipped).
   - Rewrite one existing test (any clear example, doesn't have to be in `AuthTests.cs`) from `Assert.Equal(...)` / `Assert.Contains(...)` to `.ShouldBe(...)` / `.ShouldContain(...)`.
   - Write `tests/AGENTS.md` documenting the choice and the convention ("new tests use Shouldly; existing `Assert.*` calls are fine to leave until touched").
5. **`SeedDataTests` SQLite swap:** wherever the existing seed test still uses `UseInMemoryDatabase`, replace with SQLite `:memory:`. The shape:
   ```csharp
   await using var connection = new SqliteConnection("DataSource=:memory:");
   await connection.OpenAsync();
   var options = new DbContextOptionsBuilder<AppDbContext>()
       .UseSqlite(connection)
       .Options;
   await using var ctx = new AppDbContext(options);
   await ctx.Database.MigrateAsync();
   // ... seed and assert against ctx
   ```
6. **`MigrationSmokeTests.cs`:** one test that applies all migrations and asserts `ctx.Database.GenerateCreateScript()` is non-empty and contains the expected `AspNetUsers` / `DisplayName` columns (read the actual current schema first — column names are the source of truth, not this snippet).
7. **`xunit.runner.json` (UnitTests):** add only if not already present. Use the same shape as the E2E project's `xunit.runner.json` for consistency, but with `maxParallelThreads: 0` (unbounded — there is no shared singleton to protect).
8. **A11y tests:** add `tests/CopilotBlazorTemplate.E2ETests/AccessibilityTests.cs` (skip if a similarly-scoped test already exists):
   ```csharp
   [Fact]
   public async Task Login_Has_No_Serious_Or_Critical_A11y_Violations()
   {
       var ctx = await Fixture.NewAnonymousContextAsync();
       var page = await ctx.NewPageAsync();
       await page.GotoAsync($"{Fixture.BaseUrl}/Account/Login");
       var results = await new AxeBuilder(page).AnalyzeAsync();
       var serious = results.Violations.Where(v => v.Impact is "serious" or "critical").ToList();
       serious.ShouldBeEmpty();
   }
   ```
   Repeat for `/`, `/dashboard`, `/admin` (use the user/admin context helpers as appropriate; adjust if the fixture's helper names have changed).
9. **xUnit v3 migration (do last):**
   - Swap `xunit` → `xunit.v3`, `xunit.runner.visualstudio` → `xunit.v3.runner.visualstudio` across all test projects (UnitTests, E2ETests, plus IntegrationTests if task 06 has shipped).
   - Update `IAsyncLifetime.InitializeAsync` / `DisposeAsync` to return `ValueTask`.
   - Remove any v2-only namespace references; update `using Xunit;` as needed.
   - Run `dotnet test`; iterate.
10. Run the full test suite locally; confirm trace files appear on a simulated failure (temporarily make one test fail).

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] On a forced E2E failure, a trace zip appears under `TestResults/traces/` (or wherever the fixture writes traces). On success runs no trace artifact is retained.
- [ ] At least one E2E test class uses a retry mechanism (community attribute on v2, native retry on v3).
- [ ] `Shouldly` is referenced from at least one test project and at least one test uses `.ShouldXxx(...)` assertions.
- [ ] `tests/AGENTS.md` (or the equivalent doc location convention) documents the assertion convention.
- [ ] The seed data test exercises real EF migrations against an in-process SQLite database (no `UseInMemoryDatabase`).
- [ ] A migration-smoke test exists that applies all migrations and asserts the schema includes the expected tables/columns.
- [ ] Both UnitTests and E2ETests have an `xunit.runner.json` (regardless of which task placed it).
- [ ] `Deque.AxeCore.Playwright` is referenced and at least 3 a11y tests pass with zero `serious`/`critical` violations on the listed routes.
- [ ] All test projects compile and pass on xUnit v3.
- [ ] `dotnet test` passes the whole solution.

## References
- Audit Sprint-1 items 14 (SQLite in-memory), 21 (trace-on-failure); Sprint-2 list (xUnit v3, Shouldly, axe): `../../../docs/audits/2026-05-18/REPORT.md`.
- Automated Testing findings "Playwright tests have no trace/video/screenshot-on-failure", "No retry / flake policy on E2E", "Assertions: raw Assert.* with no library", "`xunit.runner.json` only present in E2E, not Unit project", "EF Core test coverage is dangerously thin", "No accessibility testing", "xUnit v2 is end-of-life direction": `../../../docs/audits/2026-05-18/04-automated-testing.md`.
- xUnit v3 migration — <https://xunit.net/docs/getting-started/v3/>
- Shouldly — <https://docs.shouldly.org/>
- `Deque.AxeCore.Playwright` — <https://github.com/dequelabs/axe-core-nuget>
- Playwright trace viewer — <https://playwright.dev/dotnet/docs/trace-viewer>
