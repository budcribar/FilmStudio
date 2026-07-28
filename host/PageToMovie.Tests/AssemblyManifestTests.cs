using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>Assembly gate eligibility via edit log (browser Play excludes fails).</summary>
public class AssemblyManifestTests
{
    [Fact]
    public async Task IsClipEligibleForAssembly_excludes_auto_fail()
    {
        var root = Path.Combine(Path.GetTempPath(), "fs-asm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var proj = Path.Combine(root, "projects", "P");
            var video = Path.Combine(proj, "assets", "video");
            Directory.CreateDirectory(video);
            Directory.CreateDirectory(Path.Combine(root, "prompts"));
            File.WriteAllText(Path.Combine(proj, "project.json"), """{"id":"P"}""");
            File.WriteAllText(Path.Combine(proj, "pipeline_state.json"), """
                {
                  "clip_auto_review": {
                    "S01C01": { "suggestion": "pass", "category": "ok" },
                    "S01C02": { "suggestion": "fail", "category": "wrong_style", "note": "sketch" }
                  },
                  "clip_review": {}
                }
                """);
            File.WriteAllBytes(Path.Combine(video, "scene_01_clip_01.mp4"), new byte[2048]);
            File.WriteAllBytes(Path.Combine(video, "scene_01_clip_02.mp4"), new byte[2048]);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);
            var learning = new ReviewEventStore(store, NullLogger<ReviewEventStore>.Instance);
            var logs = new EditLogService(store, learning, NullLogger<EditLogService>.Instance);

            Assert.True((await logs.IsClipEligibleForAssemblyAsync("P", 1, 1)).Eligible);
            var (eligible, reason) = await logs.IsClipEligibleForAssemblyAsync("P", 1, 2);
            Assert.False(eligible);
            Assert.Contains("wrong_style", reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* */ }
        }
    }

    [Fact]
    public void IsExactClipFileName_rejects_native_sidecars()
    {
        Assert.True(ClipFileNaming.IsExactClipFileName("scene_01_clip_02.mp4"));
        Assert.False(ClipFileNaming.IsExactClipFileName("scene_01_clip_02.mp4.native.mp4"));
        Assert.False(ClipFileNaming.IsExactClipFileName("scene_01.mp4"));
    }
}
