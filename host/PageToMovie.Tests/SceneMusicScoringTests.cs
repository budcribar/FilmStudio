using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class SceneMusicScoringTests
{
    private class FakeAudioClient : IAudioClient
    {
        public bool IsConfigured { get; set; } = true;
        public byte[] GeneratedBytes { get; set; } = new byte[] { 1, 2, 3, 4 };
        public string? LastPrompt { get; private set; }

        public Task<byte[]> GenerateMusicTrackAsync(
            string prompt,
            int durationSeconds,
            string? model = null,
            CancellationToken ct = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(GeneratedBytes);
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
        var audio = new FakeAudioClient();
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
        var audio = new FakeAudioClient { IsConfigured = false };
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
}