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
    public string BaseUrl => _factory.BaseUrl;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid():N}.db");
        _factory = new TestWebApplicationFactory(_dbPath);
        _factory.Start();

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
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await page.WaitForURLAsync("**/dashboard**");
        return page;
    }

    // Runs two hosts: WebApplicationFactory's built-in in-memory TestServer (kept so WAF
    // internals don't blow up) plus a real Kestrel host on a free port that Playwright hits.
    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;
        private IHost? _kestrelHost;

        public TestWebApplicationFactory(string dbPath) => _dbPath = dbPath;

        public string BaseUrl { get; private set; } = null!;

        public void Start()
        {
            using var _ = CreateDefaultClient();
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={_dbPath}"));
            });

            // Build the in-memory TestServer host first; WAF holds onto this.
            var testHost = base.CreateHost(builder);

            // Reconfigure the deferred builder for Kestrel on a free port and build again.
            builder.ConfigureWebHost(web =>
            {
                web.UseKestrel();
                web.UseUrls("http://127.0.0.1:0");
            });
            _kestrelHost = builder.Build();
            _kestrelHost.Start();

            var addresses = _kestrelHost.Services
                .GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!;
            BaseUrl = addresses.Addresses.First().TrimEnd('/');

            return testHost;
        }

        protected override void Dispose(bool disposing)
        {
            _kestrelHost?.Dispose();
            base.Dispose(disposing);
        }
    }
}
