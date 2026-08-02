using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// API usage / error logs must stamp provider + model from models_catalog.json only.
/// </summary>
public class CatalogTelemetryIdentityTests
{
    [Fact]
    public void ApplyCatalogIdentity_overwrites_hardcoded_provider_from_catalog()
    {
        var rec = new ApiCallTelemetry
        {
            Kind = "chat",
            Model = "grok-4.5",
            Provider = "xai-not-real",
        };

        ProjectTelemetryService.ApplyCatalogIdentity(rec);

        Assert.Equal("grok-4.5", rec.Model);
        Assert.Equal("grok", rec.Provider);
    }

    [Fact]
    public void ApplyCatalogIdentity_sets_provider_when_caller_omits_it()
    {
        var rec = new ApiCallTelemetry
        {
            Kind = "video",
            Model = "grok-imagine-video",
        };

        ProjectTelemetryService.ApplyCatalogIdentity(rec);

        Assert.Equal("grok", rec.Provider);
        Assert.Equal("grok-imagine-video", rec.Model);
    }

    [Fact]
    public void ApplyCatalogIdentity_elevenlabs_voice_from_catalog()
    {
        var rec = new ApiCallTelemetry
        {
            Kind = "voice_clone",
            Model = "eleven_voice_clone",
            Provider = "wrong",
        };

        ProjectTelemetryService.ApplyCatalogIdentity(rec);

        Assert.Equal("elevenlabs", rec.Provider);
    }

    [Fact]
    public void ApplyCatalogIdentity_unknown_model_drops_invented_provider()
    {
        var rec = new ApiCallTelemetry
        {
            Kind = "chat",
            Model = "not-a-real-model-xyz",
            Provider = "made-up-provider",
        };

        ProjectTelemetryService.ApplyCatalogIdentity(rec);

        Assert.Null(rec.Provider);
    }

    [Fact]
    public void GenerationError_ApplyCatalogIdentity_uses_catalog_provider()
    {
        var rec = new GenerationErrorRecord
        {
            Stage = "test",
            ErrorType = "exception",
            Model = "claude-sonnet-5",
            Provider = "claude", // alias — must become catalog id "anthropic"
        };

        GenerationErrorLogger.ApplyCatalogIdentity(rec);

        Assert.Equal("claude-sonnet-5", rec.Model);
        Assert.Equal("anthropic", rec.Provider);
    }

    [Fact]
    public void EstimateListRateUsd_unknown_model_returns_null_not_guess()
    {
        var usd = ProjectTelemetryService.EstimateListRateUsd(new ApiCallTelemetry
        {
            Kind = "image",
            Model = "totally-fake-image-model",
            ImageCount = 3,
        });
        Assert.Null(usd);
    }

    [Fact]
    public void CatalogProviderId_music_models_match_catalog_providers()
    {
        var suno = SupportedModelCatalog.CatalogProviderId("suno-v5-5", "audio");
        var aim = SupportedModelCatalog.CatalogProviderId("aimusicapi-suno", "audio");
        Assert.Equal("suno", suno);
        Assert.Equal("aimusicapi", aim);
    }
}
