using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class PlaywrightFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>();
        var client = Factory.CreateClient();
        BaseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        PlaywrightInstance = await Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();
        PlaywrightInstance?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
    }
}
