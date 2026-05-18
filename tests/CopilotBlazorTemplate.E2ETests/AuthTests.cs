using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

// These tests exercise the real login UI, so they do a full interactive login
// rather than reusing the cached storage state.
public class AuthTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public AuthTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Login_Page_Renders_Form()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Log in");
        await Assertions.Expect(page.GetByLabel("Email")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Password")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log in" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_Login_Lands_On_Dashboard_With_Admin_Role()
    {
        var page = await _fixture.LoginAsAsync("admin@template.local", "Admin123!");

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/dashboard.*"));
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
        await Assertions.Expect(page.Locator("h2")).ToContainTextAsync("Administrator");
        await Assertions.Expect(page.Locator(".badge").First).ToContainTextAsync("Admin");
    }

    [Fact]
    public async Task User_Login_Lands_On_Dashboard_With_User_Role()
    {
        var page = await _fixture.LoginAsAsync("user@template.local", "User123!");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
        await Assertions.Expect(page.Locator("h2")).ToContainTextAsync("Demo User");
        await Assertions.Expect(page.Locator(".badge").First).ToContainTextAsync("User");
    }

    [Fact]
    public async Task Invalid_Password_Shows_Error_Message()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.GetByLabel("Email").FillAsync("admin@template.local");
        await page.GetByLabel("Password").FillAsync("WrongPassword!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Invalid login attempt")).ToBeVisibleAsync();
        Assert.Contains("/Account/Login", page.Url);
    }

    [Fact]
    public async Task Unknown_Email_Shows_Error_Message()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.GetByLabel("Email").FillAsync("nobody@template.local");
        await page.GetByLabel("Password").FillAsync("Whatever123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Invalid login attempt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Empty_Submission_Shows_Validation_Errors()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

        await Assertions.Expect(page.Locator(".text-danger").First).ToBeVisibleAsync();
        Assert.Contains("/Account/Login", page.Url);
    }
}
