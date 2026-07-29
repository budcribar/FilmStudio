using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ClipVersionAndUncommittedTests
{
    private static string NewTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDir(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }

    [Fact]
    public async Task ClipVersions_and_UncommittedStatus_work_end_to_end()
    {
        var root = NewTempDir("ptm_clip_versions");
        try
        {
            var projectDir = Path.Combine(root, "projects", "TestProj");
            var videoDir = Path.Combine(projectDir, "assets", "video");
            var historyDir = Path.Combine(videoDir, "history");
            Directory.CreateDirectory(historyDir);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);

            // Active clip file
            File.WriteAllText(Path.Combine(videoDir, "scene_01_clip_01.mp4"), "active_video");
            File.WriteAllText(Path.Combine(videoDir, "scene_01_clip_01.clip.json"), """{"visual_prompt":"Active Prompt"}""");

            // History take 1
            File.WriteAllText(Path.Combine(historyDir, "scene_01_clip_01_100.mp4"), "history_take_1");
            File.WriteAllText(Path.Combine(historyDir, "scene_01_clip_01_100.clip.json"), """{"visual_prompt":"Take 1 Prompt"}""");

            var versions = await store.GetClipVersionsAsync("TestProj", 1, 1);
            Assert.NotNull(versions);
            Assert.True(versions.Count >= 2);
            Assert.Contains(versions, v => v.IsCurrent && v.VisualPrompt == "Active Prompt");
            Assert.Contains(versions, v => !v.IsCurrent && v.VisualPrompt == "Take 1 Prompt");

            // Promote take 1
            var promoted = await store.PromoteClipVersionAsync("TestProj", 1, 1, "scene_01_clip_01_100.mp4");
            Assert.True(promoted);

            var newActiveText = File.ReadAllText(Path.Combine(videoDir, "scene_01_clip_01.mp4"));
            Assert.Equal("history_take_1", newActiveText);
        }
        finally
        {
            DeleteDir(root);
        }
    }
}
