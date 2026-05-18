# AGENTS.md — Copilot Blazor Template

## Project Structure

```
├── src/
│   ├── CopilotBlazorTemplate.Web/     # Blazor Web App (UI + Identity)
│   └── CopilotBlazorTemplate.Core/    # Domain entities, DbContext, data layer
├── tests/
│   ├── CopilotBlazorTemplate.UnitTests/   # xUnit unit tests
│   └── CopilotBlazorTemplate.E2ETests/    # Playwright E2E tests
├── docs/                               # Screenshots, demo
├── scripts/                            # Dev setup scripts
└── .github/                            # Workflows, instructions, agents
```

## Commands

| Action | Command |
|--------|---------|
| Build | `dotnet build` |
| Test (unit) | `dotnet test tests/CopilotBlazorTemplate.UnitTests/` |
| Test (E2E) | `dotnet test tests/CopilotBlazorTemplate.E2ETests/` |
| Test (all) | `dotnet test` |
| Run | `dotnet run --project src/CopilotBlazorTemplate.Web` |
| Format | `dotnet format` |
| EF Migration | `dotnet ef migrations add <Name> --project src/CopilotBlazorTemplate.Core --startup-project src/CopilotBlazorTemplate.Web` |
| EF Update DB | `dotnet ef database update --project src/CopilotBlazorTemplate.Core --startup-project src/CopilotBlazorTemplate.Web` |

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

1. **Add entities**: Create in `src/CopilotBlazorTemplate.Core/Entities/`, add DbSet to `AppDbContext`
2. **Add pages**: Create `.razor` files in `src/CopilotBlazorTemplate.Web/Components/Pages/`
3. **Add services**: Register in `Program.cs`, implement in Core project
4. **Add migrations**: Run EF migration command above after model changes

## Task Workflow

1. Create a branch from `main`
2. Make changes
3. Run `dotnet build` and `dotnet test`
4. Commit and push

## Browser Automation

- **Ad-hoc browser interaction** (debugging, exploring pages, screenshots): use the **Playwright MCP** server. It is auto-configured for the Copilot cloud agent and scoped to `localhost`/`127.0.0.1` by default. Do not install or invoke the standalone Playwright JS CLI.
- **E2E tests**: use `Microsoft.Playwright` (NuGet) inside `tests/CopilotBlazorTemplate.E2ETests/`. Browsers install via `pwsh bin/Release/net10.0/playwright.ps1 install --with-deps chromium` after `dotnet build`.
