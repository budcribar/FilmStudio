using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class FakeVideoPipelineIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;
    private readonly PageToMovieOptions _options;

    public FakeVideoPipelineIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm_fake_pipeline_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _options = new PageToMovieOptions { WorkspaceRoot = _root };
        _store = new ProjectStore(Options.Create(_options));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch { }
    }

    [Fact]
    public async Task Full_Fake_Video_Creation_And_Publishing_Lifecycle()
    {
        // 1. Create Studio Project
        var proj = await _store.CreateProjectAsync("e2e_fake_film", "End-to-End Fake Film", ownerUserId: "alice");
        Assert.NotNull(proj);

        // 2. Save Fountain Screenplay Draft
        var fountainText = @"
TITLE: End-to-End Fake Film
AUTHOR: Antigravity Test

INT. COFFEE SHOP - DAY

ALICE
Welcome to the PageToMovie fake video pipeline demo!

BOB
This is generated instantly using the fake video engine.
";
        ScreenplayService.ImportAsDraft(_store, proj.Id, fountainText, "screenplay.fountain");
        var fountainPath = Path.Combine(proj.Path, "source", "screenplay.fountain");
        Assert.True(File.Exists(fountainPath));

        // 3. Create Stage 2 Blueprint File
        var blueprintJson = @"{
          ""scenes"": [
            {
              ""scene_index"": 1,
              ""heading"": ""INT. COFFEE SHOP - DAY"",
              ""veo_clips"": [
                {
                  ""clip_index"": 1,
                  ""visual_prompt"": ""Cinematic medium shot of Alice in a coffee shop."",
                  ""dialogue"": ""Welcome to the PageToMovie fake video pipeline demo!"",
                  ""relative_path"": ""assets/video/scene_01_clip_01.mp4""
                }
              ]
            }
          ]
        }";
        var blueprintPath = Path.Combine(proj.Path, "blueprint.clips.grok.json");
        await File.WriteAllTextAsync(blueprintPath, blueprintJson);

        // 4. Generate Video Clip Bytes & Calculate SHA-256 Hash
        var videoDir = Path.Combine(proj.Path, "assets", "video");
        Directory.CreateDirectory(videoDir);
        var targetClipPath = Path.Combine(videoDir, "scene_01_clip_01.mp4");

        var videoBytes = Encoding.UTF8.GetBytes("fake_mp4_video_data_stream_for_e2e_test");
        var sha256 = Convert.ToHexString(SHA256.HashData(videoBytes)).ToLowerInvariant();
        await File.WriteAllBytesAsync(targetClipPath, videoBytes);

        Assert.True(File.Exists(targetClipPath));

        // 5. Register Provenance in Media Registry DB
        var registry = new MediaRegistryService(Options.Create(_options));
        var mediaObj = await registry.UpsertAsync(
            proj.Id,
            "assets/video/scene_01_clip_01.mp4",
            sha256,
            videoBytes.Length,
            "clip",
            scene: 1,
            clip: 1,
            userId: "alice");

        Assert.NotNull(mediaObj);
        Assert.Equal(sha256, mediaObj.Sha256);

        // 6. Verify Media Object lookup by Relative Path
        var retrieved = await registry.TryGetAsync(proj.Id, "assets/video/scene_01_clip_01.mp4");
        Assert.NotNull(retrieved);
        Assert.Equal(sha256, retrieved.Sha256);

        // 7. Cleanup Generated Clip (Simulated Video Purge / Deletion)
        if (File.Exists(targetClipPath))
        {
            File.Delete(targetClipPath);
        }
        Assert.False(File.Exists(targetClipPath));
    }
}
