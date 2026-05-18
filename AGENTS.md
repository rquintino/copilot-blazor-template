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

## Skills

Inspect `.github/skills/<name>/SKILL.md` and invoke when the trigger fires:

- **bootstrap-new-app** — FIRST action when `grep -rIlq CopilotBlazorTemplate . --exclude-dir={.git,bin,obj,node_modules}` finds matches AND the user is asking for a new app/feature/domain. Renames the template via `scripts/init-app.sh` before any other work.
- **task-orchestration** — Move existing tasks through `tasks/backlog/ → current/ → done/`. Main agent does NOT create persisted tasks; sub-step decomposition uses the built-in todo tool.
- **validator** — Run after each phase, before moving a task to `done/`.
- **screenshots-demo** — When UI pages were added or changed.
- **playwright-e2e** — When authoring or maintaining E2E tests under `tests/CopilotBlazorTemplate.E2ETests/`.

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

1. Create a branch from `main`.
2. Make changes.
3. Run `dotnet build` and `dotnet test`.
4. **Commit. Do NOT `git push`.** In the Copilot coding-agent environment, `git push` fails at the credential layer — retrying with different tokens, `gh`, the API, or MCP all fail the same way. Commits accumulate locally with no friction; that's all you need to do.
5. **Open the PR with `gh pr create` at the very end.** This is the sole operation that publishes the branch and the commits in a single step. Do not run Copilot code review or CodeQL locally beforehand — both run automatically as PR checks once the PR exists, and running them mid-task tends to hang on the same missing-origin-branch credentials that block `git push`.

If you are a sub-agent delegated work by an orchestrator: the rules above apply to you regardless of how narrow your brief is. Do not push; do not run pre-PR validation tooling; commit and hand back. See `.github/skills/task-orchestration/SKILL.md` → Finalization protocol for the full rationale.

## Browser Automation

Pick the tool by **what you're doing**, not by what's available:

- **Screenshot / demo-video capture (batch):** `bash scripts/demo.sh`. **Do not** use Playwright MCP for this. The Copilot cloud agent's managed Playwright MCP holds a singleton chromium profile and, once touched, returns `Browser is already in use for /root/.cache/ms-playwright/mcp-chrome, use --isolated`. The suggested `--isolated` flag is **not** user-passable in the managed MCP, and the failure is **not** recoverable from inside the agent — clearing `SingletonLock`, `pkill`-ing chromium, or wiping the profile dir all do nothing (the MCP server tracks state in-process and `pkill` is sandbox-blocked anyway). `scripts/demo.sh` uses an independent npm-Playwright install under `/tmp/pw-runner` (pre-installed by `.github/workflows/copilot-setup-steps.yml`) and is unaffected. See `.github/skills/screenshots-demo/SKILL.md`.
- **Ad-hoc single-page inspection** (pre-flight verification of one page, debugging a render issue): Playwright MCP is fine, one page at a time. It is auto-configured for the Copilot cloud agent and scoped to `localhost`/`127.0.0.1`. Do not install or invoke the standalone Playwright JS CLI.
- **E2E tests:** `Microsoft.Playwright` (NuGet) inside `tests/CopilotBlazorTemplate.E2ETests/`. Browsers install via `pwsh bin/Release/net10.0/playwright.ps1 install --with-deps chromium` after `dotnet build`.
