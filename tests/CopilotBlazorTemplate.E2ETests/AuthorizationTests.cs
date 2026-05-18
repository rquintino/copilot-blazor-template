using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class AuthorizationTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public AuthorizationTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Unauthenticated_Dashboard_Redirects_To_Login()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await page.WaitForURLAsync("**/Account/Login**");

        Assert.Contains("/Account/Login", page.Url);
        Assert.Contains("ReturnUrl", page.Url);
    }

    [Fact]
    public async Task Unauthenticated_Admin_Page_Redirects_To_Login()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");
        await page.WaitForURLAsync("**/Account/Login**");

        Assert.Contains("/Account/Login", page.Url);
    }

    [Fact]
    public async Task Admin_Sidebar_Includes_Admin_Link()
    {
        await using var context = await _fixture.NewAdminContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");

        await Assertions.Expect(page.Locator(".sidebar-nav").GetByRole(AriaRole.Link, new() { Name = "Admin" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_Sidebar_Hides_Admin_Link()
    {
        await using var context = await _fixture.NewUserContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");

        await Assertions.Expect(page.Locator(".sidebar-nav").GetByRole(AriaRole.Link, new() { Name = "Dashboard" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".sidebar-nav").GetByRole(AriaRole.Link, new() { Name = "Admin" }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Admin_Can_Open_Admin_Page_And_See_User_Table()
    {
        await using var context = await _fixture.NewAdminContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Admin Panel");
        await Assertions.Expect(page.Locator(".data-table")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_Page_Lists_All_Seeded_Users()
    {
        await using var context = await _fixture.NewAdminContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");

        await Assertions.Expect(page.GetByText("admin@template.local")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("user@template.local")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".data-table tbody tr")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task NonAdmin_User_Cannot_Access_Admin_Page()
    {
        await using var context = await _fixture.NewUserContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");

        var url = page.Url;
        var bodyText = await page.Locator("body").TextContentAsync() ?? "";
        Assert.True(
            url.Contains("AccessDenied", System.StringComparison.OrdinalIgnoreCase)
                || url.Contains("Login", System.StringComparison.OrdinalIgnoreCase)
                || !bodyText.Contains("Admin Panel"),
            $"Expected non-admin to be blocked from /admin, but landed at {url}");
    }
}
