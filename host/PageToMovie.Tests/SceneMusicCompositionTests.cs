using System.Text.Json;
using System.Text.Json.Nodes;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace PageToMovie.Tests;

public class SceneMusicCompositionTests
{
    [Fact]
    public async Task AugmentProjectMusicAsync_injects_music_scores_non_destructively()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AugmentMusicTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var bpPath = Path.Combine(tempDir, "blueprint.clips.grok.json");
            var initialJson = """
                {
                  "schema_version": "blueprint_clips.v1",
                  "project_id": "TestProject",
                  "scenes": [
                    {
                      "scene_number": 1,
                      "slugline": "INT. OLD MAN'S ROOM - NIGHT",
                      "content": "The narrator peers through the doorway into the dark room.",
                      "total_estimated_duration_seconds": 12.0,
                      "veo_clips": [
                        {
                          "clip_number": 1,
                          "visual_prompt": "A shadowy room with a wooden door opening slowly.",
                          "duration_seconds": 6.0
                        }
                      ]
                    },
                    {
                      "scene_number": 2,
                      "slugline": "INT. OLD MAN'S ROOM - CONTINUOUS",
                      "content": "The lantern ray shines upon the pale blue eye.",
                      "total_estimated_duration_seconds": 10.0,
                      "veo_clips": [
                        {
                          "clip_number": 1,
                          "visual_prompt": "A sharp beam of lantern light illuminating a milky eye.",
                          "duration_seconds": 5.0
                        }
                      ]
                    }
                  ]
                }
                """;

            await File.WriteAllTextAsync(bpPath, initialJson);

            var telemetryRoot = Path.Combine(Path.GetTempPath(), "AugmentMusicTest_tel_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(telemetryRoot, "prompts"));
            var telemetryStore = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = telemetryRoot }));
            var telemetry = new ProjectTelemetryService(telemetryStore, NullLogger<ProjectTelemetryService>.Instance);
            var fakeVision = new FakeGrokVisionClient(NullLogger<FakeGrokVisionClient>.Instance, telemetry);
            var composer = new SceneMusicCompositionService(fakeVision, NullLogger<SceneMusicCompositionService>.Instance);

            var ok = await composer.AugmentProjectMusicAsync(
                tempDir,
                userModel: OfflineTestModelConfig.Required("chat"));
            Assert.True(ok);

            var updatedText = await File.ReadAllTextAsync(bpPath);
            var root = JsonNode.Parse(updatedText) as JsonObject;
            Assert.NotNull(root);

            var scenes = root["scenes"] as JsonArray;
            Assert.NotNull(scenes);
            Assert.Equal(2, scenes.Count);

            var s1 = scenes[0] as JsonObject;
            Assert.NotNull(s1);
            Assert.Equal(1, s1["scene_number"]?.GetValue<int>());
            Assert.Equal("INT. OLD MAN'S ROOM - NIGHT", s1["slugline"]?.GetValue<string>());
            
            // Verify music_score augmentation
            var ms1 = s1["music_score"] as JsonObject;
            Assert.NotNull(ms1);
            Assert.Equal("Dark orchestral theme with low cello and tense pulse.", ms1["prompt"]?.GetValue<string>());
            Assert.Equal("Thriller", ms1["genre"]?.GetValue<string>());
            Assert.Equal("Tense", ms1["mood"]?.GetValue<string>());
            Assert.Equal("90 BPM", ms1["tempo"]?.GetValue<string>());

            // Verify original clip array remains intact
            var clips = s1["veo_clips"] as JsonArray;
            Assert.NotNull(clips);
            Assert.Single(clips);
            Assert.Equal("A shadowy room with a wooden door opening slowly.", clips[0]?["visual_prompt"]?.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }
    }
}
