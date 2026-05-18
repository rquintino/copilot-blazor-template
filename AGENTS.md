# AGENTS.md — Copilot Blazor Template

## Project Structure

```
├── src/
│   ├── BlazorDemo.Web/     # Blazor Web App (UI + Identity)
│   └── BlazorDemo.Core/    # Domain entities, DbContext, data layer
├── tests/
│   ├── BlazorDemo.UnitTests/   # xUnit unit tests
│   └── BlazorDemo.E2ETests/    # Playwright E2E tests
├── docs/                               # Screenshots, demo
├── scripts/                            # Dev setup scripts
└── .github/                            # Workflows, instructions, agents
```

## Commands

| Action | Command |
|--------|---------|
| Build | `dotnet build` |
| Test (unit) | `dotnet test tests/BlazorDemo.UnitTests/` |
| Test (E2E) | `dotnet test tests/BlazorDemo.E2ETests/` |
| Test (all) | `dotnet test` |
| Run | `dotnet run --project src/BlazorDemo.Web` |
| Format | `dotnet format` |
| EF Migration | `dotnet ef migrations add <Name> --project src/BlazorDemo.Core --startup-project src/BlazorDemo.Web` |
| EF Update DB | `dotnet ef database update --project src/BlazorDemo.Core --startup-project src/BlazorDemo.Web` |

## Seeded Credentials

| Email | Password | Role |
|-------|----------|------|
| admin@template.local | Admin123! | Admin |
| user@template.local | User123! | User |

## Conventions

- **TFM**: net10.0
- **Nullable**: enabled project-wide
- **File-scoped namespaces**: always
- **No Bootstrap**: use custom CSS with theme variables (see `wwwroot/css/theme.css`)
- **Identity**: ASP.NET Identity with cookie auth, no registration
- **Database**: SQLite (`app.db`), auto-migrated at startup
- **Render modes**: Static SSR for public pages, InteractiveServer for authenticated pages

## How to Extend

1. **Add entities**: Create in `src/BlazorDemo.Core/Entities/`, add DbSet to `AppDbContext`
2. **Add pages**: Create `.razor` files in `src/BlazorDemo.Web/Components/Pages/`
3. **Add services**: Register in `Program.cs`, implement in Core project
4. **Add migrations**: Run EF migration command above after model changes

## Task Workflow

1. Create a branch from `main`.
2. Make changes.
3. Run `dotnet build` and `dotnet test`.
4. **Commit. Do NOT `git push`.** In the Copilot coding-agent environment, `git push` fails at the credential layer — retrying with different tokens, `gh`, the API, or MCP all fail the same way. Commits accumulate locally with no friction; that's all you need to do.
5. **Open the PR with `gh pr create` at the very end.** This is the sole operation that publishes the branch and the commits in a single step. Do not run Copilot code review or CodeQL locally beforehand — both run automatically as PR checks once the PR exists, and running them mid-task tends to hang on the same missing-origin-branch credentials that block `git push`.

If you are a sub-agent delegated work by an orchestrator: the rules above apply to you regardless of how narrow your brief is. Do not push; do not run pre-PR validation tooling; commit and hand back. See `.github/skills/task-orchestration/SKILL.md` → Finalization protocol for the full rationale.

## Browser Automation

- **Ad-hoc browser interaction** (debugging, exploring pages, screenshots): use the **Playwright MCP** server. It is auto-configured for the Copilot cloud agent and scoped to `localhost`/`127.0.0.1` by default. Do not install or invoke the standalone Playwright JS CLI.
- **E2E tests**: use `Microsoft.Playwright` (NuGet) inside `tests/BlazorDemo.E2ETests/`. Browsers install via `pwsh bin/Release/net10.0/playwright.ps1 install --with-deps chromium` after `dotnet build`.
