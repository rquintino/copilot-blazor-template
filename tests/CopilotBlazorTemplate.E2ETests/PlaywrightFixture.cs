using CopilotBlazorTemplate.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class PlaywrightFixture : IAsyncLifetime
{
    private TestWebApplicationFactory _factory = null!;
    private string _dbPath = null!;

    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid():N}.db");
        _factory = new TestWebApplicationFactory(_dbPath);

        // Touching Services triggers host build; CreateHost configures Kestrel on a free port.
        var server = _factory.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        BaseUrl = address.TrimEnd('/');

        PlaywrightInstance = await Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();
        PlaywrightInstance?.Dispose();
        if (_factory is not null)
            await _factory.DisposeAsync();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public async Task<IBrowserContext> NewContextAsync() =>
        await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
        });

    public async Task<IPage> LoginAsAsync(string email, string password)
    {
        var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/Account/Login");
        await page.FillAsync("#Input\\.Email", email);
        await page.FillAsync("#Input\\.Password", password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await page.WaitForURLAsync("**/dashboard**");
        return page;
    }

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;

        public TestWebApplicationFactory(string dbPath) => _dbPath = dbPath;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureWebHost(web =>
            {
                web.UseKestrel();
                web.UseUrls("http://127.0.0.1:0");
            });
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={_dbPath}"));
            });
            return base.CreateHost(builder);
        }
    }
}
