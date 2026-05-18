using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

[Collection("E2E")]
public class NavigationTests
{
    private readonly PlaywrightFixture _fixture;

    public NavigationTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Admin_Can_Navigate_Sidebar_Dashboard_To_Admin()
    {
        var page = await _fixture.LoginAsAsync("admin@template.local", "Admin123!");

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
        var page = await _fixture.LoginAsAsync("user@template.local", "User123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
        // Logout posts and redirects to "/"; wait for the navigation to settle.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Now accessing a protected page should redirect to login.
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await page.WaitForURLAsync("**/Account/Login**");
        Assert.Contains("/Account/Login", page.Url);
    }

    [Fact]
    public async Task Unknown_Route_Renders_NotFound_Page()
    {
        await using var context = await _fixture.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/this-route-does-not-exist");

        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("Not Found");
    }

    [Fact]
    public async Task Dashboard_Has_Expected_Page_Title()
    {
        var page = await _fixture.LoginAsAsync("admin@template.local", "Admin123!");

        await Assertions.Expect(page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Dashboard"));
    }
}
