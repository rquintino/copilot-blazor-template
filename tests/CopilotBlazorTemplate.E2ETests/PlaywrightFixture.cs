using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

public class PlaywrightFixture : IAsyncLifetime
{
    private Process? _app;
    private string _dbPath = null!;
    private readonly StringWriter _logSink = new();

    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid():N}.db");
        await StartWebAppAsync();

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

        if (_app is not null && !_app.HasExited)
        {
            try { _app.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            try { await _app.WaitForExitAsync(); } catch { /* best-effort */ }
        }
        _app?.Dispose();

        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
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

    private async Task StartWebAppAsync()
    {
        var webDll = LocateWebAssembly();
        var contentRoot = Path.GetDirectoryName(webDll)!;

        var psi = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { "exec", webDll },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = contentRoot,
        };
        psi.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={_dbPath}";

        _app = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var listeningTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listeningRegex = new Regex(@"Now listening on:\s*(http://[^\s]+)", RegexOptions.IgnoreCase);

        void Handle(string? line)
        {
            if (line is null) return;
            lock (_logSink) _logSink.WriteLine(line);
            if (!listeningTcs.Task.IsCompleted)
            {
                var match = listeningRegex.Match(line);
                if (match.Success)
                    listeningTcs.TrySetResult(match.Groups[1].Value.TrimEnd('/'));
            }
        }

        _app.OutputDataReceived += (_, e) => Handle(e.Data);
        _app.ErrorDataReceived += (_, e) => Handle(e.Data);
        _app.Exited += (_, _) =>
        {
            if (!listeningTcs.Task.IsCompleted)
                listeningTcs.TrySetException(new InvalidOperationException(
                    $"Web app exited (code={_app?.ExitCode}) before reporting a listening address.\n--- output ---\n{_logSink}"));
        };

        _app.Start();
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        var timeout = Task.Delay(TimeSpan.FromSeconds(60));
        var winner = await Task.WhenAny(listeningTcs.Task, timeout);
        if (winner == timeout)
        {
            try { _app.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException(
                $"Web app did not start within 60s.\n--- output ---\n{_logSink}");
        }

        BaseUrl = await listeningTcs.Task;
    }

    private static string LocateWebAssembly()
    {
        var testBin = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var tfm = testBin.Name;
        var config = testBin.Parent?.Name ?? "Debug";

        var current = testBin;
        while (current is not null && !string.Equals(current.Name, "tests", StringComparison.OrdinalIgnoreCase))
            current = current.Parent;
        if (current?.Parent is null)
            throw new InvalidOperationException(
                $"Cannot locate repo root from test bin: {testBin.FullName}");

        var repoRoot = current.Parent.FullName;
        var webDll = Path.Combine(
            repoRoot, "src", "CopilotBlazorTemplate.Web", "bin", config, tfm,
            "CopilotBlazorTemplate.Web.dll");

        if (!File.Exists(webDll))
            throw new FileNotFoundException(
                $"Expected web assembly not found at: {webDll}. Did you run 'dotnet build --configuration {config}'?");

        return webDll;
    }
}
