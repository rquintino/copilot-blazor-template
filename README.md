# 🚀 Copilot Blazor Template

[![CI](https://github.com/rquintino/copilot-blazor-template/actions/workflows/ci.yml/badge.svg)](https://github.com/rquintino/copilot-blazor-template/actions/workflows/ci.yml)

A minimal, agent-ready Blazor Web App starter with authentication, designed for rapid development with GitHub Copilot.

## Architecture

| Project | Purpose |
|---------|---------|
| `CopilotBlazorTemplate.Web` | Blazor Server app with Identity auth and UI |
| `CopilotBlazorTemplate.Core` | Domain entities, EF Core DbContext, data layer |
| `CopilotBlazorTemplate.UnitTests` | xUnit unit tests |
| `CopilotBlazorTemplate.E2ETests` | Playwright end-to-end tests |

## Quick Start

```bash
# Clone and run
git clone https://github.com/rquintino/copilot-blazor-template.git
cd copilot-blazor-template
dotnet run --project src/CopilotBlazorTemplate.Web
```

Open https://localhost:5001 (or the URL shown in console).

## Seeded Credentials

| Email | Password | Role |
|-------|----------|------|
| admin@template.local | Admin123! | Admin |
| user@template.local | User123! | User |

## Screenshots

| Landing | Login |
|---------|-------|
| ![Landing](docs/screenshots/landing.png) | ![Login](docs/screenshots/login.png) |

| Dashboard | Admin |
|-----------|-------|
| ![Dashboard](docs/screenshots/dashboard.png) | ![Admin](docs/screenshots/admin.png) |

## What's Included

- ✅ .NET 10 Blazor Web App
- ✅ ASP.NET Identity with seeded users (no registration)
- ✅ EF Core with SQLite
- ✅ Sidebar navigation with role-based visibility
- ✅ Custom theme (CSS variables, no Bootstrap)
- ✅ Unit tests (xUnit)
- ✅ E2E tests (Playwright)
- ✅ CI/CD with GitHub Actions
- ✅ Copilot agent support (AGENTS.md, instructions, skills)

## How to Use This Template

1. **Fork** this repository
2. **Add entities** to `src/CopilotBlazorTemplate.Core/Entities/`
3. **Add pages** to `src/CopilotBlazorTemplate.Web/Components/Pages/`
4. **Prompt Copilot** — the agent instructions and AGENTS.md guide AI assistance

## Tech Stack

- .NET 10 · Blazor Server · ASP.NET Identity
- EF Core · SQLite
- xUnit · Playwright
- GitHub Actions

---

> Scaffolded by [plan-dotnet-app](https://github.com/rquintino/skills) v1.3.0