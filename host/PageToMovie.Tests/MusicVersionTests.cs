using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class MusicVersionTests
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
    public async Task MusicVersions_list_and_promote_work_end_to_end()
    {
        var root = NewTempDir("ptm_music_versions");
        try
        {
            var projectDir = Path.Combine(root, "projects", "TestProj");
            var musicDir = Path.Combine(projectDir, "assets", "music");
            var historyDir = Path.Combine(musicDir, "history");
            Directory.CreateDirectory(historyDir);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);

            // Active take sidecar
            File.WriteAllText(Path.Combine(musicDir, "scene_01.meta.json"), """
                {"take_id":"200","model":"fal-ai/stable-audio","is_vocal":false,"prompt":"Active style","segment_file_names":["scene_01_seg_01.wav"],"created_at_utc":"2026-07-30T12:00:00Z"}
                """);

            // Archived vocal take
            File.WriteAllText(Path.Combine(historyDir, "scene_01_take_100.meta.json"), """
                {"take_id":"100","model":"suno-v5-5","is_vocal":true,"prompt":"Ballad style","lyrics":"Somewhere in the dark","segment_file_names":["scene_01_seg_01_100.wav"],"created_at_utc":"2026-07-30T11:00:00Z"}
                """);

            var versions = await store.GetMusicVersionsAsync("TestProj", 1);
            Assert.NotNull(versions);
            Assert.Equal(2, versions.Count);
            Assert.Contains(versions, v => v.IsCurrent && v.TakeId == "200" && !v.IsVocal && v.Prompt == "Active style");
            Assert.Contains(versions, v => !v.IsCurrent && v.TakeId == "100" && v.IsVocal && v.Lyrics == "Somewhere in the dark");

            // Promote the archived vocal take to active
            var promoted = await store.PromoteMusicVersionAsync("TestProj", 1, "100");
            Assert.True(promoted);

            var newActiveJson = File.ReadAllText(Path.Combine(musicDir, "scene_01.meta.json"));
            Assert.Contains("\"take_id\":\"100\"", newActiveJson);
            Assert.Contains("\"is_vocal\":true", newActiveJson);

            // The previously-active take (200) should now be archived under history
            Assert.True(File.Exists(Path.Combine(historyDir, "scene_01_take_200.meta.json")));

            var versionsAfter = await store.GetMusicVersionsAsync("TestProj", 1);
            Assert.Contains(versionsAfter, v => v.IsCurrent && v.TakeId == "100");
            Assert.Contains(versionsAfter, v => !v.IsCurrent && v.TakeId == "200");
        }
        finally
        {
            DeleteDir(root);
        }
    }

    [Fact]
    public async Task MusicVersions_soft_delete_and_restore_work_end_to_end()
    {
        var root = NewTempDir("ptm_music_trash");
        try
        {
            var projectDir = Path.Combine(root, "projects", "TestProj");
            var musicDir = Path.Combine(projectDir, "assets", "music");
            var historyDir = Path.Combine(musicDir, "history");
            Directory.CreateDirectory(historyDir);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);

            File.WriteAllText(Path.Combine(musicDir, "scene_01.meta.json"), """
                {"take_id":"200","model":"fal-ai/stable-audio","is_vocal":false,"prompt":"Active","segment_file_names":["scene_01_seg_01.wav"],"created_at_utc":"2026-07-30T12:00:00Z"}
                """);
            File.WriteAllText(Path.Combine(historyDir, "scene_01_take_100.meta.json"), """
                {"take_id":"100","model":"fal-ai/stable-audio","is_vocal":false,"prompt":"Old","segment_file_names":["scene_01_seg_01_100.wav"],"created_at_utc":"2026-07-30T11:00:00Z"}
                """);

            var deleted = await store.SoftDeleteMusicVersionAsync("TestProj", 1, "100");
            Assert.True(deleted);
            Assert.False(File.Exists(Path.Combine(historyDir, "scene_01_take_100.meta.json")));

            var trash = await store.GetTrashMusicVersionsAsync("TestProj", 1);
            Assert.Single(trash);
            Assert.Equal("100", trash[0].TakeId);

            var restored = await store.RestoreSoftDeletedMusicVersionAsync("TestProj", 1, "100");
            Assert.True(restored);
            Assert.True(File.Exists(Path.Combine(historyDir, "scene_01_take_100.meta.json")));
        }
        finally
        {
            DeleteDir(root);
        }
    }
}
