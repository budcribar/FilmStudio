using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class CostReportServiceTests
{
    [Fact]
    public void BuildVideoBaseRateTable_ReturnsEmptyForPerSecondOnlyModel()
    {
        // Grok/Veo are genuinely priced per second — no flat fee, no guessed base cost.
        var grok = SupportedModelCatalog.ResolveOrDefault("grok-imagine-video", ModelCapability.Video);
        var table = CostReportService.BuildVideoBaseRateTable(grok);
        Assert.Empty(table);
    }

    [Theory]
    [InlineData("hunyuan-video", "720p", 0.40)]
    [InlineData("fal-ai/wan-2.1", "480p", 0.20)]
    [InlineData("fal-ai/wan-2.1", "720p", 0.40)]
    public void BuildVideoBaseRateTable_ReturnsRealFlatFeeForFrameCountBasedModels(
        string modelId, string resolution, double expectedBase)
    {
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Video);
        var table = CostReportService.BuildVideoBaseRateTable(entry);
        Assert.Equal(expectedBase, table[resolution]);
    }

    [Fact]
    public void RatesFromModels_PriceVideo_UsesFlatFeeNotPerSecondForHunyuan()
    {
        var rates = CostReportService.RatesFromModels("hunyuan-video", "grok-imagine-image-quality");
        // A 5s and an 8s clip must cost the SAME — Hunyuan bills per generation, not per second.
        var five = CostReportService.PriceVideo(5, "720p", rates, hasRef: false, isExtend: false, attempts: 1);
        var eight = CostReportService.PriceVideo(8, "720p", rates, hasRef: false, isExtend: false, attempts: 1);
        Assert.Equal(0.40, five.Usd);
        Assert.Equal(0.40, eight.Usd);
    }

    [Fact]
    public void RatesFromModels_PriceVideo_StillScalesWithDurationForGrok()
    {
        // Grok is genuinely per-second — a longer clip must cost more, unlike Hunyuan/Wan.
        var rates = CostReportService.RatesFromModels("grok-imagine-video", "grok-imagine-image-quality");
        var five = CostReportService.PriceVideo(5, "720p", rates, hasRef: false, isExtend: false, attempts: 1);
        var eight = CostReportService.PriceVideo(8, "720p", rates, hasRef: false, isExtend: false, attempts: 1);
        Assert.True(eight.Usd > five.Usd);
    }
}
