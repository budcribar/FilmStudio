using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class SceneGitHistoryTests
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
    public async Task GetSceneGitHistoryAsync_and_RevertSceneToCommitAsync_work_end_to_end()
    {
        var root = NewTempDir("ptm_scene_history");
        try
        {
            var projectDir = Path.Combine(root, "projects", "TestProj");
            Directory.CreateDirectory(projectDir);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);

            var initialBlueprint = """
            {
              "scenes": [
                {
                  "scene_number": 1,
                  "heading": "EXT. FOREST - DAY",
                  "veo_clips": [
                    { "clip_number": 1, "visual_prompt": "Original prompt 1", "duration_seconds": 4.0 },
                    { "clip_number": 2, "visual_prompt": "Original prompt 2", "duration_seconds": 4.0 }
                  ]
                }
              ]
            }
            """;
            File.WriteAllText(Path.Combine(projectDir, "blueprint.clips.grok.json"), initialBlueprint);
            var git = new ProjectGitRepositoryService(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectGitRepositoryService>.Instance);
            var c1 = await git.CommitProjectStateAsync(projectDir, "Operator", "Initial scene 1 setup");

            // Update clip 2 prompt in scene 1
            var updatedBlueprint = """
            {
              "scenes": [
                {
                  "scene_number": 1,
                  "heading": "EXT. FOREST - DAY",
                  "veo_clips": [
                    { "clip_number": 1, "visual_prompt": "Original prompt 1", "duration_seconds": 4.0 },
                    { "clip_number": 2, "visual_prompt": "MODIFIED prompt 2", "duration_seconds": 5.0 }
                  ]
                }
              ]
            }
            """;
            File.WriteAllText(Path.Combine(projectDir, "blueprint.clips.grok.json"), updatedBlueprint);
            var c2 = await git.CommitProjectStateAsync(projectDir, "Operator", "Modified clip 2 prompt");

            // Test GetSceneGitHistoryAsync
            var history = await store.GetSceneGitHistoryAsync("TestProj", 1);
            Assert.NotNull(history);
            Assert.True(history.Count >= 2);
            Assert.Contains(history, h => h.Changes.Exists(ch => ch.Contains("Clip 2 prompt modified")));

            // Test RevertSceneToCommitAsync back to c1
            var reverted = await store.RevertSceneToCommitAsync("TestProj", 1, c1.CommitHash);
            Assert.True(reverted);

            var currentBpText = File.ReadAllText(Path.Combine(projectDir, "blueprint.clips.grok.json"));
            Assert.Contains("Original prompt 2", currentBpText);
            Assert.DoesNotContain("MODIFIED prompt 2", currentBpText);
        }
        finally
        {
            DeleteDir(root);
        }
    }
}
