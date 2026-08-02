using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PageToMovie.Tests.Api;

/// <summary>
/// Video-extend continuity (see FilmJobService.GenerateOneClipAsync +
/// ClientMediaFolderService.PrepareExtendSourceAsync): the client uploads a tail-trimmed
/// continuation source via <c>kind=extend-source</c> before requesting a chained clip's
/// generation. Confirms the upload endpoint writes that to the fixed, single-use path the
/// server later looks for, distinct from a normal clip-replacement upload.
/// </summary>
public class ClipExtendSourceUploadApiTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public ClipExtendSourceUploadApiTests(PageToMovieApiFactory factory)
    {
        _factory = factory;
    }

    private static MultipartFormDataContent BuildVideoForm(string fileName = "clip.mp4")
    {
        var bytes = Encoding.ASCII.GetBytes("....ftypmp42" + new string('x', 2000));
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
        form.Add(content, "video", fileName);
        return form;
    }

    private async Task<string> CreateProjectAsync(HttpClient client)
    {
        var slug = "ExtendSrcTest_" + Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsJsonAsync("/api/projects", new { name = slug, title = "Extend Source Test" });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("active").GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Upload_with_kind_extend_source_writes_to_fixed_single_use_path()
    {
        var client = _factory.CreateUserClient("extend-src-user");
        var projectId = await CreateProjectAsync(client);

        using var form = BuildVideoForm();
        var resp = await client.PostAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/2/clips/3/upload?kind=extend-source", form);
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        var expectedPath = Path.Combine(
            _factory.WorkspaceRoot, "projects", projectId, "assets", "video", "_extend_src_s02c03.mp4");
        Assert.True(File.Exists(expectedPath), $"Expected extend-source file at {expectedPath}");

        // The default (no kind) clip-replacement path must remain untouched by this feature.
        var defaultPath = Path.Combine(
            _factory.WorkspaceRoot, "projects", projectId, "assets", "video", "scene_02_clip_03.mp4");
        Assert.False(File.Exists(defaultPath));
    }

    [Fact]
    public async Task Upload_without_kind_still_uses_default_naming()
    {
        var client = _factory.CreateUserClient("extend-src-user-2");
        var projectId = await CreateProjectAsync(client);

        using var form = BuildVideoForm(fileName: "scene_01_clip_02.mp4");
        var resp = await client.PostAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/1/clips/2/upload", form);
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());

        var expectedPath = Path.Combine(
            _factory.WorkspaceRoot, "projects", projectId, "assets", "video", "scene_01_clip_02.mp4");
        Assert.True(File.Exists(expectedPath));

        var extendSourcePath = Path.Combine(
            _factory.WorkspaceRoot, "projects", projectId, "assets", "video", "_extend_src_s01c02.mp4");
        Assert.False(File.Exists(extendSourcePath));
    }
}
