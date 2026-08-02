using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class VoiceCloneApplyServiceTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;
    private readonly VoiceCloneApplyService _apply;

    public VoiceCloneApplyServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm-voice-apply-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        _store = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _root }));
        var http = new HttpClient { BaseAddress = new Uri("https://api.elevenlabs.io/v1/") };
        IVoiceClient eleven = new ElevenLabsVoiceClient(http, NullLogger<ElevenLabsVoiceClient>.Instance, allowMockFallback: true);
        // Fal not configured → route falls back to ElevenLabs mock
        var falHttp = new HttpClient { BaseAddress = new Uri("https://queue.fal.run/") };
        IVoiceCloneClient fal = new FalVoiceCloneClient(falHttp, NullLogger<FalVoiceCloneClient>.Instance);
        var httpFactory = new SimpleFactory();
        _apply = new VoiceCloneApplyService(
            _store, eleven, fal, httpFactory, NullLogger<VoiceCloneApplyService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task ApplyFromSample_routes_to_eleven_mock_without_keys()
    {
        var p = await _store.CreateProjectAsync("tthv7", title: "Tell-Tale Heart V7");
        await _store.SaveConfigAsync(p.Id, JsonSerializer.SerializeToElement(new { voice_model_name = "eleven_voice_clone" }));

        var sample = MockToneWav.Sine(2.5, 195);
        var result = await _apply.ApplyFromSampleAsync(
            p.Id,
            "Character_Narrator",
            sampleOverride: sample,
            sampleFileName: "voice_clone_sample.wav",
            previewText: "True! nervous — very, very dreadfully nervous I had been and am.");

        Assert.True(result.Ok, result.Error);
        Assert.Equal("elevenlabs", result.ProviderId);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderVoiceId));
        Assert.True(result.UsedMock);
        Assert.True(File.Exists(_store.GetVoiceCloneSamplePath(p.Id, "Character_Narrator")));
        var preview = _apply.GetTtsPreviewPath(p.Id, "Character_Narrator");
        Assert.True(File.Exists(preview));
    }

    [Fact]
    public async Task ApplyFromSample_fal_model_without_key_falls_back_to_eleven_mock()
    {
        var p = await _store.CreateProjectAsync("buster", title: "Buster");
        await _store.SaveConfigAsync(p.Id, JsonSerializer.SerializeToElement(new { voice_model_name = "fal-ai/minimax/voice-clone" }));

        var sample = MockToneWav.Sine(2.0, 180);
        var result = await _apply.ApplyFromSampleAsync(
            p.Id,
            "Character_Narrator",
            sampleOverride: sample,
            sampleFileName: "voice_clone_sample.wav");

        // No FAL key → ElevenLabs mock fallback when eleven mock allowed
        Assert.True(result.Ok, result.Error);
        Assert.Equal("elevenlabs", result.ProviderId);
        Assert.True(result.UsedMock);
    }

    private sealed class SimpleFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
