using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class SceneMusicScoringTests
{
    private class FakeMusicClient : IMusicClient
    {
        public bool IsConfigured { get; set; } = true;
        public byte[] GeneratedBytes { get; set; } = new byte[] { 1, 2, 3, 4 };
        public string? LastPrompt { get; private set; }

        public Task<byte[]?> GenerateMusicTrackAsync(
            string prompt,
            double durationSeconds,
            string? model = null,
            CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return Task.FromResult<byte[]?>(GeneratedBytes);
        }
    }

    private class FakeChatClient : IChatClient
    {
        public bool IsConfigured => true;
        public Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            string model,
            double temperature = 0.2,
            CancellationToken ct = default,
            string? mode = null)
        {
            return Task.FromResult("Dramatic orchestral music with low cellos at 90 BPM");
        }
    }

    [Fact]
    public async Task ProcessSceneMusicAsync_BypassesWhenAudioModelIsNone()
    {
        var chat = new FakeChatClient();
        var audio = new FakeMusicClient();
        var service = new SceneMusicScoringService(chat, audio, NullLogger<SceneMusicScoringService>.Instance);

        var cfg = new Dictionary<string, JsonElement>
        {
            ["audio_model_name"] = JsonSerializer.SerializeToElement("none"),
        };

        var tempVideoPath = Path.GetTempFileName();
        try
        {
            var res = await service.ProcessSceneMusicAsync(
                Path.GetTempPath(),
                sceneNumber: 1,
                inputSceneMp4Path: tempVideoPath,
                outputSceneMp4Path: Path.GetTempFileName(),
                screenplayText: "INT. CASTLE - NIGHT",
                durationSeconds: 10,
                config: cfg);

            Assert.Null(res);
        }
        finally
        {
            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
        }
    }

    [Fact]
    public async Task ProcessSceneMusicAsync_BypassesWhenAudioClientIsNotConfigured()
    {
        var chat = new FakeChatClient();
        var audio = new FakeMusicClient { IsConfigured = false };
        var service = new SceneMusicScoringService(chat, audio, NullLogger<SceneMusicScoringService>.Instance);

        var cfg = new Dictionary<string, JsonElement>
        {
            ["audio_model_name"] = JsonSerializer.SerializeToElement("fal-ai/stable-audio"),
        };

        var tempVideoPath = Path.GetTempFileName();
        try
        {
            var res = await service.ProcessSceneMusicAsync(
                Path.GetTempPath(),
                sceneNumber: 1,
                inputSceneMp4Path: tempVideoPath,
                outputSceneMp4Path: Path.GetTempFileName(),
                screenplayText: "INT. CASTLE - NIGHT",
                durationSeconds: 10,
                config: cfg);

            Assert.Null(res);
        }
        finally
        {
            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
        }
    }

    [Fact]
    public async Task GenerateProjectSceneAudioAsync_SynthesizesAudioFromBlueprint_AndSkipsExisting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SceneMusicBatch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var blueprintPath = Path.Combine(tempDir, "blueprint.clips.grok.json");
            var json = """
            {
              "scenes": [
                {
                  "scene_number": 1,
                  "total_estimated_duration_seconds": 12,
                  "music_prompt": "Fragile solo piano leitmotif in a minor key"
                },
                {
                  "scene_number": 2,
                  "total_estimated_duration_seconds": 8,
                  "music_prompt": "Tense creeping string pulse"
                }
              ]
            }
            """;
            await File.WriteAllTextAsync(blueprintPath, json);

            var chat = new FakeChatClient();
            var audio = new FakeMusicClient();
            var service = new SceneMusicScoringService(chat, audio, NullLogger<SceneMusicScoringService>.Instance);

            // Pre-create scene 1 audio to verify skip logic
            var assetsDir = Path.Combine(tempDir, "assets");
            Directory.CreateDirectory(assetsDir);
            var existingMp3 = Path.Combine(assetsDir, "scene_01_music.mp3");
            await File.WriteAllBytesAsync(existingMp3, new byte[] { 99, 99 });

            var generatedCount = await service.GenerateProjectSceneAudioAsync(tempDir);

            // Scene 1 should be skipped, Scene 2 should be generated -> total 1 new
            Assert.Equal(1, generatedCount);
            Assert.True(File.Exists(Path.Combine(assetsDir, "scene_02_music.mp3")));
            Assert.Equal("Tense creeping string pulse", audio.LastPrompt);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}