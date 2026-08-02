using PageToMovie.Core.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PageToMovie.Tests.Api;

/// <summary>In-process API host with fakes + isolated temp workspace.</summary>
public sealed class PageToMovieApiFactory : WebApplicationFactory<PageToMovie.Api.Program>
{
    private readonly string _workspace;

    public PageToMovieApiFactory()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "fs_api_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_workspace, "projects"));
        Directory.CreateDirectory(Path.Combine(_workspace, "prompts"));
    }

    public string WorkspaceRoot => _workspace;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PageToMovie:WorkspaceRoot"] = _workspace,
                ["PageToMovie:UseFakes"] = "true",
                ["PageToMovie:EnableReadCaches"] = "true",
                ["PageToMovie:Auth:AllowDevBypass"] = "true",
                ["PageToMovie:Auth:RequireLogin"] = "false",
                ["PageToMovie:Auth:AdminUsername"] = "admin",
                ["PageToMovie:Auth:AdminPassword"] = "admin",
                ["PageToMovie:Auth:DefaultUserId"] = "test-user",
                // Force YouTube OAuth to "unconfigured" regardless of the host machine's real
                // environment variables (e.g. PageToMovie__YouTube__ClientId/ClientSecret/RedirectUri
                // set for local dev use of the actual product) — otherwise a dev/CI box with real
                // credentials in its environment leaks them into this isolated test host and the
                // "unconfigured" gating tests (YouTubeUploadTests) become flaky/false-negative.
                ["PageToMovie:YouTube:ClientId"] = "",
                ["PageToMovie:YouTube:ClientSecret"] = "",
                ["PageToMovie:YouTube:RedirectUri"] = "",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<PageToMovieOptions>(o =>
            {
                o.WorkspaceRoot = _workspace;
                o.UseFakes = true;
                o.EnableReadCaches = true;
                o.Auth ??= new AuthOptions();
                o.Auth.RequireLogin = false;
                o.YouTube ??= new YouTubeOptions();
                o.YouTube.ClientId = "";
                o.YouTube.ClientSecret = "";
                o.YouTube.RedirectUri = "";
            });
        });
    }

    public HttpClient CreateUserClient(string userId = "test-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        try
        {
            if (Directory.Exists(_workspace))
                Directory.Delete(_workspace, recursive: true);
        }
        catch { /* temp */ }
    }
}
