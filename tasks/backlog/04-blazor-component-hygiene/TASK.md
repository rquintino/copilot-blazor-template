# Blazor component hygiene

## Goal
Demonstrate the modern Blazor Web App per-component opt-in pattern: explicit per-page `@rendermode`, `[StreamRendering]` on async pages, an `ErrorBoundary` around routed content, `PersistentComponentState` for prerender → interactive data hand-off, the Home-page redirect anti-pattern fixed, `CancellationToken` flow into `OnInitializedAsync`, consistent CSS theme for the Identity scaffold pages, and proper data annotations on `ApplicationUser.DisplayName`.

## Scope
- Add explicit `@rendermode` directives to every page in `Components/Pages/` and `Components/Account/Pages/`. SSR by default; `@rendermode InteractiveServer` only on pages that actually need a circuit.
- Add `@attribute [StreamRendering]` to async pages (`Dashboard.razor`, `Admin.razor`, `AuthenticatedLayout.razor` if it does async work).
- Wrap routed content in `MainLayout.razor` and `AuthenticatedLayout.razor` in `<ErrorBoundary>`.
- Wire `PersistentComponentState` for the user/role payload so prerender data carries into the interactive circuit on at least one page (`Dashboard` or `Admin`).
- Replace the `OnInitializedAsync` `NavigationManager.NavigateTo("/dashboard")` in `Home.razor` with a server-side `/` redirect (minimal endpoint in `Program.cs`) or a guarded post-render navigation.
- Add `CancellationToken` capture + `Dispose()` to all components that issue async data calls in `OnInitializedAsync`.
- Make the Identity Account pages visually consistent with the custom theme (one CSS pass; lock in either Bootstrap or the custom theme — recommend the custom theme since `theme.css` already overrides `.btn-primary`).
- Add `[PersonalData]` + `[MaxLength(128)]` to `ApplicationUser.DisplayName` and mark `ApplicationUser` `sealed`.
- Extract the duplicated `<div id="blazor-error-ui">` into a shared `BlazorErrorUi.razor` component used by both layouts.

Out of scope:
- Changes to `Program.cs` DI / startup (owned by task 03).
- Component tests (owned by task 05).
- Render-mode documentation in `AGENTS.md` (owned by task 10 — this task records the convention in a short comment in `Components/_Imports.razor`).

## Edit zone
- `src/CopilotBlazorTemplate.Web/Components/_Imports.razor` (add a small comment documenting the render-mode convention; trim unused `System.Net.Http` imports)
- `src/CopilotBlazorTemplate.Web/Components/Pages/*.razor`
- `src/CopilotBlazorTemplate.Web/Components/Account/Pages/*.razor`
- `src/CopilotBlazorTemplate.Web/Components/Layout/MainLayout.razor`
- `src/CopilotBlazorTemplate.Web/Components/Layout/AuthenticatedLayout.razor`
- `src/CopilotBlazorTemplate.Web/Components/Layout/BlazorErrorUi.razor` (new)
- `src/CopilotBlazorTemplate.Core/Entities/ApplicationUser.cs`
- A new EF migration for the `DisplayName` `[MaxLength(128)]` change: `src/CopilotBlazorTemplate.Core/Migrations/*` (one new pair of files)
- `src/CopilotBlazorTemplate.Web/wwwroot/css/theme.css` (additive — append any new component classes needed for the Account pages)
- `src/CopilotBlazorTemplate.Web/Program.cs` (modify only to add the optional `MapGet("/")` redirect endpoint — if task 03 has shipped, append this; do not restructure)

## Independence guarantee
- `Program.cs` is owned by task 03 for this cycle. If task 03 ships first, this task appends the `MapGet("/")` endpoint near the other `app.Map…` calls without touching the DI block. If task 03 has not shipped, this task can still ship — just leave `Home.razor` as-is and document the redirect-endpoint approach in this task's follow-up notes (or guard the existing `NavigateTo` on `RendererInfo.IsInteractive`).
- The new `ApplicationUser` annotations require a migration. If task 06 (integration tests) or task 07 (SQLite in-memory in `SeedDataTests`) ship first, those tests pick up the new migration automatically.
- The render-mode sweep is purely additive — adding `@rendermode InteractiveServer` to a page that previously inherited the global mode is a no-op behaviour change. Pages that move to SSR-only (`Dashboard`, `Admin` if their interactivity is removed) need verification that no `@onclick` handlers are present; this task does not strip interactivity from pages that have it.
- The `ErrorBoundary` wrap is additive. If task 09 introduces new layouts via skills, those layouts opt in by reusing `BlazorErrorUi.razor`.
- E2E tests may need updated selectors if the Identity-pages CSS pass renames classes. Coordinate by keeping the underlying HTML element / `role` / accessible name unchanged — only swap CSS classes.

