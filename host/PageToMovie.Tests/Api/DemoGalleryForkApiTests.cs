using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests.Api;

/// <summary>Feature 11: public gallery can fork the studio project behind a demo film.</summary>
public class DemoGalleryForkApiTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public DemoGalleryForkApiTests(PageToMovieApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Fork_from_public_demo_creates_project_under_caller()
    {
        var owner = _factory.CreateUserClient("gallery-owner");
        var projectId = "GalForkSrc_" + Guid.NewGuid().ToString("N")[..8];
        var create = await owner.PostAsJsonAsync("/api/projects", new { name = projectId, title = "Gallery Fork Source" });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());

        // Seed a public demo linked to that project (without needing WIP movie pipeline).
        string demoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var demos = scope.ServiceProvider.GetRequiredService<DemoCatalogService>();
            var tempMp4 = Path.Combine(Path.GetTempPath(), "ptm_demo_fork_" + Guid.NewGuid().ToString("N") + ".mp4");
            await File.WriteAllBytesAsync(tempMp4, Encoding.ASCII.GetBytes("....ftypmp42" + new string('x', 2000)));
            try
            {
                var entry = demos.PublishFromFile(
                    tempMp4,
                    "Gallery Film",
                    "for fork test",
                    projectId,
                    "gallery-owner",
                    acceptedGuidelines: true);
                demos.SetStatus(entry.Id, DemoCatalogService.DemoStatuses.Public, "admin", "approve for fork test");
                demoId = entry.Id;
            }
            finally
            {
                try { File.Delete(tempMp4); } catch { /* */ }
            }
        }

        var list = await owner.GetFromJsonAsync<JsonElement>("/api/demos?take=50&sort=new");
        var demosArr = list.GetProperty("demos");
        var found = false;
        foreach (var el in demosArr.EnumerateArray())
        {
            if (el.GetProperty("id").GetString() == demoId)
            {
                found = true;
                Assert.True(el.GetProperty("canFork").GetBoolean());
                break;
            }
        }
        Assert.True(found, "Public demo should appear in gallery with canFork");

        var viewer = _factory.CreateUserClient("gallery-forker");
        var forkResp = await viewer.PostAsync($"/api/demos/{demoId}/fork", content: null);
        Assert.Equal(HttpStatusCode.OK, forkResp.StatusCode);
        using var forkDoc = JsonDocument.Parse(await forkResp.Content.ReadAsStringAsync());
        Assert.True(forkDoc.RootElement.GetProperty("ok").GetBoolean());
        var forkedId = forkDoc.RootElement.GetProperty("projectId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(forkedId));
        Assert.NotEqual(projectId, forkedId);
        Assert.Equal(projectId, forkDoc.RootElement.GetProperty("parentProjectId").GetString());
    }

    [Fact]
    public async Task Fork_missing_demo_returns_not_found()
    {
        var user = _factory.CreateUserClient("gallery-forker-2");
        var resp = await user.PostAsync("/api/demos/does_not_exist_zzzz/fork", content: null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Fork_without_source_project_returns_bad_request()
    {
        string demoId;
        using (var scope = _factory.Services.CreateScope())
        {
            var demos = scope.ServiceProvider.GetRequiredService<DemoCatalogService>();
            var tempMp4 = Path.Combine(Path.GetTempPath(), "ptm_demo_fork2_" + Guid.NewGuid().ToString("N") + ".mp4");
            await File.WriteAllBytesAsync(tempMp4, Encoding.ASCII.GetBytes("....ftypmp42" + new string('x', 2000)));
            try
            {
                var entry = demos.PublishFromFile(
                    tempMp4, "Orphan Film", null, projectId: null, createdBy: "someone",
                    acceptedGuidelines: true);
                demos.SetStatus(entry.Id, DemoCatalogService.DemoStatuses.Public, "admin", null);
                demoId = entry.Id;
            }
            finally
            {
                try { File.Delete(tempMp4); } catch { /* */ }
            }
        }

        var user = _factory.CreateUserClient("gallery-forker-3");
        var resp = await user.PostAsync($"/api/demos/{demoId}/fork", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
