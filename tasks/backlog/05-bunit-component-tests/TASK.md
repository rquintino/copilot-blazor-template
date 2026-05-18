# bUnit component tests

## Goal
Fill the empty middle layer of the testing pyramid by adding bUnit and ~5 starter Blazor component tests that exercise role-aware rendering, layout structure, and the not-found / reconnect scaffolding — without spinning up a browser or a server.

## Scope
- Add the `bunit` NuGet package to the existing `CopilotBlazorTemplate.UnitTests` project (do not create a new test project — keep the tree minimal).
- Add ~5 component tests under `tests/CopilotBlazorTemplate.UnitTests/Components/`:
  1. `AuthenticatedLayoutTests` — `AddTestAuthorization` with the `Admin` role renders the admin link; without the role hides it.
  2. `DashboardTests` — renders a heading and the welcome text for the signed-in user.
  3. `AdminTests` — renders a table of users; mock or substitute the data dependency (`IDbContextFactory<AppDbContext>` if task 03 has shipped, else a thin POCO model).
  4. `NotFoundTests` — renders the 404 marker text and a link back to `/`.
  5. `BlazorErrorUiTests` — renders the `data-nosnippet` toast wrapper (if task 04 has shipped the extracted component) **or** a sidebar role-gating test (if task 04 has not shipped).
- Add an `xunit.runner.json` to UnitTests (declaring `parallelizeAssembly: true`, `parallelizeTestCollections: true`).

Out of scope:
- Migrating to xUnit v3 (owned by task 07).
- Adding an assertion library (Shouldly / AwesomeAssertions) — owned by task 07.
- Coverage collection in CI (owned by task 08).
- Integration tests with `WebApplicationFactory` (owned by task 06).

## Edit zone
- `tests/CopilotBlazorTemplate.UnitTests/CopilotBlazorTemplate.UnitTests.csproj` (add `bunit` `<PackageReference>`)
- `tests/CopilotBlazorTemplate.UnitTests/Components/*.cs` (new)
- `tests/CopilotBlazorTemplate.UnitTests/xunit.runner.json` (new)
- `Directory.Packages.props` (if it exists — add `<PackageVersion Include="bunit" Version="…" />`)

## Independence guarantee
- If `Directory.Packages.props` exists (task 02 shipped), add the new `<PackageVersion>` there and reference without a version in the csproj. Otherwise, add `<PackageReference Include="bunit" Version="<pinned>" />` directly in the csproj and leave a TODO comment referencing task 02 so a later sweep centralises it.
- If task 03 (DbContextFactory) has shipped, the `AdminTests` mock injects `IDbContextFactory<AppDbContext>`. If not, mock the scoped `AppDbContext` directly. Both branches use `Microsoft.EntityFrameworkCore.InMemory` for component tests — this is acceptable here because the test asserts component markup, not query semantics (see task 07 for why `InMemory` is wrong for `SeedDataTests`).
- If task 04 (Blazor component hygiene) has shipped:
  - `BlazorErrorUi.razor` exists → write `BlazorErrorUiTests`.
  - Components now require `@rendermode InteractiveServer` — bUnit's `TestContext.RenderComponent<T>` works regardless of render mode.
  If task 04 has not shipped, swap the fifth test for `SidebarRoleGatingTests` against `AuthenticatedLayout.razor` (the legacy inline error UI is fine to ignore for that test).
- New tests do not modify any existing test file.
- If task 03 changes the seed credentials or `ApplicationUser` shape, the bUnit tests construct their own `ApplicationUser` instances and seed `IdentityRole` via `AddTestAuthorization` — no dependency on `SeedData`.

This task may sit in `backlog/` for weeks. By the time it is picked up the test project and the components under test may have drifted. Handle the three drift modes explicitly:

- **File already changed by another task.** Before adding `bunit` or `xunit.runner.json`, read the UnitTests csproj and project root: `bunit` may already be referenced, or `xunit.runner.json` may already exist (task 07 sometimes ships it). Don't duplicate references; additively merge any missing settings.
- **File moved/renamed.** Components targeted by these tests (`AuthenticatedLayout`, `Dashboard`, `Admin`, `NotFound`, `BlazorErrorUi`) may have moved to feature folders or been renamed. Locate by symbol (`grep -rln 'class AuthenticatedLayout' src/` / `grep -rln '@page "/admin"' src/`) and reference the current location in `using` statements.
- **Prerequisite work already done.** Before writing each test, check whether an equivalent test already exists in the project (`grep -rln 'AddTestAuthorization' tests/CopilotBlazorTemplate.UnitTests/`). If a test with overlapping intent already exists, skip the duplicate; note in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if `bunit` is already referenced and four sibling tests already exist, just add the missing one.
- If intent conflicts (e.g. someone wrote bUnit tests but on a different version pinned to an older release), surface the conflict; don't silently upgrade.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read the UnitTests csproj, list `tests/CopilotBlazorTemplate.UnitTests/Components/` (if it exists), check for an existing `xunit.runner.json`, and grep for `bunit` references across the test tree. The snippets below describe the *intent* of each test; apply that intent to whatever is actually in the project today. If a component referenced below has been moved/renamed, find its current location via `grep -rln`. Skip duplicates and note in the PR description.
2. Pick a `bunit` version pinned to a release at least 7 days old (per the repo's `.npmrc` cool-down rule, mirrored manually for NuGet). At time of writing, `bunit` 2.x is current; verify on `nuget.org`.
3. Add the package: edit the UnitTests csproj (locate via `git ls-files '*UnitTests*.csproj'`):
   ```xml
   <ItemGroup>
     <PackageReference Include="bunit" />
     <!-- or include Version="2.x.y" if task 02 has not shipped CPM -->
   </ItemGroup>
   ```
   And if `Directory.Packages.props` exists, append `<PackageVersion Include="bunit" Version="2.x.y" />`.
4. Add `tests/CopilotBlazorTemplate.UnitTests/Components/AuthenticatedLayoutTests.cs` (skip if an equivalent test already exists):
   ```csharp
   using Bunit;
   using Bunit.TestDoubles;
   using CopilotBlazorTemplate.Web.Components.Layout;
   using Xunit;

   public class AuthenticatedLayoutTests : TestContext
   {
       [Fact]
       public void Sidebar_Shows_Admin_Link_For_Admin_Role()
       {
           var auth = this.AddTestAuthorization();
           auth.SetAuthorized("admin@template.local");
           auth.SetRoles("Admin");
           var cut = RenderComponent<AuthenticatedLayout>();
           Assert.Contains("/admin", cut.Markup);
       }

       [Fact]
       public void Sidebar_Hides_Admin_Link_For_Non_Admin()
       {
           var auth = this.AddTestAuthorization();
           auth.SetAuthorized("user@template.local");
           auth.SetRoles("User");
           var cut = RenderComponent<AuthenticatedLayout>();
           Assert.DoesNotContain("href=\"/admin\"", cut.Markup);
       }
   }
   ```
5. Add `DashboardTests.cs` along the same lines. Inspect `Dashboard.razor` to see what it actually injects today (`UserManager<ApplicationUser>`, `IDbContextFactory<AppDbContext>`, an application service, etc.) and register fakes accordingly before `RenderComponent<Dashboard>`.
6. Add `AdminTests.cs`. Use `Microsoft.EntityFrameworkCore.InMemory` to back a fake `IDbContextFactory<AppDbContext>` (acceptable for markup-only tests):
   ```csharp
   Services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase("admin-tests"));
   ```
   Seed two users in the in-memory store, then assert the rendered table contains both display names.
7. Add `NotFoundTests.cs` — `RenderComponent<NotFound>()`; assert markup contains the not-found marker text (read `NotFound.razor` first to see what string is actually rendered).
8. Add the fifth test per the Independence guarantee branch.
9. Add `tests/CopilotBlazorTemplate.UnitTests/xunit.runner.json` if it does not already exist:
   ```json
   {
     "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
     "parallelizeAssembly": true,
     "parallelizeTestCollections": true,
     "maxParallelThreads": 0
   }
   ```
   Mark it as content / copy-to-output in the csproj if not auto-picked.
10. Run `dotnet test tests/CopilotBlazorTemplate.UnitTests`. All new tests should pass in well under a second each.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] `bunit` is referenced by the UnitTests project (with version centralised in `Directory.Packages.props` if task 02 has shipped, inline pinned otherwise).
- [ ] At least 5 bUnit test methods exist under the UnitTests project that exercise the components listed in Scope (role gating, dashboard, admin list, not-found, error-UI or sidebar).
- [ ] All bUnit tests run in well under 1 second each (per-test timing visible via `dotnet test --logger "console;verbosity=normal"`).
- [ ] An `xunit.runner.json` exists in the UnitTests project (regardless of whether this task or task 07 shipped it).
- [ ] `dotnet test tests/CopilotBlazorTemplate.UnitTests` passes.
- [ ] `dotnet build` succeeds across the whole solution.

## References
- Audit Sprint-1 item 12 (bUnit + 5 starter tests): `../../../docs/audits/2026-05-18/REPORT.md`.
- Automated Testing findings "No Blazor component tests — middle of the pyramid is empty", "`xunit.runner.json` only present in E2E, not Unit project": `../../../docs/audits/2026-05-18/04-automated-testing.md`.
- bUnit — <https://bunit.dev/>
- Microsoft: Test Razor components — <https://learn.microsoft.com/aspnet/core/blazor/test>