This task may sit in `backlog/` for weeks. By the time it is picked up the components and layouts may have drifted from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Read every `.razor` file you plan to touch before editing. If a page already has `@rendermode`, `[StreamRendering]`, an `ErrorBoundary` wrap, or a `CancellationToken` plumbed in, leave it. Where you add new directives, append rather than rewrite the file. If `BlazorErrorUi.razor` already exists, reuse it; do not create a second copy.
- **File moved/renamed.** Pages may have moved between `Components/Pages/`, feature folders, or the `Account/` subtree. Locate by content/route (e.g. `grep -rln '@page "/dashboard"' src/`, `grep -rln 'class ApplicationUser' src/`) rather than hardcoded paths. If `Home.razor` no longer exists, find whatever component matches `@page "/"` and apply the redirect-fix intent there.
- **Prerequisite work already done.** Quick checks: does `_Imports.razor` already document the render-mode convention? Does the latest EF migration already constrain `DisplayName` to 128 chars (`grep -r 'DisplayName' src/CopilotBlazorTemplate.Core/Migrations/`)? Is `ApplicationUser` already `sealed`? Skip whatever is already in place and note the skip in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if `Dashboard.razor` already has `[StreamRendering]` and a working `PersistentComponentState` hookup, the goal is met; move on.
- If intent conflicts (e.g. someone deliberately removed the `ErrorBoundary` to debug a different problem), surface in the PR description; don't silently restore it.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Before editing, read each `.razor` file under `Components/Pages/`, `Components/Account/Pages/`, and `Components/Layout/`, plus `ApplicationUser.cs`. The snippets below describe the *intent* of each change — apply that intent to whatever the file currently contains. If a file no longer exists or has been moved (e.g. `Home.razor` replaced by a feature-folder landing), locate the new home by route or content and apply the equivalent change. Skip steps whose intent is already in place; note the skip in the PR description.
2. **Decide the convention** and write it as a comment block at the top of `Components/_Imports.razor` (or update the existing comment if one is already there):
   ```razor
   @* Render-mode convention:
        - Default is static SSR.
        - Add `@rendermode InteractiveServer` only on pages that need a circuit.
        - Account pages stay SSR (the Identity HTTP endpoints depend on it
          via `[ExcludeFromInteractiveRouting]` in Account/Pages/_Imports.razor).
   *@
   ```
3. **Add `@rendermode` to interactive pages.** Pages that need the circuit: any with `@onclick`, `EventCallback`, or `<EditForm>` that submits via the circuit. Inspect each page currently under `Components/Pages/` and `Components/Account/Pages/` and add `@rendermode InteractiveServer` only to pages that need it; leave the rest on SSR. Where a page already has an explicit `@rendermode`, leave it.
4. **Add `[StreamRendering]`** to any page that `await`s data during initialization (typically `Dashboard.razor`, `Admin.razor`, possibly `AuthenticatedLayout.razor`). Detect by reading `OnInitializedAsync` bodies for `await` calls. Add:
   ```razor
   @attribute [StreamRendering]
   ```
   to whatever components currently match; skip those that already have it.
5. **Extract `BlazorErrorUi.razor`:** if it does not already exist, create it in `Components/Layout/`:
   ```razor
   <div id="blazor-error-ui" data-nosnippet>
       An unhandled error has occurred.
       <a href="." class="reload">Reload</a>
       <span class="dismiss">🗙</span>
   </div>
   ```
   Wherever a layout currently inlines a `<div id="blazor-error-ui">` block, replace it with `<BlazorErrorUi />`.
6. **Wrap `@Body` in `<ErrorBoundary>`** in any layout that does not already do so:
   ```razor
   <ErrorBoundary>
       <ChildContent>@Body</ChildContent>
       <ErrorContent Context="ex">
           <p role="alert">Something went wrong. <a href="@NavigationManager.Uri">Retry</a></p>
       </ErrorContent>
   </ErrorBoundary>
   ```
   Inject `NavigationManager`.
7. **Fix the home-page navigation anti-pattern.** Wherever the project's home page currently calls `NavigationManager.NavigateTo("/dashboard")` from `OnInitializedAsync`, replace it with either a server-side redirect endpoint or a guard on `RendererInfo.IsInteractive`. The server-endpoint approach (preferred, append to whatever route-mapping chain `Program.cs` already has):
   ```csharp
   app.MapGet("/", (HttpContext ctx) =>
       ctx.User.Identity?.IsAuthenticated == true
           ? Results.LocalRedirect("/dashboard")
           : Results.Empty);
   ```
   Then simplify the home component to render the anonymous landing only. If task 03 has not shipped (so `Program.cs` is still in flux), instead guard the existing `OnInitializedAsync` with a `RendererInfo.IsInteractive` check and document in a comment.
8. **`PersistentComponentState`** — demonstrate on at least one prerender→interactive page (Dashboard or Admin):
   ```csharp
   @inject PersistentComponentState ApplicationState
   @implements IDisposable

   private PersistingComponentStateSubscription _persistingSubscription;
   private UserSummary? _summary;

   protected override async Task OnInitializedAsync()
   {
       _persistingSubscription = ApplicationState.RegisterOnPersisting(() =>
       {
           ApplicationState.PersistAsJson("dashboard-summary", _summary);
           return Task.CompletedTask;
       });
       if (!ApplicationState.TryTakeFromJson<UserSummary>("dashboard-summary", out _summary))
       {
           _summary = await LoadSummaryAsync(_cts.Token);
       }
   }
   public void Dispose() { _persistingSubscription.Dispose(); _cts.Cancel(); }
   ```
