using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class HomeTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public HomeTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Landing_Page_Shows_Hero_Content()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Copilot Blazor Template");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Landing_Page_Has_Correct_Title()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);

        await Assertions.Expect(page).ToHaveTitleAsync(new System.Text.RegularExpressions.Regex("Copilot Blazor Template"));
    }

    [Fact]
    public async Task Clicking_Login_Link_Navigates_To_Login_Page()
    {
        await using var context = await _fixture.NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        await page.GetByRole(AriaRole.Link, new() { Name = "Login" }).ClickAsync();

        await page.WaitForURLAsync("**/Account/Login");
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Log in");
    }

    [Fact]
    public async Task Authenticated_User_Redirected_From_Home_To_Dashboard()
    {
        await using var context = await _fixture.NewUserContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForURLAsync("**/dashboard**");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Dashboard");
    }
}
