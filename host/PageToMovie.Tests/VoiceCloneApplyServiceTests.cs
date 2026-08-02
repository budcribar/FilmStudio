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
        IVoiceClient voices = new ElevenLabsVoiceClient(http, NullLogger<ElevenLabsVoiceClient>.Instance, allowMockFallback: true);
        _apply = new VoiceCloneApplyService(_store, voices, NullLogger<VoiceCloneApplyService>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task ApplyFromSample_writes_provider_voice_id_and_tts_preview()
    {
        var p = await _store.CreateProjectAsync("tthv7", title: "Tell-Tale Heart V7");

        var sample = MockToneWav.Sine(2.5, 195);
        var result = await _apply.ApplyFromSampleAsync(
            p.Id,
            "Character_Narrator",
            sampleOverride: sample,
            sampleFileName: "voice_clone_sample.wav",
            previewText: "True! nervous — very, very dreadfully nervous I had been and am.");

        Assert.True(result.Ok, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderVoiceId));
        Assert.True(result.UsedMock);
        Assert.Equal("elevenlabs", result.ProviderId);
        Assert.True(File.Exists(_store.GetVoiceCloneSamplePath(p.Id, "Character_Narrator")));
        var preview = _apply.GetTtsPreviewPath(p.Id, "Character_Narrator");
        Assert.True(File.Exists(preview));
        Assert.True(new FileInfo(preview!).Length > 44);
    }
}
