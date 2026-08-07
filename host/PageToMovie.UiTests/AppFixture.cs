using System.Diagnostics;
using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// Shared fixture for the UI regression suite. Ensures the single-process fakes host
/// (Api serves the Blazor WASM UI at :5088) is running — reusing an already-running
/// instance or launching one — and owns the Playwright browser. Tests reference
/// PageToMovie.Core/Engine directly so they can compute expected values with the real
/// domain code rather than re-deriving them here.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    public string BaseUrl { get; } = "http://localhost:5088";
    public IBrowser Browser { get; private set; } = null!;

    private IPlaywright _pw = null!;
    private Process? _api;              // non-null only when WE launched it
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task InitializeAsync()
    {
        if (!await IsHealthyAsync())
            await LaunchApiAsync();

        // Install the browser if missing (idempotent; fast when cached).
        var exit = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exit != 0) throw new InvalidOperationException($"playwright install exited {exit}");

        _pw = await Playwright.CreateAsync();
        Browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>A fresh, isolated context+page already navigated through the login bypass + terms gate.</summary>
    public async Task<(IBrowserContext ctx, IPage page)> NewPageAsync()
    {
        var ctx = await Browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new() { Width = 1280, Height = 900 } });
        var page = await ctx.NewPageAsync();
        return (ctx, page);
    }

    private async Task<bool> IsHealthyAsync()
    {
        try
        {
            var r = await Http.GetAsync($"{BaseUrl}/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task LaunchApiAsync()
    {
        var repo = FindRepoRoot();
        var apiProj = Path.Combine(repo, "host", "PageToMovie.Api");
        var psi = new ProcessStartInfo("dotnet",
            $"run --project \"{apiProj}\" --launch-profile \"http (fakes)\"")
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

        _api = Process.Start(psi) ?? throw new InvalidOperationException("failed to start Api");
        // Drain output so the child doesn't block on a full pipe.
        _ = Task.Run(async () => { while (!_api.StandardOutput.EndOfStream) await _api.StandardOutput.ReadLineAsync(); });
        _ = Task.Run(async () => { while (!_api.StandardError.EndOfStream) await _api.StandardError.ReadLineAsync(); });

        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsHealthyAsync()) return;
            await Task.Delay(1000);
        }
        throw new TimeoutException("fakes Api did not become healthy within 3 minutes");
    }

    private static string FindRepoRoot()
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
    }
}

[CollectionDefinition("ui")]
public sealed class UiCollection : ICollectionFixture<AppFixture> { }
