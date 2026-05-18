# Startup & data hardening

## Goal
Tighten the app's startup pipeline and Identity defaults: gate seed + auto-migrate on Development, switch to async migration with logging, adopt `IDbContextFactory<AppDbContext>` (and kill the `Admin.razor` N+1), strengthen the Identity password / lockout policy, add a security-headers middleware, bound the Blazor circuit, pin a `__Host-`-prefixed auth cookie, and add a default-deny authorization fallback policy.

## Scope
- Gate `db.Database.Migrate()` + `SeedData.InitializeAsync` behind `app.Environment.IsDevelopment()` (or a `Database:AutoMigrate` config flag), switch to `MigrateAsync`, wrap with logging + try/catch.
- Remove the seeded-credential hint from `Login.razor` in non-Development environments.
- Switch DI from `AddDbContext<AppDbContext>` to `AddDbContextFactory<AppDbContext>`; refactor `Admin.razor` to a single projection with `AsNoTracking()` (fixes N+1).
- Configure `IdentityOptions`: `Password.RequiredLength = 12`, `Password.RequiredUniqueChars = 4`, `Lockout.AllowedForNewUsers = true`, `Lockout.MaxFailedAccessAttempts = 5`, `Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)`.
- Flip `Login.razor`'s `PasswordSignInAsync` call to `lockoutOnFailure: true`.
- Add a security-headers middleware before `UseAntiforgery` (X-Content-Type-Options, Referrer-Policy, Permissions-Policy, CSP `frame-ancestors 'self'`).
- Configure `CircuitOptions` and `HubOptions` to bound message size, retained circuits, and timeouts.
- Pin `ConfigureApplicationCookie` with `__Host-` prefixed name, `HttpOnly`, `SecurePolicy = Always`, `SameSite = Lax`, sliding 8 h expiration.
- Add `AddAuthorizationBuilder().SetFallbackPolicy(RequireAuthenticatedUser)` and apply `[AllowAnonymous]` to the public surface (`Home.razor`, `NotFound`, `Error`, all `/Account/*` pages).

Out of scope:
- Adding rate limiting (`AddRateLimiter`) — defer to a follow-up.
- MFA enforcement — defer.
- Switching to `AddIdentityCore` + `AddIdentityCookies` — defer (low severity).
- Component-level `ErrorBoundary` / `[StreamRendering]` / per-page `@rendermode` (owned by task 04).

## Edit zone
- `src/CopilotBlazorTemplate.Web/Program.cs`
- `src/CopilotBlazorTemplate.Web/Components/Account/Pages/Login.razor`
- `src/CopilotBlazorTemplate.Web/Components/Pages/Admin.razor`
- `src/CopilotBlazorTemplate.Web/Components/Account/Pages/Manage/ChangePassword.razor` (update `[StringLength]` MinimumLength to 12 to match)
- `src/CopilotBlazorTemplate.Web/SecurityHeadersMiddleware.cs` (new — small middleware class) **or** inline `app.Use(…)` in `Program.cs`
- `src/CopilotBlazorTemplate.Web/appsettings.json` (add a `Database:AutoMigrate` key, default `false`)
- `src/CopilotBlazorTemplate.Web/appsettings.Development.json` (add `Database:AutoMigrate = true`)

## Independence guarantee
- This task is the canonical owner of `Program.cs` and `Admin.razor` for this cycle. Other tasks should not modify these files; if they must, they append (a new middleware registration after the security-headers middleware, a new component on the Admin page).
- The `IDbContextFactory<AppDbContext>` switch may break any in-progress component or test that injects `AppDbContext` directly. If task 05 (bUnit) or task 06 (integration tests) ship first, their TASK.md instructs them to inject `IDbContextFactory<AppDbContext>` and `await using var db = await factory.CreateDbContextAsync()`. If those tasks already inject `AppDbContext`, this task updates those call sites as part of the refactor.
- The `[AllowAnonymous]` sweep touches the Account pages; if other tasks add new public pages, they must also add `[AllowAnonymous]`. The Independence guarantee in the other tasks notes this.
- The security-headers CSP `frame-ancestors 'self'` is intentionally minimal; if task 04 introduces an inline `<script>` or `<style>`, this task's CSP does not break it (CSP without `script-src` / `style-src` does not restrict those directives).
- If task 02 (CPM) has not yet shipped, this task does not introduce new NuGet packages, so the csproj edits are zero.

