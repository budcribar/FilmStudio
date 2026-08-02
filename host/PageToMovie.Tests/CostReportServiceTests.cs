using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Covers the video reference-image / extend-per-second pricing add-ons in
/// <see cref="CostReportService"/>: catalog-sourced values must win when a model publishes them,
/// and the small estimated fallback constants must apply (with the right "pricing_source" flags)
/// when the catalog has no verified number — which, as of 2026-08, is every enabled video model
/// including xAI's grok-imagine-video (no separate line item published on
/// docs.x.ai/developers/pricing for reference images or video-extend).
/// </summary>
public sealed class CostReportServiceTests : IDisposable
{
    public CostReportServiceTests()
    {
        SupportedModelCatalog.ReloadCatalog();
    }

    public void Dispose()
    {
        // Undo any TryLoadFromJson swap so later test classes see the real on-disk catalog again.
        SupportedModelCatalog.ReloadCatalog();
    }

    private const string SyntheticCatalogJson = """
    {
      "models": [
        {
          "id": "test-video-with-addons",
          "displayName": "Test Video With Addons",
          "capability": "Video",
          "provider": "Xai",
          "apiBase": "https://api.x.ai/v1",
          "endpointPath": "videos/generations",
          "requiredEnvKeys": ["XAI_API_KEY"],
          "enabled": true,
          "supportsVideoContinue": true,
          "videoCostPerSecondByResolution": {"480p": 0.05, "720p": 0.07, "1080p": 0.25},
          "videoReferenceImageCost": 0.003,
          "videoExtendCostPerSecond": 0.015
        },
        {
          "id": "test-video-no-addons",
          "displayName": "Test Video No Addons",
          "capability": "Video",
          "provider": "Xai",
          "apiBase": "https://api.x.ai/v1",
          "endpointPath": "videos/generations",
          "requiredEnvKeys": ["XAI_API_KEY"],
          "enabled": true,
          "supportsVideoContinue": true,
          "videoCostPerSecondByResolution": {"480p": 0.05, "720p": 0.07, "1080p": 0.25}
        },
        {
          "id": "test-image",
          "displayName": "Test Image",
          "capability": "Image",
          "provider": "Xai",
          "apiBase": "https://api.x.ai/v1",
          "endpointPath": "images/generations",
          "requiredEnvKeys": ["XAI_API_KEY"],
          "enabled": true,
          "imageCostPerImage": 0.05
        }
      ]
    }
    """;

    [Fact]
    public void RatesFromModels_prefers_catalog_ref_image_and_extend_cost_when_published()
    {
        Assert.True(SupportedModelCatalog.TryLoadFromJson(SyntheticCatalogJson));

        var rates = CostReportService.RatesFromModels("test-video-with-addons", "test-image");

        Assert.Equal(0.003, Assert.IsType<double>(rates["video_input_image"]));
        Assert.Equal("model_catalog", rates["video_input_image_source"]);
        Assert.Equal(0.015, Assert.IsType<double>(rates["video_input_per_sec"]));
        Assert.Equal("model_catalog", rates["video_input_per_sec_source"]);
        Assert.Equal("model_catalog", rates["video_pricing_source"]);
    }

    [Fact]
    public void RatesFromModels_falls_back_to_constants_when_catalog_has_no_addon_pricing()
    {
        Assert.True(SupportedModelCatalog.TryLoadFromJson(SyntheticCatalogJson));

        var rates = CostReportService.RatesFromModels("test-video-no-addons", "test-image");

        Assert.Equal(CostReportService.FallbackVideoRefImageCost, Assert.IsType<double>(rates["video_input_image"]));
        Assert.Equal("estimated_fallback", rates["video_input_image_source"]);
        Assert.Equal(CostReportService.FallbackVideoExtendCostPerSec, Assert.IsType<double>(rates["video_input_per_sec"]));
        Assert.Equal("estimated_fallback", rates["video_input_per_sec_source"]);
        // Output-by-resolution is real catalog data, but the two add-ons are estimated, so overall
        // video pricing for this model is only partially real.
        Assert.Equal("estimated_fallback", rates["video_pricing_source"]);
    }

    [Fact]
    public void PriceVideo_uses_catalog_ref_image_and_extend_cost_in_the_math()
    {
        Assert.True(SupportedModelCatalog.TryLoadFromJson(SyntheticCatalogJson));
        var rates = CostReportService.RatesFromModels("test-video-with-addons", "test-image");

        var priced = CostReportService.PriceVideo(
            durationSec: 10,
            resolution: "720p",
            rates: rates,
            hasRef: true,
            isExtend: true,
            attempts: 1);

        Assert.Equal(0.003, priced.RefImg);
        Assert.Equal(0.15, priced.ExtendIn); // 10 sec * $0.015/sec catalog extend rate * 1 attempt
    }

    [Fact]
    public void Live_grok_imagine_video_has_no_published_ref_image_or_extend_pricing_yet()
    {
        // Documents the real-world finding: as of 2026-08, xAI (the only enabled model with
        // SupportsVideoContinue=true) publishes only a flat per-second rate on
        // docs.x.ai/developers/pricing — no separate line item for reference images or video-extend.
        // If this ever flips to non-null, CostReportServiceTests above show the pricing math already
        // prefers a real catalog value automatically.
        var video = SupportedModelCatalog.Find("grok-imagine-video", ModelCapability.Video);
        Assert.NotNull(video);
        Assert.Null(video!.VideoReferenceImageCost);
        Assert.Null(video.VideoExtendCostPerSecond);

        var rates = CostReportService.RatesFromModels("grok-imagine-video", "grok-imagine-image-quality");
        Assert.Equal("estimated_fallback", rates["video_input_image_source"]);
        Assert.Equal("estimated_fallback", rates["video_input_per_sec_source"]);
    }
}
