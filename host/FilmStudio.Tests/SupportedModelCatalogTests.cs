using FilmStudio.Core.Models;
using Xunit;

namespace FilmStudio.Tests;

public class SupportedModelCatalogTests
{
    [Fact]
    public void Video_default_is_grok_imagine_video()
    {
        var m = SupportedModelCatalog.ResolveOrDefault(null, ModelCapability.Video);
        Assert.Equal("grok-imagine-video", m.Id);
        Assert.Equal(ModelProviderFamily.Xai, m.Provider);
        Assert.Contains("XAI_API_KEY", m.RequiredEnvKeys);
        Assert.Equal("videos/generations", m.EndpointPath);
    }

    [Fact]
    public void Model_id_implies_provider_without_service_dropdown()
    {
        Assert.Equal("grok", SupportedModelCatalog.LegacyProviderFor(
            "grok-imagine-image-quality", ModelCapability.Image));
        Assert.Equal("grok", SupportedModelCatalog.LegacyProviderFor(
            "grok-4.5", ModelCapability.Chat));
    }

    [Fact]
    public void Enabled_video_models_are_nonempty()
    {
        var list = SupportedModelCatalog.ForCapability(ModelCapability.Video);
        Assert.NotEmpty(list);
        Assert.All(list, e => Assert.True(e.Enabled));
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var m = SupportedModelCatalog.Find("Grok-Imagine-Video", ModelCapability.Video);
        Assert.NotNull(m);
        Assert.Equal("grok-imagine-video", m!.Id);
    }
}
