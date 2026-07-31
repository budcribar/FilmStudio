using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace PageToMovie.Tests.LiveApi;

/// <summary>
/// Live API test for Suno background music generation via aimusicapi.ai.
/// Requires AIMUSICAPI_API_KEY or Suno_API_KEY env var and PAGETOMOVIE_LIVE_API_TESTS=1.
/// </summary>
[Trait("Category", LiveApiGate.Category)]
public class SunoMusicLiveApiTests
{
    private readonly ITestOutputHelper _output;

    public SunoMusicLiveApiTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [LiveApiFact]
    public async Task Live_generate_tell_tale_heart_scene_music()
    {
        var rawKey = Environment.GetEnvironmentVariable("AIMUSICAPI_API_KEY")
            ?? Environment.GetEnvironmentVariable("Suno_API_Key")
            ?? Environment.GetEnvironmentVariable("SUNO_API_KEY");

        Assert.False(string.IsNullOrWhiteSpace(rawKey), "No API key found in AIMUSICAPI_API_KEY, Suno_API_Key, or SUNO_API_KEY env vars.");
        var apiKey = rawKey!.Trim(' ', '"', '\'', '\r', '\n', '\t');

        using var http = new HttpClient();
        using var scope = ApiKeyScope.Push(new Dictionary<string, string?> { ["aimusicapi"] = apiKey });

        var client = new AiMusicApiClient(http, NullLogger<AiMusicApiClient>.Instance);

        Assert.True(client.IsConfigured, "AiMusicApiClient should be configured when API key is provided.");

        var prompt = "Dark gothic orchestral strings, low muffled bass pulse mimicking a beating heart, creeping tension, 19th-century psychological horror atmosphere, suspenseful building rhythm";
        string? progressMsg = null;

        var audioUrl = await client.GenerateMusicTrackAsync(
            prompt: prompt,
            durationSeconds: 30,
            model: "chirp-v5",
            ct: default,
            onProgress: msg => progressMsg = msg,
            isVocal: false);

        _output.WriteLine($"[SUNO GENERATED AUDIO URL]: {audioUrl}");
        Console.WriteLine($"[SUNO GENERATED AUDIO URL]: {audioUrl}");

        Assert.False(string.IsNullOrWhiteSpace(audioUrl), $"AiMusicApiClient should return a valid audio URL. Last status: {progressMsg}");
        Assert.StartsWith("http", audioUrl, StringComparison.OrdinalIgnoreCase);
    }
}