This task may sit in `backlog/` for weeks. By the time it is picked up the repo may have drifted significantly from the snapshot the audit captured. Handle the three drift modes explicitly:

- **File already changed by another task.** Read `Program.cs`, `Login.razor`, `Admin.razor`, etc. before editing. If `lockoutOnFailure: true` is already set, leave it. If `AddDbContextFactory<AppDbContext>` is already registered, only fix consumers. If `IdentityOptions` is already partially configured, additively merge the missing settings; don't replace the whole block. Never blindly overwrite work another task has shipped.
- **File moved/renamed.** `Admin.razor` may have been moved out of `Components/Pages/`; `SecurityHeadersMiddleware.cs` may already exist under a different folder. Locate by content/symbol search (e.g. `grep -rln 'PasswordSignInAsync' src/`, `grep -rln 'AddDbContext' src/`) rather than hardcoded paths. If `Home.razor` has been replaced by a different landing page, apply the `[AllowAnonymous]` rule to whatever the anonymous landing actually is.
- **Prerequisite work already done.** Quick checks: does `appsettings.json` already have a `Database:AutoMigrate` key? Does the cookie name already start with `__Host-`? Does `SetFallbackPolicy` already appear in `Program.cs`? Skip whatever is already in place and note the skip in the PR description.

### If you find related work already started
- Don't undo what's there if its intent matches this task — if someone already pinned the cookie name and `SecurePolicy.Always`, the goal is met; move on.
- If intent conflicts (e.g. someone deliberately set a shorter `ExpireTimeSpan` for security reasons), surface in the PR description; don't silently widen it back to 8 hours.
- Coordination happens via the PR description and the existing sticky CI comment, not via blocking dependencies between tasks.

## Steps
1. **Verify current state first.** Read `Program.cs`, `Login.razor`, `Admin.razor`, `ChangePassword.razor`, `ApplicationUser.cs`, `appsettings.json`, `appsettings.Development.json` end-to-end before editing. The snippets below describe the *intent*; apply that intent to whatever the files actually look like today. If a referenced file no longer exists or has been moved (e.g. `Admin.razor` migrated to a feature folder), locate the new home via `grep -rln` and apply the change there. If a particular intent is already in place from prior work, skip and note in the PR description.
2. **DI changes in `Program.cs`** — wherever the project registers the DbContext and Identity, apply the following intents (do not assume the existing code matches the snippets verbatim; merge the intent additively):
   - Wherever the project still registers a scoped `DbContext` (`AddDbContext<AppDbContext>` or similar), switch to `AddDbContextFactory<AppDbContext>` and update any consumers. Components that need a scoped `AppDbContext` (e.g. ASP.NET Identity stores) require both the factory and a scoped resolver:
     ```csharp
     builder.Services.AddDbContextFactory<AppDbContext>(options =>
         options.UseSqlite(connectionString));
     builder.Services.AddScoped(sp =>
         sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
     ```
     This keeps `AddEntityFrameworkStores<AppDbContext>()` working.
   - Configure `IdentityOptions`:
     ```csharp
     builder.Services.Configure<IdentityOptions>(o =>
     {
         o.Password.RequiredLength = 12;
         o.Password.RequiredUniqueChars = 4;
         o.Lockout.AllowedForNewUsers = true;
         o.Lockout.MaxFailedAccessAttempts = 5;
         o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
     });
     ```
   - Pin the application cookie:
     ```csharp
     builder.Services.ConfigureApplicationCookie(o =>
     {
         o.LoginPath = "/Account/Login";
         o.LogoutPath = "/Account/Logout";
         o.AccessDeniedPath = "/Account/AccessDenied";
         o.Cookie.HttpOnly = true;
         o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
         o.Cookie.SameSite = SameSiteMode.Lax;
         o.Cookie.Name = "__Host-CopilotBlazor.Auth";
         o.ExpireTimeSpan = TimeSpan.FromHours(8);
         o.SlidingExpiration = true;
     });
     ```
   - Add a default-deny fallback:
     ```csharp
     builder.Services.AddAuthorizationBuilder()
         .SetFallbackPolicy(new AuthorizationPolicyBuilder()
             .RequireAuthenticatedUser()
             .Build());
     ```
   - Bound the Blazor circuit:
     ```csharp
     builder.Services.AddRazorComponents()
         .AddInteractiveServerComponents()
         .AddHubOptions(o =>
         {
             o.MaximumReceiveMessageSize = 64 * 1024;
             o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
             o.KeepAliveInterval = TimeSpan.FromSeconds(15);
         });
     builder.Services.Configure<CircuitOptions>(o =>
     {
         o.DisconnectedCircuitMaxRetained = 100;
         o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
     });
     ```
