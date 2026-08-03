using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class SelectiveSceneReplanTests
{
    [Fact]
    public async Task Replan_only_updates_modified_scene_clips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ptm_replan_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var projDir = Path.Combine(tempDir, "projects", "TestProject");
        Directory.CreateDirectory(projDir);
        Directory.CreateDirectory(Path.Combine(projDir, "source"));

        try
        {
            var initialFountain = """
                Title: SELECTIVE REPLAN TEST

                INT. LIVING ROOM - DAY

                ALICE sits on the couch reading a book.

                INT. KITCHEN - DAY

                BOB pours a glass of water at the sink.

                EXT. GARDEN - DAY

                ALICE and BOB walk through the flower garden together.
                """;

            var fountainPath = Path.Combine(projDir, "source", "screenplay.fountain");
            await File.WriteAllTextAsync(fountainPath, initialFountain);

            var store = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = tempDir }));
            await OfflineTestModelConfig.ApplyAsync(store, "TestProject");
            var planner = new Stage2PlannerService(store, NullLogger<Stage2PlannerService>.Instance);

            // Approve initial screenplay
            var sign1 = ScreenplayService.SignOff(store, "TestProject");
            Assert.True(sign1.Ok, "Initial sign-off failed: " + sign1.Error);

            // 1. Initial Stage 2 plan for all scenes
            var result1 = await planner.PlanAsync("TestProject", resolution: "720p", scenes: "all");
            Assert.True(result1.Ok, "Initial plan failed");

            var blueprintPath = result1.OutPath!;
            Assert.True(File.Exists(blueprintPath));

            var json1 = await File.ReadAllTextAsync(blueprintPath);
            Assert.Contains("LIVING ROOM", json1);
            Assert.Contains("KITCHEN", json1);
            Assert.Contains("GARDEN", json1);

            using var doc1 = System.Text.Json.JsonDocument.Parse(json1);
            var scenes1 = doc1.RootElement.GetProperty("scenes");
            Assert.True(scenes1.GetArrayLength() >= 3);

            var s1_clipPrompt = scenes1[0].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();
            var s2_clipPrompt_old = scenes1[1].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();
            var s3_clipPrompt = scenes1[2].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();

            // 2. Modify ONLY Scene 2 (Kitchen) in the screenplay and approve edit
            var updatedFountain = """
                Title: SELECTIVE REPLAN TEST

                INT. LIVING ROOM - DAY

                ALICE sits on the couch reading a book.

                INT. KITCHEN - DAY

                BOB drops a plate on the floor and it shatters into pieces.

                EXT. GARDEN - DAY

                ALICE and BOB walk through the flower garden together.
                """;

            await File.WriteAllTextAsync(fountainPath, updatedFountain);
            var sign2 = ScreenplayService.SignOff(store, "TestProject");
            Assert.True(sign2.Ok, "Second sign-off failed: " + sign2.Error);

            // 3. Re-run Stage 2 plan
            var result2 = await planner.PlanAsync("TestProject", resolution: "720p", scenes: "all");
            Assert.True(result2.Ok, "Replan failed");

            var json2 = await File.ReadAllTextAsync(blueprintPath);
            using var doc2 = System.Text.Json.JsonDocument.Parse(json2);
            var scenes2 = doc2.RootElement.GetProperty("scenes");
            Assert.True(scenes2.GetArrayLength() >= 3);

            var s1_clipPrompt_new = scenes2[0].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();
            var s2_clipPrompt_new = scenes2[1].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();
            var s3_clipPrompt_new = scenes2[2].GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString();

            // 4. Assert: Unchanged scenes (Scene 1 & 3) retain identical prompts; Scene 2 prompt changes
            Assert.Equal(s1_clipPrompt, s1_clipPrompt_new);
            Assert.Equal(s3_clipPrompt, s3_clipPrompt_new);
            Assert.NotEqual(s2_clipPrompt_old, s2_clipPrompt_new);
            Assert.Contains("shatters", s2_clipPrompt_new, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
