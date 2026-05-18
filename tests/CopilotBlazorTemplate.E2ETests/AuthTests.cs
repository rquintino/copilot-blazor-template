using Microsoft.Playwright;
using Xunit;

namespace CopilotBlazorTemplate.E2ETests;

[Collection("Playwright")]
public class AuthTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public AuthTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = "Requires Playwright browsers installed")]
    public async Task Landing_Page_Loads()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        var heading = await page.TextContentAsync("h1");
        Assert.Contains("Copilot Blazor Template", heading);
    }

    [Fact(Skip = "Requires Playwright browsers installed")]
    public async Task Unauthenticated_Redirects_To_Login()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/dashboard");
        await page.WaitForURLAsync("**/Account/Login**");
        Assert.Contains("Account/Login", page.Url);
    }

    [Fact(Skip = "Requires Playwright browsers installed")]
    public async Task Admin_Login_Shows_Dashboard()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.FillAsync("input[name='Input.Email']", "admin@template.local");
        await page.FillAsync("input[name='Input.Password']", "Admin123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync("**/dashboard**");
        var content = await page.TextContentAsync("body");
        Assert.Contains("Administrator", content);
    }

    [Fact(Skip = "Requires Playwright browsers installed")]
    public async Task Admin_Can_Access_Admin_Page()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.FillAsync("input[name='Input.Email']", "admin@template.local");
        await page.FillAsync("input[name='Input.Password']", "Admin123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync("**/dashboard**");
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");
        var content = await page.TextContentAsync("body");
        Assert.Contains("Admin Panel", content);
    }

    [Fact(Skip = "Requires Playwright browsers installed")]
    public async Task User_Cannot_Access_Admin_Page()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await page.FillAsync("input[name='Input.Email']", "user@template.local");
        await page.FillAsync("input[name='Input.Password']", "User123!");
        await page.ClickAsync("button[type='submit']");
        await page.WaitForURLAsync("**/dashboard**");
        await page.GotoAsync($"{_fixture.BaseUrl}/admin");
        // Should be redirected or shown access denied
        var url = page.Url;
        var content = await page.TextContentAsync("body");
        Assert.True(url.Contains("AccessDenied") || !content.Contains("Admin Panel"));
    }
}
