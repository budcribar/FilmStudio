using System.Diagnostics;
using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// Shared fixture for the UI regression suite. Ensures a single-process fakes host (Api serves the
/// Blazor WASM UI) is running — reusing an already-running instance on its port or launching one —
/// and owns the Playwright browser. Tests reference PageToMovie.Core/Engine directly so they can
/// compute expected values with the real domain code. Subclasses tweak the port + extra env to
/// spin up a second host (e.g. with capabilities forced off) for gated-UI tests.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    protected virtual int Port => 5088;
    protected virtual IReadOnlyDictionary<string, string> ExtraEnv => EmptyEnv;
    private static readonly IReadOnlyDictionary<string, string> EmptyEnv = new Dictionary<string, string>();

    public string BaseUrl => $"http://localhost:{Port}";
    public IBrowser Browser { get; private set; } = null!;

    private IPlaywright _pw = null!;
    private Process? _api;              // non-null only when WE launched it
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task InitializeAsync()
    {
        if (!await IsHealthyAsync())
            await LaunchApiAsync();

        var exit = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exit != 0) throw new InvalidOperationException($"playwright install exited {exit}");

        _pw = await Playwright.CreateAsync();
        Browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>A fresh, isolated context+page.</summary>
    public async Task<(IBrowserContext ctx, IPage page)> NewPageAsync()
    {
        var ctx = await Browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await ctx.NewPageAsync();
        return (ctx, page);
    }

    /// <summary>Raw HTTP GET against this fixture's host (for endpoint-level assertions).</summary>
    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync($"{BaseUrl}{path}");

    private async Task<bool> IsHealthyAsync()
    {
        try { return (await _http.GetAsync($"{BaseUrl}/health")).IsSuccessStatusCode; }
        catch { return false; }
    }

    private async Task LaunchApiAsync()
    {
        var repo = FindRepoRoot();
        var apiProj = Path.Combine(repo, "host", "PageToMovie.Api");
        // --no-launch-profile so ASPNETCORE_URLS (our port) is honored; the "http (fakes)" profile
        // pins port 5088, which would collide with the second (caps-off) host.
        var psi = new ProcessStartInfo("dotnet",
            $"run --project \"{apiProj}\" --no-launch-profile")
        {
            WorkingDirectory = Path.Combine(repo, "host"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["PageToMovie__UseFakes"] = "true";
        psi.Environment["PageToMovie_USE_FAKES"] = "true";
        psi.Environment["PageToMovie__WorkspaceRoot"] = repo;
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        foreach (var kv in ExtraEnv) psi.Environment[kv.Key] = kv.Value;

        _api = Process.Start(psi) ?? throw new InvalidOperationException("failed to start Api");
        _ = Task.Run(async () => { while (!_api.StandardOutput.EndOfStream) await _api.StandardOutput.ReadLineAsync(); });
        _ = Task.Run(async () => { while (!_api.StandardError.EndOfStream) await _api.StandardError.ReadLineAsync(); });

        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsHealthyAsync()) return;
            await Task.Delay(1000);
        }
        throw new TimeoutException($"fakes Api did not become healthy at {BaseUrl} within 3 minutes");
    }

    /// <summary>Repo root (workspace root) — the same the running host uses, so tests can drive the
    /// real domain code (e.g. CostReportService) against the same project files.</summary>
    internal static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (File.Exists(Path.Combine(d.FullName, "host", "PageToMovie.slnx"))) return d.FullName;
            d = d.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root (host/PageToMovie.slnx)");
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        _pw?.Dispose();
        if (_api is { HasExited: false })
        {
            try { _api.Kill(entireProcessTree: true); } catch { /* best effort */ }
            _api.Dispose();
        }
        _http.Dispose();
    }
}

/// <summary>
/// A second fakes host (separate port) with the gated capabilities forced OFF, so the disabled
/// "Set up →" UI is reachable (fakes otherwise reports everything configured).
/// </summary>
public sealed class CapabilitiesOffFixture : AppFixture
{
    protected override int Port => 5099;
    protected override IReadOnlyDictionary<string, string> ExtraEnv => new Dictionary<string, string>
    {
        ["PAGETOMOVIE_BIND_PORTS"] = "5099", // bind only 5099 so it doesn't collide with the :5088 host
        ["PAGETOMOVIE_FAKE_DISABLED_CAPABILITIES"] = "video,image,review,music,voice",
    };
}

[CollectionDefinition("ui")]
public sealed class UiCollection : ICollectionFixture<AppFixture> { }

[CollectionDefinition("ui-caps-off")]
public sealed class CapsOffCollection : ICollectionFixture<CapabilitiesOffFixture> { }
