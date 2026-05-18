# Security Audit

## Summary

This is a small .NET 10 Blazor Web App starter that leans heavily on the stock ASP.NET Identity scaffold. The core auth machinery is wired correctly — `IdentityRevalidatingAuthenticationStateProvider`, `[Authorize]` on `/admin` and `/dashboard`, the new WebAuthn/passkey endpoints with antiforgery validation, an `AuthorizeRouteView` with a login redirect, and recent hardening of logout (antiforgery + relative `returnUrl`). NuGet auditing and lockfiles are on, which is a strong supply-chain baseline.

However, the template ships several configurations that are **fine for a local demo but are footguns the moment someone clones this into production**: hardcoded weak seeded credentials with `EmailConfirmed = true` and `RequireConfirmedAccount = true` (a contradiction that hides a configuration smell), `lockoutOnFailure: false` on the login form (no brute-force throttling), default Identity password policy (6 chars, no complexity), no MFA enforcement, no security-response headers (CSP / Permissions-Policy / Referrer-Policy / X-Content-Type-Options), no Blazor Server circuit DoS limits, an auto-`Migrate()` at startup, demo credentials rendered into the login page HTML, no CodeQL/SAST in CI, and unpinned GitHub Actions (tag refs instead of SHAs). None of these are unusual for a template, but the README and `Program.cs` should explicitly flag the prod-hardening checklist.

Verdict: **good template scaffolding, NOT production-ready as-is**. Roughly 4-6 hours of focused work would close the most important gaps without changing the developer experience.

## Strengths

- **NuGet supply chain**: `NuGetAudit=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=low`, `RestorePackagesWithLockFile=true` in `Directory.Build.props` — best-practice baseline.
- **Lockfiles checked in** (`packages.lock.json` in both projects).
- **Dependabot enabled** for both `nuget` and `npm` ecosystems with weekly cadence.
- **CI permissions are scoped** (`contents: read`, `pull-requests: write`) — no blanket `write-all`.
- **Antiforgery is enabled and ordered correctly**: `app.UseAntiforgery()` runs after `UseHttpsRedirection` and before component mapping (`Program.cs:63`).
- **Logout endpoint requires antiforgery** (recent hardening) and uses `TypedResults.LocalRedirect` to constrain `returnUrl` (`IdentityComponentsEndpointRouteBuilderExtensions.cs:44-51`).
- **Passkey endpoints explicitly call `antiforgery.ValidateRequestAsync`** (`IdentityComponentsEndpointRouteBuilderExtensions.cs:59,85`).
- **`/Account/Manage/*` group has `.RequireAuthorization()`** applied at the group level (`IdentityComponentsEndpointRouteBuilderExtensions.cs:92`) — default-deny on management endpoints.
- **`IdentityRevalidatingAuthenticationStateProvider` is registered** so InteractiveServer circuits revalidate the security stamp every 30 minutes (`Program.cs:17`).
- **`SignIn.RequireConfirmedAccount = true`** is set (`Program.cs:27`).
- **HSTS and `UseExceptionHandler("/Error")` enabled** in non-Development (`Program.cs:56-58`); `HttpsRedirection` enabled unconditionally (`Program.cs:61`).
- **`IdentityRedirectManager` blocks open redirects** by forcing non-relative URIs through `ToBaseRelativePath` (`IdentityRedirectManager.cs:24-27`).
- **No use of `MarkupString`** anywhere in the codebase (zero hits) — XSS surface from raw HTML injection is essentially nil.
- **No raw SQL** (`FromSqlRaw` / `ExecuteSqlRaw`) — all DB access goes through EF Core's parameterized translator.
- **Status cookie is locked down**: `SameSite=Strict`, `HttpOnly`, 5-second `MaxAge` (`IdentityRedirectManager.cs:11-17`).
- **`.gitignore` excludes `*.db`, `*.db-shm`, `*.db-wal`** so the SQLite file with seeded password hashes never gets committed.
- **Passkey list is capped** at 100 per user (`Passkeys.razor:63`) — prevents trivial enumeration/abuse via unbounded credential addition.
- **`.npmrc` sets `min-release-age=15`** — supply-chain cool-down on npm deps (good defensive posture, exceeds 7-day rule).

## Findings

### [Severity: High] Demo credentials hardcoded and seeded on every startup

