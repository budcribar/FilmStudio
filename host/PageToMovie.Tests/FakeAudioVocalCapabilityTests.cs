using PageToMovie.Core.Models;
using PageToMovie.Fakes;
using Xunit;

namespace PageToMovie.Tests;

// See CatalogSerialCollection in SupportedModelCatalogTests.cs.
[Collection("catalog-serial")]
public class FakeAudioVocalCapabilityTests
{
    public FakeAudioVocalCapabilityTests()
    {
        SupportedModelCatalog.ReloadCatalog();
    }

    [Fact]
    public void Instrumental_always_ok()
    {
        FakeAudioClient.ValidateVocalRequest("fal-ai/stable-audio-2.0", isVocal: false);
        FakeAudioClient.ValidateVocalRequest("suno-v5-5", isVocal: false);
    }

    [Theory]
    [InlineData("suno-v5-5")]
    [InlineData("aimusicapi-suno")]
    [InlineData("elevenlabs-music")]
    public void Vocal_allowed_for_singing_providers(string model)
    {
        FakeAudioClient.ValidateVocalRequest(model, isVocal: true);
    }

    [Theory]
    [InlineData("fal-ai/stable-audio-2.0")]
    [InlineData("fal-ai/musicgen")]
    public void Vocal_rejected_for_instrumental_providers(string model)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => FakeAudioClient.ValidateVocalRequest(model, isVocal: true));
        Assert.Contains("no vocal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
