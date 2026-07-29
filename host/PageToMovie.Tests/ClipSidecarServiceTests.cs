using System.Text.Json;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ClipSidecarServiceTests : IDisposable
{
    private readonly string _tempWorkspace;

    public ClipSidecarServiceTests()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ptm-sidecar-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempWorkspace)) Directory.Delete(_tempWorkspace, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public async Task WriteSidecarAsync_creates_valid_json_sidecar()
    {
        var projects = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var service = new ClipSidecarService(projects);

        var projectDir = Path.Combine(_tempWorkspace, "projects", "TestMovie");
        Directory.CreateDirectory(projectDir);

        var sidecarPath = await service.WriteSidecarAsync(
            projectDir,
            scene: 1,
            clip: 2,
            prompt: "A dark room with glowing candles",
            scriptText: "THE CONFESSOR stares into the shadows.",
            model: "grok-imagine-video",
            resolution: "480p",
            durationSeconds: 6.0,
            sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            sizeBytes: 1024_000);

        Assert.True(File.Exists(sidecarPath));
        Assert.Contains("scene_01_clip_02", sidecarPath);
        Assert.EndsWith(".clip.json", sidecarPath);

        var text = await File.ReadAllTextAsync(sidecarPath);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        Assert.Equal("clip_sidecar.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("TestMovie", root.GetProperty("project_id").GetString());
        Assert.Equal(1, root.GetProperty("scene").GetInt32());
        Assert.Equal(2, root.GetProperty("clip").GetInt32());
        Assert.Equal("THE CONFESSOR stares into the shadows.", root.GetProperty("script_text").GetString());
        Assert.Equal("A dark room with glowing candles", root.GetProperty("visual_prompt").GetString());
        Assert.Equal("grok-imagine-video", root.GetProperty("model").GetString());
        Assert.Equal("480p", root.GetProperty("resolution").GetString());
        Assert.Equal(6.0, root.GetProperty("duration_seconds").GetDouble());
        Assert.Equal(1024_000, root.GetProperty("size_bytes").GetInt64());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("created_at_utc").GetString()));
    }

    [Fact]
    public async Task EnsureAllSidecarsExistAsync_backfills_missing_sidecars_for_mp4s()
    {
        var projects = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var service = new ClipSidecarService(projects);

        var projectDir = Path.Combine(_tempWorkspace, "projects", "TestMovie");
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        // Dummy MP4 file without sidecar
        var mp4Path = Path.Combine(videoDir, "scene_02_clip_03.mp4");
        await File.WriteAllBytesAsync(mp4Path, new byte[2048]);

        var count = await service.EnsureAllSidecarsExistAsync(projectDir);
        Assert.Equal(1, count);

        var sidecarPath = Directory.EnumerateFiles(videoDir, "*.clip.json").FirstOrDefault();
        Assert.NotNull(sidecarPath);
        Assert.True(File.Exists(sidecarPath!));

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sidecarPath));
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("scene").GetInt32());
        Assert.Equal(3, root.GetProperty("clip").GetInt32());
        Assert.Equal(2048, root.GetProperty("size_bytes").GetInt64());
    }

    [Fact]
    public async Task ConvertProjectClipsToNewFormatAsync_renames_clips_and_writes_take_sidecars()
    {
        var projects = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var service = new ClipSidecarService(projects);

        var projectDir = Path.Combine(_tempWorkspace, "projects", "TellTaleTest");
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        // Create legacy named MP4 file
        var legacyMp4 = Path.Combine(videoDir, "scene_12.mp4");
        await File.WriteAllBytesAsync(legacyMp4, new byte[1024]);

        var count = await service.ConvertProjectClipsToNewFormatAsync(projectDir);
        Assert.True(count >= 1);

        var files = Directory.GetFiles(videoDir, "*.clip.json");
        Assert.NotEmpty(files);

        var sidecarText = await File.ReadAllTextAsync(files[0]);
        using var doc = JsonDocument.Parse(sidecarText);
        var root = doc.RootElement;

        Assert.Equal(12, root.GetProperty("scene").GetInt32());
        Assert.Equal(1, root.GetProperty("take").GetInt32());
    }
}
