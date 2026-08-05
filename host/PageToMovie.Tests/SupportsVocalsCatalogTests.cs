using PageToMovie.Core.Models;
using PageToMovie.Engine;
using PageToMovie.Fakes;
using Xunit;

namespace PageToMovie.Tests;

public class SupportsVocalsCatalogTests
{
    public SupportsVocalsCatalogTests()
    {
        SupportedModelCatalog.ReloadCatalog();
    }

    [Theory]
    [InlineData("suno-v5-5", true)]
    [InlineData("aimusicapi-suno", true)]
    [InlineData("elevenlabs-music", true)]
    [InlineData("fal-ai/stable-audio-2.0", false)]
    [InlineData("fal-ai/musicgen", false)]
    public void Catalog_SupportsVocals_matches_expectation(string modelId, bool expected)
    {
        var e = SupportedModelCatalog.Find(modelId, ModelCapability.Audio);
        Assert.NotNull(e);
        Assert.Equal(expected, e!.SupportsVocals);
    }

    [Fact]
    public void Dto_round_trips_SupportsVocals()
    {
        var e = SupportedModelCatalog.Find("suno-v5-5", ModelCapability.Audio)!;
        var dto = SupportedModelCatalog.ToDto(e);
        Assert.True(dto.SupportsVocals);
        var back = SupportedModelCatalog.FromDto(dto);
        Assert.True(back.SupportsVocals);
    }

    [Fact]
    public void Image_enabled_models_have_maxReferenceImages()
    {
        var images = SupportedModelCatalog.ForCapability(ModelCapability.Image, enabledOnly: true);
        Assert.NotEmpty(images);
        Assert.All(images, e => Assert.True(
            e.MaxReferenceImages is not null,
            $"{e.Id} missing maxReferenceImages"));
    }

    [Fact]
    public void ImageApiLimits_uses_catalog_not_provider_for_gemini()
    {
        var n = ImageApiLimits.MaxReferenceImages("grok", "gemini-2.5-pro-image");
        Assert.Equal(14, n); // catalog value, even if provider string is wrong
    }

    [Fact]
    public void ImageApiLimits_flux_from_catalog()
    {
        var n = ImageApiLimits.MaxReferenceImages(null, "fal-ai/flux/dev");
        Assert.Equal(1, n);
    }
}
