using FilmStudio.Core.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FilmStudio.Tests.Api;

/// <summary>In-process API host with fakes + isolated temp workspace.</summary>
public sealed class FilmStudioApiFactory : WebApplicationFactory<Program>
{
    private readonly string _workspace;

    public FilmStudioApiFactory()
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
                ["FilmStudio:WorkspaceRoot"] = _workspace,
                ["FilmStudio:UseFakes"] = "true",
                ["FilmStudio:EnableReadCaches"] = "true",
                ["FilmStudio:Auth:AllowDevBypass"] = "true",
                ["FilmStudio:Auth:AdminUsername"] = "admin",
                ["FilmStudio:Auth:AdminPassword"] = "admin",
                ["FilmStudio:Auth:DefaultUserId"] = "test-user",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<FilmStudioOptions>(o =>
            {
                o.WorkspaceRoot = _workspace;
                o.UseFakes = true;
                o.EnableReadCaches = true;
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