**Where:** `src/CopilotBlazorTemplate.Core/Data/SeedData.cs:25-50`, displayed on `Components/Account/Pages/Login.razor:44-48`
**Current:** `admin@template.local / Admin123!` and `user@template.local / User123!` are unconditionally created on every app boot, with `EmailConfirmed = true`, in any environment (Production included). The Login page renders the credentials verbatim in HTML.
**Recommended:**
1. Gate `SeedData.InitializeAsync` behind `app.Environment.IsDevelopment()` or an explicit `Seed:Enabled` config flag.
2. Read seed passwords from `IConfiguration` / user-secrets / env vars; never hardcode.
3. Remove the credential hint from `Login.razor` for any non-Development environment (`@if (Environment.IsDevelopment)` guard).
4. Add a startup check that refuses to boot in Production if any seeded user still has a default password.
**Why:** If anyone deploys the template unchanged, attackers know the admin credentials before login. The credentials are also visible in repo history and on the live login page.
**Effort:** S

### [Severity: High] No account lockout on failed password attempts

**Where:** `src/CopilotBlazorTemplate.Web/Components/Account/Pages/Login.razor:101`
**Current:** `await SignInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);`
**Recommended:** Set `lockoutOnFailure: true`, and configure the Identity options explicitly:
```csharp
builder.Services.Configure<IdentityOptions>(o =>
{
    o.Lockout.AllowedForNewUsers = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});
```
Combine with an IP-level rate limiter (`AddRateLimiter` with a fixed-window policy on `/Account/Login`) to prevent username enumeration via timing & lockout-bypass via attacker rotation.
**Why:** Without lockout the template invites credential-stuffing and password-spray attacks. OWASP ASVS V2.2.1 / V11.1 require throttling.
**Effort:** S

