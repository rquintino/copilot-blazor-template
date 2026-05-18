using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class NavigationTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public NavigationTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Admin_Can_Navigate_Sidebar_Dashboard_To_Admin()
    {
        await using var context = await _fixture.NewAdminContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");

        await page.Locator(".sidebar-nav").GetByRole(AriaRole.Link, new() { Name = "Admin" }).ClickAsync();
        await page.WaitForURLAsync("**/admin");
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Admin Panel");

        await page.Locator(".sidebar-nav").GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();
        await page.WaitForURLAsync("**/dashboard");
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
    }

    [Fact]
    public async Task Logout_Signs_User_Out()
    {
        // Real login so the post-logout cookie state on this context is observable.
        var page = await _fixture.LoginAsAsync("user@template.local", "User123!");
        // ClickAsync on a submit button awaits the form post + redirect chain.
        await page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();

        // With the cookie cleared, hitting a protected page should bounce to login.
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await page.WaitForURLAsync("**/Account/Login**");
        Assert.Contains("/Account/Login", page.Url);
    }

    [Fact]
    public async Task Unknown_Route_Renders_NotFound_Page()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/this-route-does-not-exist");

        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Not Found");
    }

    [Fact]
    public async Task Dashboard_Has_Expected_Page_Title()
    {
        await using var context = await _fixture.NewAdminContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");

        await Assertions.Expect(page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Dashboard"));
    }
}