9. **CancellationToken flow:** every component that does async work in `OnInitializedAsync` (locate via `grep -rln 'protected override async Task OnInitializedAsync' src/CopilotBlazorTemplate.Web/Components/`) should hold a `CancellationTokenSource _cts = new();` field, cancel it in `Dispose()`, and pass `_cts.Token` to EF / UserManager / HTTP calls that accept one. Skip components that already do this.
10. **Identity pages CSS pass:** the goal is visual consistency between the Identity scaffold pages and the rest of the app. Pick whichever theme the rest of the app uses (the custom theme is recommended since `theme.css` already overrides `.btn-primary`). For each file under `Components/Account/Pages/`, swap the Bootstrap-only class names (`form-floating`, `form-control`, `btn btn-lg btn-primary`, `col-md-*`, `alert alert-*`) for the theme equivalents that have already been applied elsewhere (look at `Login.razor` as the reference if it has already been converted). Move inline `style="..."` blobs to CSS classes in `theme.css`. Preserve underlying HTML elements / `role` / accessible names so E2E selectors stay valid.
11. **ApplicationUser annotations:** wherever `ApplicationUser` is defined, ensure it is `sealed`, that `DisplayName` carries `[PersonalData]` and `[MaxLength(128)]`, and that the using directives include the right namespaces. Locate via `grep -rln 'class ApplicationUser' src/`:
    ```csharp
    public sealed class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [MaxLength(128)]
        public string DisplayName { get; set; } = string.Empty;
    }
    ```
12. **Generate migration:** if the latest migration does not already constrain `DisplayName`, generate one — `dotnet ef migrations add ApplicationUserDisplayNameMaxLength --project src/CopilotBlazorTemplate.Core --startup-project src/CopilotBlazorTemplate.Web` (adjust paths if the projects have been renamed). Inspect the generated migration to confirm it adds a `nvarchar(128)` (or SQLite equivalent) column constraint.
13. Run `dotnet build` and `dotnet test`. Adjust E2E tests only if a selector relied on a Bootstrap class.

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] The render-mode convention is documented (in `_Imports.razor` or an adjacent doc) and every page in the app either has an explicit `@rendermode` or is intentionally inheriting SSR per that convention.
- [ ] Pages that `await` during initialization render their initial markup before the data arrives (streaming rendering verified by reading the markup in a `view-source:` or by an integration test).
- [ ] Every routed layout wraps `@Body` (or equivalent slot) in an `<ErrorBoundary>` and reuses a single shared error-UI component (no inlined duplicate `<div id="blazor-error-ui">` blocks).
- [ ] The home page does NOT call `NavigationManager.NavigateTo` during prerender (either replaced by a server redirect endpoint or guarded on interactivity).
- [ ] At least one page demonstrates `PersistentComponentState` (data persists from prerender to interactive without a second round-trip).
- [ ] Components doing async work in `OnInitializedAsync` honour a `CancellationToken` that cancels on `Dispose`.
- [ ] `ApplicationUser` is `sealed` and `DisplayName` carries `[PersonalData]` and a max-length constraint of 128 (verifiable via reflection or by reading the latest migration).
- [ ] An EF migration applies the `DisplayName` column constraint to the database schema.
- [ ] Identity scaffold pages render with the same theme as the rest of the app — no Bootstrap-only class names remain unstyled.
- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` passes the full suite.

## References
- Audit cross-cutting theme CC-6 (render-mode strategy + PersistentComponentState + ErrorBoundary): `../../../docs/audits/2026-05-18/REPORT.md`.
- Code Quality findings "Render-mode strategy is global", "No `PersistentComponentState`, no streaming rendering", "Home.razor does post-render navigation", "No `ErrorBoundary` around routed content", "Scaffolded Identity pages still carry Bootstrap markup", "`ApplicationUser.DisplayName` lacks data annotations", "No cancellation tokens flowed into UI/data calls", "MainLayout duplicates error UI": `../../../docs/audits/2026-05-18/01-code-quality.md`.
- Blazor render modes — <https://learn.microsoft.com/aspnet/core/blazor/components/render-modes>
- Streaming rendering — <https://learn.microsoft.com/aspnet/core/blazor/components/rendering#streaming-rendering>
- `PersistentComponentState` — <https://learn.microsoft.com/aspnet/core/blazor/components/prerender#persist-prerendered-state>
- `ErrorBoundary` — <https://learn.microsoft.com/aspnet/core/blazor/fundamentals/handle-errors#error-boundaries>
- GDPR personal-data annotations — <https://learn.microsoft.com/aspnet/core/security/gdpr>