### [Severity: High] Default Identity password policy is too weak

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs:25-30` — only `RequireConfirmedAccount` is configured; `IdentityOptions.Password` is left at defaults (6 chars, requires digit/lowercase/uppercase/non-alphanumeric — but length is the weakest knob). Reinforced by `ChangePassword.razor:99` which validates min length 6.
**Current:** Effectively a 6-char minimum.
**Recommended:** Configure to NIST SP 800-63B aligned policy:
```csharp
o.Password.RequiredLength = 12;
o.Password.RequiredUniqueChars = 4;
// Keep the complexity requirements on or off based on policy;
// length matters far more than character classes.
```
Update the validation attribute on `ChangePassword.InputModel.NewPassword` to match.
**Why:** A 6-char minimum is well below 2017 NIST guidance, never mind 2025. Combined with no lockout this is a serious weakness.
**Effort:** S

### [Severity: High] No security response headers (CSP, Permissions-Policy, etc.)

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs` (entire pipeline)
**Current:** No middleware adds Content-Security-Policy, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, or X-Frame-Options. Blazor Web App framework adds none by default.
**Recommended:** Add a small headers middleware (or `NetEscapades.AspNetCore.SecurityHeaders`) registered before `UseAntiforgery`:
```csharp
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    // CSP needs tuning for Blazor (inline script for WebAssembly bootstrap + nonce for blazor.web.js).
    // Start with frame-ancestors to lock clickjacking even before full CSP:
    h["Content-Security-Policy"] = "frame-ancestors 'self'";
    await next();
});
```
Iterate the CSP with nonce-based `script-src` once the team is ready (Blazor's bootstrap can be nonced; the `PasskeySubmit.razor.js` module would need to be allowed).
**Why:** Defense-in-depth against XSS, clickjacking, MIME-sniffing, and feature-policy abuse. OWASP Top 10 2021 A05 "Security Misconfiguration".
**Effort:** M (CSP tuning takes iteration with Blazor's framework-injected scripts)

### [Severity: High] No DoS / circuit limits configured for Blazor Server

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs:12-13`
**Current:** `AddInteractiveServerComponents()` is called with no `CircuitOptions` or `HubOptions` overrides. Defaults allow large messages and long disconnected-circuit retention.
**Recommended:**
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o =>
    {
        o.MaximumReceiveMessageSize = 64 * 1024; // 64 KB
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    });

builder.Services.Configure<CircuitOptions>(o =>
{
    o.DisconnectedCircuitMaxRetained = 100;
    o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
    o.DetailedErrors = false; // already default in non-Dev
});
```
Also enable ASP.NET Core rate limiting (`builder.Services.AddRateLimiter(...)`, `app.UseRateLimiter()`) on `/Account/*` endpoints.
**Why:** Blazor Server circuits are an attractive DoS target (per-circuit memory + SignalR connection). Without bounds an attacker can open thousands of circuits and exhaust the server. Microsoft explicitly calls this out in the Blazor security docs.
**Effort:** S

### [Severity: Med] Application cookie not explicitly hardened

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs:32-35`
**Current:** Only `LoginPath` is set; everything else inherits Identity defaults.
**Recommended:** Pin the security-relevant knobs explicitly so they cannot drift:
```csharp
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax; // Strict breaks OAuth callbacks
    o.Cookie.Name = "__Host-CopilotBlazor.Auth"; // __Host- prefix locks domain
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
```
The Identity defaults are mostly correct in .NET 10, but pinning protects against framework default changes and accidental misconfiguration.
**Why:** Defense in depth; the `__Host-` prefix makes the cookie strictly host-scoped (no domain attribute, secure, path=/), eliminating subdomain cookie shadowing.
**Effort:** S

### [Severity: Med] `RequireConfirmedAccount = true` combined with `IdentityNoOpEmailSender` blocks all new accounts (and contradicts the seed flow)

**Where:** `Program.cs:27`, `Components/Account/IdentityNoOpEmailSender.cs`, `Core/Data/SeedData.cs:32,46`
**Current:** Email confirmation is required, but the email sender is a no-op that just calls `NoOpEmailSender.SendEmailAsync` (which does nothing). The seed gets around this by forcing `EmailConfirmed = true`. A real user signing up has no way to confirm — the registration scaffold is intentionally absent, but the option is left on and would block any future opt-in.
**Recommended:**
1. Decide and document: "Template ships with registration disabled; confirmation flow assumes real `IEmailSender<ApplicationUser>` is wired before enabling registration." Add a `README.md` Security section.
2. When enabling registration, replace `IdentityNoOpEmailSender` with a real provider (SendGrid/SES/SMTP) configured via user-secrets/env.
3. Until then, also remove the `RegisterConfirmation` and `ResetPassword` pages from the deployed surface, or document that they will silently fail.
**Why:** A no-op email sender is a footgun — password resets and email confirmations silently fail in prod, creating both UX and security incidents (users locked out, or worse, an admin assumes resets work and rolls a password they think they emailed).
**Effort:** S

### [Severity: Med] EF Core `Migrate()` runs at startup in every environment

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs:44-46`
**Current:**
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SeedData.InitializeAsync(scope.ServiceProvider);
}
```
**Recommended:**
- For prod, run migrations as a deliberate deployment step (`dotnet ef database update` in CI/CD, or a dedicated migration container/job).
- Gate the in-process `Migrate()` call behind `IsDevelopment()` or a `Migrations:RunOnStartup` config flag.
- Apply the principle of least privilege: the runtime DB principal should *not* have DDL rights in prod; only the migration job principal should.
**Why:** Auto-migration on startup means (a) the app needs schema-altering DB permissions at runtime — a large blast radius if compromised, (b) rolling deployments can produce two app versions hitting an in-flight schema change, (c) a successful auth-bypass turns into an RCE-adjacent surface via DB privilege.
**Effort:** S

### [Severity: Med] GitHub Actions are pinned by tag, not SHA

**Where:** `.github/workflows/ci.yml:17,19,23,74,100,109`, `.github/workflows/copilot-setup-steps.yml:8,9,12`
**Current:** `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`, `actions/github-script@v7`, `actions/upload-artifact@v4`. Tag refs are mutable.
**Recommended:** Pin to commit SHAs (Dependabot will keep them current):
```yaml
- uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1
```
Dependabot in `.github/dependabot.yml` should additionally watch `github-actions`:
```yaml
- package-ecosystem: "github-actions"
  directory: "/"
  schedule: { interval: "weekly" }
```
**Why:** A tag can be force-pushed by a compromised maintainer; SHAs are immutable. CISA and GitHub explicitly recommend SHA-pinning for third-party actions. Note the current actions are all first-party Microsoft/GitHub, lowering the immediate risk, but `actions/github-script` runs arbitrary JS with `GITHUB_TOKEN`.
**Effort:** S

### [Severity: Med] Dependabot does not cover GitHub Actions ecosystem

**Where:** `.github/dependabot.yml`
**Current:** Only `nuget` and `npm` are configured.
**Recommended:** Add `github-actions` (and arguably `docker` if any Dockerfile is added later). See snippet above.
**Why:** Action versions never get patched otherwise; combined with the unpinned tags above, this widens the supply-chain window.
**Effort:** XS

### [Severity: Med] No SAST/CodeQL or secret-scanning in CI

**Where:** `.github/workflows/ci.yml` (no codeql.yml present)
**Current:** Build + tests only. No CodeQL analysis, no `gitleaks`/`trufflehog`, no `dotnet list package --vulnerable --include-transitive` step as a hard gate.
**Recommended:** Add a `codeql.yml` (GitHub-provided starter for C# + JavaScript) and an explicit step in CI:
```yaml
- name: NuGet vulnerable packages
  run: dotnet list package --vulnerable --include-transitive
```
GitHub also offers free secret scanning + push protection on public repos — enable in repo settings.
**Why:** NuGetAudit at build time catches some, but CodeQL covers code-level CWEs (taint, deserialization, XSS sinks) the auditor doesn't.
**Effort:** S

### [Severity: Med] No `FallbackPolicy` / default-deny authorization

**Where:** `src/CopilotBlazorTemplate.Web/Program.cs` (no `AddAuthorizationBuilder` call)
**Current:** Authorization is per-page (`[Authorize]` on Dashboard and Admin). Any future page that forgets the attribute is anonymous-by-default.
**Recommended:**
```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
// Mark anonymous endpoints (Home, Login, Error, NotFound, ForgotPassword, etc.) with [AllowAnonymous].
```
Also prefer policy-based authorization for roles long-term (`.RequireRole("Admin")` -> named policy `"AdminOnly"`).
**Why:** Default-deny is the OWASP-recommended posture (Top 10 2021 A01 "Broken Access Control"). It catches the "I forgot the attribute" class of regression.
**Effort:** M (need to audit and `[AllowAnonymous]`-tag the public surface, especially all `/Account/*` pages)

### [Severity: Low] `IdentityRedirectManager.RedirectTo` open-redirect guard is permissive

**Where:** `src/CopilotBlazorTemplate.Web/Components/Account/IdentityRedirectManager.cs:19-30`
**Current:** Only checks `Uri.IsWellFormedUriString(uri, UriKind.Relative)`. A protocol-relative URL like `//evil.com/path` is treated as relative by .NET URI parsing in some cases, and the subsequent `ToBaseRelativePath` would only neutralize it if the input parses as absolute.
**Recommended:** Add an explicit guard:
```csharp
if (uri.StartsWith("//", StringComparison.Ordinal) ||
    uri.Contains(":", StringComparison.Ordinal))
{
    uri = "/";
}
```
Or, more cleanly, use `Url.IsLocalUrl(uri)` (MVC helper) / mirror its logic.
**Why:** Open redirect → phishing pivot. The logout endpoint already uses `TypedResults.LocalRedirect` (good), but in-process redirects via this helper don't have the same guard.
**Effort:** XS

### [Severity: Low] `RedirectToLogin` builds returnUrl from full URI without sanitization

**Where:** `src/CopilotBlazorTemplate.Web/Components/Account/Shared/RedirectToLogin.razor:6`
**Current:** `NavigationManager.NavigateTo($"Account/Login?returnUrl={Uri.EscapeDataString(NavigationManager.Uri)}", forceLoad: true);` — passes the *absolute* current URI as `returnUrl`. The Login page then hands it to `RedirectManager.RedirectTo` which has the (weak) open-redirect guard above.
**Recommended:** Use `NavigationManager.ToBaseRelativePath(NavigationManager.Uri)` so only the path+query is sent.
**Why:** Tightens the open-redirect attack surface and produces cleaner login URLs.
**Effort:** XS

### [Severity: Low] No MFA enforced; recovery codes / TOTP scaffolding present but optional

**Where:** `Components/Account/Pages/Manage/EnableAuthenticator.razor`, `LoginWith2fa.razor` — Identity defaults
**Current:** TOTP is available, but not required for the `Admin` role.
**Recommended:** For prod, add a policy-driven MFA requirement at least for users in `Admin`:
- Custom `IUserClaimsPrincipalFactory<ApplicationUser>` that adds an `mfa` claim if `await UserManager.GetTwoFactorEnabledAsync(user)`.
- Authorization policy `"AdminWithMfa"` requiring both role and claim.
- Or a middleware that redirects authenticated admins without MFA to `/Account/Manage/EnableAuthenticator`.
**Why:** Admin accounts are high-value; MFA is the highest-ROI single control (per Microsoft / CISA guidance).
**Effort:** M

### [Severity: Low] `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` referenced in Web project

**Where:** `src/CopilotBlazorTemplate.Web/CopilotBlazorTemplate.Web.csproj:12`, used at `Program.cs:23` (`AddDatabaseDeveloperPageExceptionFilter`) and `Program.cs:52` (`UseMigrationsEndPoint`).
**Current:** Both calls are inside `if (app.Environment.IsDevelopment())` — that's correct. However `AddDatabaseDeveloperPageExceptionFilter()` is registered unconditionally on line 23. The filter only *activates* under Development per its implementation, so this is fine in practice, but the package ships diagnostics endpoints whose UI leaks schema info if env is misconfigured.
**Recommended:** Move both the `AddDatabaseDeveloperPageExceptionFilter` registration *and* package reference behind a Development-only compile/runtime guard, or accept the risk and add a comment. Optionally `<PackageReference ... Condition="'$(Configuration)' == 'Debug'">`.
**Why:** Prevents accidental disclosure if `ASPNETCORE_ENVIRONMENT` is wrong in prod.
**Effort:** XS

### [Severity: Low] `RedirectToLogin` uses `forceLoad: true` (correct) but the InteractiveServer mode never has cookies issued mid-circuit

**Where:** `Routes.razor` + `RedirectToLogin.razor`
**Current:** This is actually working as intended — cookie auth must happen on a full POST, not over the SignalR circuit, and `forceLoad: true` ensures the navigation breaks out of the circuit. Good. Just calling it out to confirm.
**Recommended:** No change. Document in `AGENTS.md` so future agents don't "optimize" away the `forceLoad`.
**Why:** Removing `forceLoad` would cause auth state mismatch between circuit and HTTP, a subtle and dangerous regression.
**Effort:** XS (docs only)

### [Severity: Low] SQLite file path / permissions in prod

**Where:** `appsettings.json:3` — `"DataSource=Data/app.db;Cache=Shared"`
**Current:** Relative path under the app's working directory; whatever umask/uid the host runs under determines who can read the file. Contains all user password hashes.
**Recommended:** For prod: move `app.db` out of the deployment directory (`/var/lib/copilot-blazor/app.db`), chown to the service user, chmod 600. Better: use a real RDBMS (Postgres/SQL Server) for prod and keep SQLite as the dev driver via configuration. Document this in the template's deployment guide.
**Why:** Anyone who reads `app.db` reads every password hash. PBKDF2 will slow brute force but won't stop it.
**Effort:** S (mostly docs / sample compose file)

### [Severity: Low] `BlazorDisableThrowNavigationException` is set — confirm intent

**Where:** `src/CopilotBlazorTemplate.Web/CopilotBlazorTemplate.Web.csproj:8`
**Current:** `<BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>`
**Recommended:** Not a security finding per se, but verify the redirect-as-exception pattern isn't being relied on for control flow in any auth code path. The `IdentityRedirectManager` uses `NavigateTo` and that's fine. Document the setting.
**Why:** Future maintainers may write `NavigateTo("/login")` expecting it to short-circuit execution; with this setting it does *not*, which can let post-redirect code run and leak data or perform unintended writes.
**Effort:** XS (audit + docs)

## Threat model snapshot

| Asset                       | Threat                                               | Mitigation status                                                                          |
| --------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Admin account               | Credential stuffing / password spray                 | **WEAK** — no lockout, 6-char pw, seeded `Admin123!` published in login UI                 |
| Admin account               | Phishing / session theft                             | Partial — cookie HttpOnly default; no MFA; no `__Host-` prefix                             |
| Auth cookie                 | XSS exfiltration                                     | Partial — no `MarkupString`, but no CSP                                                    |
| User session                | CSRF                                                 | **STRONG** — antiforgery on all mutating endpoints, logout hardened                        |
| Login flow                  | Open redirect via `returnUrl`                        | Mostly OK — `LocalRedirect` on logout, `IsWellFormedUriString` check (weak) on others      |
| Blazor circuit              | DoS via circuit flooding / large messages            | **NONE** — defaults, no rate limit, no circuit caps                                        |
| Blazor circuit              | Cross-tab session takeover after sign-out elsewhere  | OK — `IdentityRevalidatingAuthenticationStateProvider` revalidates security stamp (30 min) |
| DB password hashes          | Disclosure via file read                             | Partial — `.gitignore` excludes `app.db`; no file-permission guidance for prod             |
| DB schema                   | Privilege escalation via runtime DDL                 | **WEAK** — auto-migrate at startup; runtime principal needs DDL                            |
| Supply chain (NuGet)        | Malicious package via lockfile drift                 | **STRONG** — `NuGetAudit=all`, lockfile, Dependabot                                        |
| Supply chain (npm)          | Malicious package, brand-new                         | **STRONG** — `min-release-age=15`                                                          |
| Supply chain (GH Actions)   | Tag hijack on third-party action                     | Partial — first-party only today; tags not SHAs; no Dependabot coverage                    |
| Frontend (clickjacking, XSS sinks) | Iframe-based hijack / inline script injection | **WEAK** — no CSP, no `frame-ancestors`, no `X-Content-Type-Options`                       |
| Error pages                 | Stack trace / schema disclosure                      | OK in non-Dev — `UseExceptionHandler("/Error")`; Error.razor is plain                      |
| PII (email, phone)          | Logging / disclosure                                 | OK — no obvious PII in logs; download-personal-data endpoint requires auth                 |

## Quick wins (top 5)

1. **Flip `lockoutOnFailure: true`** in `Login.razor:101` and configure 5-attempt / 15-min lockout in `Program.cs`. Add `AddRateLimiter` on `/Account/Login`. (~30 min)
2. **Add a security-headers middleware** in `Program.cs` (X-Content-Type-Options, Referrer-Policy, Permissions-Policy, CSP `frame-ancestors 'self'` to start). (~30 min)
3. **Gate `SeedData.InitializeAsync` + the credential hint on `Login.razor`** behind `IsDevelopment()`, and add a README warning that seed creds must be removed before any deployment. (~30 min)
4. **Configure circuit/hub options** to bound `MaximumReceiveMessageSize` and `DisconnectedCircuitMaxRetained`. (~15 min)
5. **Bump `Password.RequiredLength` to 12** in `Program.cs` and update the `[StringLength(MinimumLength = ...)]` in `ChangePassword.razor` to match. (~15 min)

Bonus / next sprint: pin Actions to SHAs + add `github-actions` to Dependabot + add CodeQL workflow (~1 hr total).

## References

- **OWASP ASVS 4.0.3** — V2 (Auth), V3 (Session), V4 (Access Control), V11 (Business Logic) — https://owasp.org/www-project-application-security-verification-standard/
- **OWASP Top 10 2021** — A01 Broken Access Control, A02 Crypto Failures, A05 Security Misconfiguration, A07 Identification & Auth Failures — https://owasp.org/Top10/
- **Microsoft: ASP.NET Core Blazor authentication & authorization** — https://learn.microsoft.com/aspnet/core/blazor/security/
- **Microsoft: Threat mitigation guidance for ASP.NET Core Blazor Server** — https://learn.microsoft.com/aspnet/core/blazor/security/server/threat-mitigation
- **Microsoft: Cookie authentication** — https://learn.microsoft.com/aspnet/core/security/authentication/cookie
- **Microsoft: Account lockout** — https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration#lockout
- **Microsoft: ASP.NET Core Rate Limiting middleware** — https://learn.microsoft.com/aspnet/core/performance/rate-limit
- **MDN: `__Host-` cookie prefix** — https://developer.mozilla.org/docs/Web/HTTP/Headers/Set-Cookie#cookie_prefixes
- **NIST SP 800-63B** — Digital Identity Guidelines (password length > complexity) — https://pages.nist.gov/800-63-3/sp800-63b.html
- **CISA / GitHub: Pin GitHub Actions to commit SHA** — https://docs.github.com/actions/security-guides/security-hardening-for-github-actions#using-third-party-actions
- **W3C WebAuthn Level 3** — https://www.w3.org/TR/webauthn-3/
- **OWASP Cheat Sheet: Content Security Policy** — https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html
- **OWASP Cheat Sheet: SQL Injection Prevention (EF Core context)** — https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html