3. **Gate migrations + seeding:** wherever the project currently calls `db.Database.Migrate()` and `SeedData.InitializeAsync(...)` at startup, replace that block with a guarded async equivalent. The intent:
   ```csharp
   var runMigrations = app.Environment.IsDevelopment()
       || app.Configuration.GetValue<bool>("Database:AutoMigrate");
   if (runMigrations)
   {
       using var scope = app.Services.CreateAsyncScope();
       var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
       try
       {
           var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
           logger.LogInformation("Applying database migrations…");
           await db.Database.MigrateAsync();
           logger.LogInformation("Seeding default users/roles…");
           await SeedData.InitializeAsync(scope.ServiceProvider);
       }
       catch (Exception ex)
       {
           logger.LogCritical(ex, "Database migration / seed failed at startup");
           throw;
       }
   }
   ```
   Add `"Database": { "AutoMigrate": false }` to `appsettings.json` and `"Database": { "AutoMigrate": true }` to `appsettings.Development.json`.
4. **Security-headers middleware:** if a security-headers middleware already exists somewhere under `src/CopilotBlazorTemplate.Web/`, additively extend it with any missing headers. Otherwise create `SecurityHeadersMiddleware.cs` (or inline an `app.Use(...)` in `Program.cs`):
   ```csharp
   internal static class SecurityHeadersMiddleware
   {
       public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
           app.Use(async (ctx, next) =>
           {
               var h = ctx.Response.Headers;
               h["X-Content-Type-Options"] = "nosniff";
               h["Referrer-Policy"] = "strict-origin-when-cross-origin";
               h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
               h["Content-Security-Policy"] = "frame-ancestors 'self'";
               await next();
           });
   }
   ```
   Register in the request pipeline between `UseHttpsRedirection` and `UseAntiforgery` (or wherever the equivalent middleware ordering lives today).
5. **Login.razor:** wherever the project calls `SignInManager.PasswordSignInAsync(...)`, ensure the `lockoutOnFailure` argument is `true`. Wherever the project renders a seeded-credential hint (whatever the current markup looks like), gate the render on `Environment.IsDevelopment` (inject `IWebHostEnvironment Environment`).
6. **Admin page refactor (kill N+1):** wherever the admin user list currently issues per-user role lookups, project it down to a single EF query with `AsNoTracking()`. Use a `DbFactory.CreateDbContextAsync()` if step 2 has shipped, otherwise the existing scoped `AppDbContext` is fine. Example shape:
   ```csharp
   @inject IDbContextFactory<AppDbContext> DbFactory
   ...
   await using var db = await DbFactory.CreateDbContextAsync();
   users = await db.Users
       .AsNoTracking()
       .Select(u => new UserVm
       {
           Id = u.Id,
           Email = u.Email,
           DisplayName = u.DisplayName,
           Roles = db.UserRoles
                     .Where(r => r.UserId == u.Id)
                     .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                     .ToList()
       })
       .ToListAsync();
   ```
