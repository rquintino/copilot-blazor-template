# Integration tests with WebApplicationFactory

## Goal
Create a new in-process integration-test project that exercises HTTP-shaped behaviour (auth redirects, antiforgery enforcement, role gating, `returnUrl` handling) via `WebApplicationFactory<Program>` + `HttpClient`, so the E2E browser tests can stay focused on actual rendering and interaction.

## Scope
- Create `tests/CopilotBlazorTemplate.IntegrationTests/` with its own csproj.
- Add a `Program.cs` partial declaration so `WebApplicationFactory<Program>` can resolve it (mirroring the partial pattern the E2ETests project already exploits internally — confirm the existing `Program.cs` works with the factory; if not, add a `public partial class Program { }` shim at the bottom of `Program.cs`).
- Write ~15-20 tests covering:
  - `GET /dashboard` unauthenticated → 302 to `/Account/Login?returnUrl=…`.
  - `GET /admin` as a `User`-role user → 302 (or 403) to `/Account/AccessDenied`.
  - `GET /admin` as an `Admin`-role user → 200.
  - `POST /Account/Logout` without antiforgery token → 400 / 415.
  - `POST /Account/Logout` with antiforgery token → 302 to `/`.
  - `GET /Identity/Account/Manage/...` unauthenticated → 302.
  - `GET /` unauthenticated → 200 (anonymous landing) **or** 302 to `/dashboard` if signed in.
  - Open-redirect attempt on `?returnUrl=//evil.com` → not honoured (response location does not contain `evil.com`).
  - `GET /healthz` — if not present, skip with reason.
  - Plus a handful of headers checks (e.g. `X-Content-Type-Options: nosniff` if task 03 has shipped).
- Override `AppDbContext` to use SQLite in-memory (`new SqliteConnection("DataSource=:memory:")`) via `WebApplicationFactory.WithWebHostBuilder(b => b.ConfigureTestServices(...))`.

Out of scope:
- bUnit component tests (task 05).
- E2E test improvements (task 07).
- Coverage collection / CI wiring (task 08).
- Migrating to xUnit v3 (task 07).

## Edit zone
- `tests/CopilotBlazorTemplate.IntegrationTests/CopilotBlazorTemplate.IntegrationTests.csproj` (new)
- `tests/CopilotBlazorTemplate.IntegrationTests/**/*.cs` (new)
- `CopilotBlazorTemplate.slnx` (add the new project under `tests/` — additive XML insert)
- `src/CopilotBlazorTemplate.Web/Program.cs` (only if a `public partial class Program { }` shim is needed — append at the very bottom of the file, no other edits)
- `Directory.Packages.props` (if it exists — add `<PackageVersion>` entries for `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`)

## Independence guarantee
- The new project is wholly new; it cannot collide on its own files.
- `Program.cs` edit, if needed, is a single `public partial class Program { }` line appended at the end. Task 03 owns `Program.cs` rewrites but appends are explicitly allowed; coordinate by placing the shim outside any existing class/namespace block. If `Program.cs` already has this declaration (or implicit-Program is on, which it is in net10 minimal hosting via `WebApplicationBuilder`), skip the edit.
- `.slnx` edit is additive (one `<Project Path="…" />` element added under the existing `tests/` solution folder). Task 10 may also touch `.slnx` to add a Solution Items folder — coordinate by placing the new project line above any Solution Items group.
- If task 02 (CPM + lockfiles) has shipped, lockfile updates pick up the new project automatically on next `dotnet restore`; commit the new `packages.lock.json`.
- If task 03 has shipped `Database:AutoMigrate=true` only in Development, the WebApplicationFactory runs in `Production` by default — override the environment to `Development` in the factory **or** invoke `db.Database.Migrate()` manually after `ConfigureTestServices`.
- If task 07 ships first and migrates the existing UnitTests to xUnit v3, this task can land on v2 first and migrate alongside in a follow-up. Stay on whatever xUnit major UnitTests currently uses.
- The Admin-role test seeds users via the factory's test services, not by relying on `SeedData` (which is Dev-only after task 03). This insulates from any seed changes.
- Tests asserting headers (`X-Content-Type-Options`, CSP) MUST be skipped with `[Fact(Skip = "Re-enable after task 03 ships security headers")]` if task 03 has not yet shipped.

