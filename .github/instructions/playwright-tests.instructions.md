---
applyTo: "**/E2ETests/**"
---

# Playwright E2E Test Instructions

- Test project: `tests/CopilotBlazorTemplate.E2ETests/`
- Uses `Microsoft.Playwright` NuGet package
- Use `WebApplicationFactory<Program>` to start the app in-process
- Browser: Chromium, headless mode
- Test seeded credentials:
  - Admin: admin@template.local / Admin123!
  - User: user@template.local / User123!
- Login flow: navigate to /Account/Login, fill email + password, submit
- Use `page.WaitForURLAsync()` after navigation actions
- Assert on page content with `page.TextContentAsync()`
- Each test should use a fresh page (`Browser.NewPageAsync()`)
