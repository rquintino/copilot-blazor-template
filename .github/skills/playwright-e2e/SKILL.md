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