This task may sit in `backlog/` for weeks. By the time it is picked up the routes, auth pipeline, and `Program.cs` may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Before adding `public partial class Program { }`, grep `Program.cs` for it. Before adding the project to `.slnx`, read the slnx to see whether someone already added it. Before adding a `<PackageVersion>` to `Directory.Packages.props`, check whether it is already present.
- **File moved/renamed.** Routes asserted by tests (`/dashboard`, `/admin`, `/Account/Login`, `/Account/Logout`) may have moved. Verify each by `grep -rln '@page' src/CopilotBlazorTemplate.Web/` and adjust the test URLs to match. If `appsettings*.json` or `Program.cs` paths have changed (renamed project), locate them via `git ls-files`.
- **Prerequisite work already done.** Quick checks: does an `IntegrationTests` project already exist? Does `Program.cs` already have the partial-class shim? Are security headers already in place (so `HeadersTests` can be un-skipped)? Skip steps whose intent is already in place and note in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if an integration project exists with overlapping tests, additively add only the missing scenarios.
- If intent conflicts (e.g. someone used `UseInMemoryDatabase` instead of SQLite `:memory:`), surface it in the PR description; rewrite only if the conflict materially affects test quality.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read `src/CopilotBlazorTemplate.Web/Program.cs`, the `.slnx`, and the test routes you plan to assert against (`grep -rln '@page' src/CopilotBlazorTemplate.Web/`). The snippets below describe the *intent* of each test; apply that intent to whatever routes / Identity endpoints / appsettings shape the project actually has today. If the Web project has been renamed, locate it via `git ls-files '*.Web.csproj'`. Skip any sub-step whose intent is already in place; note the skip in the PR description.
2. `dotnet new xunit -o tests/CopilotBlazorTemplate.IntegrationTests` (skip if the folder already exists; instead add the missing tests inside it). Add a project reference to the Web csproj (locate via `git ls-files '*.Web.csproj'`).
3. Add NuGet references: `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.Data.Sqlite`, `coverlet.collector`. Match the existing xUnit version used by the other test projects (read their csprojs first).
4. Add a `Program.cs` shim only if it's not already implicitly available. Grep `Program.cs` for `partial class Program`; if absent, append at the bottom of `src/CopilotBlazorTemplate.Web/Program.cs`:
   ```csharp
   public partial class Program { } // for WebApplicationFactory<Program>
   ```
5. Write a base `IntegrationTestFactory : WebApplicationFactory<Program>` that:
   - Overrides `ConfigureWebHost` to set environment to `Development` (or sets `Database:AutoMigrate=true` via in-memory config).
   - Replaces the registered `DbContextOptions<AppDbContext>` to use a SQLite `:memory:` connection (kept open for the lifetime of the factory).
   - Calls `db.Database.Migrate()` once after the host is built so the schema is in place.
   - Provides helper methods `CreateAnonymousClient()`, `CreateUserClient()`, `CreateAdminClient()` returning `HttpClient` instances with appropriate auth cookies (sign-in via the real `/Account/Login` form once, then reuse the cookie).
6. Add the test classes (skip any class whose intent is already covered):
   - `AuthRedirectTests` — unauthenticated access patterns.
   - `RoleGatingTests` — `User` vs `Admin` access to `/admin`.
   - `AntiforgeryTests` — logout without token, logout with token.
   - `OpenRedirectTests` — `?returnUrl=//evil.com`, `?returnUrl=https://evil.com`.
   - `HeadersTests` — `X-Content-Type-Options`, CSP, Referrer-Policy (Skip if task 03 not yet shipped).
7. Add `tests/CopilotBlazorTemplate.IntegrationTests/xunit.runner.json` (`parallelizeTestCollections: false` — `WebApplicationFactory` is fine across collections but `IClassFixture` per-class is enough).
8. Update `CopilotBlazorTemplate.slnx` only if the new project is not already listed there:
   ```xml
   <Folder Name="/tests/">
     <Project Path="tests/CopilotBlazorTemplate.IntegrationTests/CopilotBlazorTemplate.IntegrationTests.csproj" />
     <!-- existing Project lines -->
   </Folder>
   ```
9. Run `dotnet restore` then `dotnet test tests/CopilotBlazorTemplate.IntegrationTests`. Iterate until green.
10. Verify the whole solution still builds: `dotnet build`.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] An integration-tests project exists, is referenced from the solution, and uses `WebApplicationFactory<Program>` against the real Web project (not a stub).
- [ ] At least 15 test methods cover the scenarios in Scope (auth redirects, role gating, antiforgery, open-redirect rejection — plus headers if task 03 has shipped, otherwise skipped with a clear reason).
- [ ] All tests run in-process (no Playwright, no subprocess) with total wall time under 10 seconds.
- [ ] The integration suite backs `AppDbContext` with SQLite `:memory:`, not `UseInMemoryDatabase`.
- [ ] `dotnet test` (whole solution) passes.
- [ ] If task 02 has shipped, the new `packages.lock.json` for the integration project is committed.

## References
- Audit Sprint-1 item 13 (WebApplicationFactory integration tests): `../../../docs/audits/2026-05-18/REPORT.md`.
- Automated Testing finding "No `WebApplicationFactory` integration tests despite the package being referenced": `../../../docs/audits/2026-05-18/04-automated-testing.md`.
- ASP.NET Core integration tests — <https://learn.microsoft.com/aspnet/core/test/integration-tests>
- `WebApplicationFactory<TEntryPoint>` — <https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1>
