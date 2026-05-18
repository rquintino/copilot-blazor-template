using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CopilotBlazorTemplate.E2ETests;

// Each test class gets its own PlaywrightFixture via IClassFixture so classes
// can run in parallel, but all instances share one app+browser+cookie cache via
// a static async-Lazy. Without this trick a [Collection] would be required to
// share state, which would force the whole suite to run serially.
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private static readonly Lazy<Task<SharedState>> _shared =
        new(SharedState.CreateAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private SharedState _state = null!;

    public async Task InitializeAsync() => _state = await _shared.Value;

    // No-op: the singleton lives for the whole test session; OS reaps the
    // subprocess on process exit (also wired up below).
    public Task DisposeAsync() => Task.CompletedTask;

    public string BaseUrl => _state.BaseUrl;
    public IBrowser Browser => _state.Browser;

    // Keep timeouts tight so failures surface fast instead of waiting 30s default.
    private const int DefaultTimeoutMs = 2000;

    public Task<IBrowserContext> NewAnonymousContextAsync() =>
        CreateContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
        });

    public Task<IBrowserContext> NewAdminContextAsync() =>
        CreateContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            StorageState = _state.AdminStorageState,
        });

    public Task<IBrowserContext> NewUserContextAsync() =>
        CreateContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            StorageState = _state.UserStorageState,
        });

    private async Task<IBrowserContext> CreateContextAsync(BrowserNewContextOptions options)
    {
        var ctx = await Browser.NewContextAsync(options);
        ctx.SetDefaultTimeout(DefaultTimeoutMs);
        ctx.SetDefaultNavigationTimeout(DefaultTimeoutMs);
        return ctx;
    }

    // Real interactive login — for tests that explicitly exercise the login flow.
    public async Task<IPage> LoginAsAsync(string email, string password)
    {
        var context = await NewAnonymousContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/Account/Login");
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await page.WaitForURLAsync("**/dashboard**");
        return page;
    }

    private sealed class SharedState
    {
        public string BaseUrl { get; private set; } = null!;
        public IBrowser Browser { get; private set; } = null!;
        public string AdminStorageState { get; private set; } = null!;
        public string UserStorageState { get; private set; } = null!;

        private IPlaywright _playwright = null!;
        private Process _app = null!;
        private string _dbPath = null!;

        public static async Task<SharedState> CreateAsync()
        {
            var s = new SharedState();
            await s.InitAsync();
            return s;
        }

        private async Task InitAsync()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid():N}.db");
            BaseUrl = await StartWebAppAsync();

            _playwright = await Playwright.CreateAsync();
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                ChromiumSandbox = false,
                Args = new[]
                {
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-renderer-backgrounding",
                },
            });

            AdminStorageState = await CaptureLoginStateAsync("admin@template.local", "Admin123!");
            UserStorageState = await CaptureLoginStateAsync("user@template.local", "User123!");

            // Web-first assertions default to 5s; clamp them so failed tests
            // surface in <2s instead of stretching out to 30s of timeouts.
            Assertions.SetDefaultExpectTimeout(DefaultTimeoutMs);

            // Best-effort cleanup on process exit so subprocesses and temp DBs
            // don't accumulate across local runs.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Cleanup();
        }

        private async Task<string> CaptureLoginStateAsync(string email, string password)
        {
            await using var ctx = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = BaseUrl,
            });
            var page = await ctx.NewPageAsync();
            await page.GotoAsync($"{BaseUrl}/Account/Login");
            await page.GetByLabel("Email").FillAsync(email);
            await page.GetByLabel("Password").FillAsync(password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
            await page.WaitForURLAsync("**/dashboard**");
            return await ctx.StorageStateAsync();
        }

        private async Task<string> StartWebAppAsync()
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
            // Quiet down noisy Identity / EF logs to keep stdout parseable.
            psi.Environment["Logging__LogLevel__Default"] = "Warning";
            psi.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";

            _app = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var listeningTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var listeningRegex = new Regex(@"Now listening on:\s*(http://[^\s]+)", RegexOptions.IgnoreCase);
            var logSink = new System.Text.StringBuilder();

            void Handle(string? line)
            {
                if (line is null) return;
                lock (logSink) logSink.AppendLine(line);
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
                        $"Web app exited (code={_app.ExitCode}) before reporting a listening address.\n--- output ---\n{logSink}"));
            };

            _app.Start();
            _app.BeginOutputReadLine();
            _app.BeginErrorReadLine();

            var timeout = Task.Delay(TimeSpan.FromSeconds(60));
            var winner = await Task.WhenAny(listeningTcs.Task, timeout);
            if (winner == timeout)
            {
                try { _app.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                throw new TimeoutException($"Web app did not start within 60s.\n--- output ---\n{logSink}");
            }

            return await listeningTcs.Task;
        }

        private void Cleanup()
        {
            try { if (_app is not null && !_app.HasExited) _app.Kill(entireProcessTree: true); } catch { }
            try { _app?.Dispose(); } catch { }
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

        private static string LocateWebAssembly()
        {
            var testBin = new DirectoryInfo(
                AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var tfm = testBin.Name;
            var config = testBin.Parent?.Name ?? "Debug";

            var current = testBin;
            while (current is not null && !string.Equals(current.Name, "tests", StringComparison.OrdinalIgnoreCase))
                current = current.Parent;
            if (current?.Parent is null)
                throw new InvalidOperationException($"Cannot locate repo root from test bin: {testBin.FullName}");

            var webDll = Path.Combine(
                current.Parent.FullName, "src", "CopilotBlazorTemplate.Web", "bin", config, tfm,
                "CopilotBlazorTemplate.Web.dll");

            if (!File.Exists(webDll))
                throw new FileNotFoundException(
                    $"Expected web assembly not found at: {webDll}. Did you run 'dotnet build --configuration {config}'?");

            return webDll;
        }
    }
}