7. **Change-password form:** wherever the change-password form validates `NewPassword`, raise the `MinimumLength` to match the `IdentityOptions.Password.RequiredLength` set in step 2 (12). This may be a `[StringLength]` attribute, a FluentValidation rule, or similar — apply to whatever validator is in use today.
8. **AllowAnonymous sweep:** every page that should be reachable while signed-out must carry `@attribute [AllowAnonymous]` (or the equivalent for the routing model in use). Locate signed-out-reachable pages by inspecting `Components/Pages/` and `Components/Account/Pages/` (plus the `_Imports.razor` files) — the set typically includes the home/landing, `NotFound`, `Error`, and all Account pages (`Login`, `Register`, `ForgotPassword`, `ResetPassword`, `ResetPasswordConfirmation`, `RegisterConfirmation`, `ConfirmEmail`, `Lockout`, `AccessDenied`, `ExternalLogin`, `ForgotPasswordConfirmation`, `LoginWith2fa`, `LoginWithRecoveryCode`). Using `_Imports.razor` in `Components/Account/Pages/` to apply globally is acceptable. Add the attribute to whichever files currently lack it; skip files that already have it.
9. Run `dotnet build` and `dotnet test`. Fix any regressions (E2E tests may need adjustment if cookie name changed — the existing storage-state cache will need to be invalidated; the `PlaywrightFixture` already isolates per-run state).

## Acceptance criteria
Expressed as outcomes, not exact file contents.

- [ ] Booting the app in `Production` without `Database:AutoMigrate=true` does NOT run `Database.Migrate()` or `SeedData.InitializeAsync()` (verify by inspection or by a quick run with `ASPNETCORE_ENVIRONMENT=Production`).
- [ ] Booting in `Development` (or with the flag on) still applies migrations and seeds; failures are logged and rethrown.
- [ ] After 5 bad logins for the same account, the 6th attempt is rejected with a lockout response, not "wrong password".
- [ ] The seeded-credential hint on the login page only appears in Development.
- [ ] `IdentityOptions` enforces minimum password length 12 and a 5-attempt / 15-minute lockout (verifiable from `Program.cs` or an integration test).
- [ ] DI exposes `IDbContextFactory<AppDbContext>` to consumers.
- [ ] The Admin user-list page issues exactly one EF query for the full result (verify with `Microsoft.EntityFrameworkCore.Database.Command` logging at Information level).
- [ ] Responses carry `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy`, and `Content-Security-Policy: frame-ancestors 'self'` on at least one route (`curl -I`).
- [ ] `CircuitOptions` retention is bounded (max retained circuits set; retention period set) and `HubOptions.MaximumReceiveMessageSize` is configured.
- [ ] The application cookie uses a `__Host-`-prefixed name with `HttpOnly`, `Secure`, and a sliding ~8h expiration.
- [ ] A fallback authorization policy requires authenticated users by default; every page reachable while signed-out carries `[AllowAnonymous]`.
- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` passes the full suite.

## References
- Audit cross-cutting themes CC-4 (seeded creds + auto-migrate), CC-5 (DbContext lifetime + N+1): `../../../docs/audits/2026-05-18/REPORT.md`.
- Code Quality findings "No `IDbContextFactory<AppDbContext>`", "Auto-migration and seeding run unguarded", "`Admin.razor` runs N+1 role lookups": `../../../docs/audits/2026-05-18/01-code-quality.md`.
- Security findings "Demo credentials hardcoded", "No account lockout", "Default Identity password policy is too weak", "No security response headers", "No DoS / circuit limits", "Application cookie not explicitly hardened", "EF Core `Migrate()` runs at startup in every environment", "No `FallbackPolicy` / default-deny authorization": `../../../docs/audits/2026-05-18/03-security.md`.
- Microsoft: Blazor + EF Core lifetime — <https://learn.microsoft.com/aspnet/core/blazor/blazor-ef-core>
- Microsoft: Threat mitigation for Blazor Server — <https://learn.microsoft.com/aspnet/core/blazor/security/server/threat-mitigation>
- NIST SP 800-63B — <https://pages.nist.gov/800-63-3/sp800-63b.html>
- MDN: `__Host-` cookie prefix — <https://developer.mozilla.org/docs/Web/HTTP/Headers/Set-Cookie#cookie_prefixes>
