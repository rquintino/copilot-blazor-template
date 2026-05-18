---
name: playwright-e2e
description: Author or maintain Playwright end-to-end tests under `tests/CopilotBlazorTemplate.E2ETests/` (or the renamed `*.E2ETests/` after bootstrap). Use when adding tests for a new page or flow, debugging an existing E2E failure, working with `WebApplicationFactory<Program>` fixtures, or anything involving Microsoft.Playwright + xUnit in this repo. Does NOT cover ad-hoc browser inspection (use Playwright MCP) or screenshot/demo capture (use `screenshots-demo`).
---

# Playwright E2E Testing Skill

## Overview
End-to-end testing for CopilotBlazorTemplate using Playwright.

## Project Setup
- Location: `tests/CopilotBlazorTemplate.E2ETests/`
- Framework: xUnit + Microsoft.Playwright
- App startup: `WebApplicationFactory<Program>`

## Test Patterns

### Login Helper
```csharp
var page = await browser.NewPageAsync();
await page.GotoAsync($"{baseUrl}/Account/Login");
await page.FillAsync("input[name='Input.Email']", email);
await page.FillAsync("input[name='Input.Password']", password);
await page.ClickAsync("button[type='submit']");
await page.WaitForURLAsync("**/dashboard**");
```

### Route Testing
- Public routes: `/` (landing)
- Auth required: `/dashboard`
- Admin only: `/admin`

## Running Tests
```bash
# Install browsers first
cd tests/CopilotBlazorTemplate.E2ETests
dotnet build
pwsh bin/Release/net10.0/playwright.ps1 install --with-deps chromium

# Run tests
dotnet test tests/CopilotBlazorTemplate.E2ETests/
```
