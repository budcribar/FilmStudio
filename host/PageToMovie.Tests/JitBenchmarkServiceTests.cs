using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class JitBenchmarkServiceTests
{
    [Fact]
    public void AiActionOverheadClassifier_ClassifiesWeaponActionCorrectly()
    {
        var ledger = new ActionCameraOverheadLedger();
        var router = new SmartClassifierModelRouter();
        var classifier = new AiActionOverheadClassifier(router, ledger);

        var estimation = classifier.ClassifyNovelAction("Pulls out a rusty blade", "(clicks open)");

        Assert.Equal("act_knife_pull", estimation.MatchCategoryId);
        Assert.Equal(1.9, estimation.EstimatedOverheadSec);
        Assert.True(estimation.ConfidenceScore >= 0.85);
    }

    [Fact]
    public async Task EnsureBeatCalibratedAsync_ReturnsJitResultWithFallbackWhenKeysMissing()
    {
        var ledger = new ActionCameraOverheadLedger();
        var router = new SmartClassifierModelRouter();
        var classifier = new AiActionOverheadClassifier(router, ledger);
        var jitService = new JitBenchmarkService(ledger, classifier);

        var result = await jitService.EnsureBeatCalibratedAsync("Sorting pill bottles on the counter", "(while speaking)");

        Assert.Equal("act_pills_sorting", result.CategoryId);
        Assert.Equal(2.9, result.MeasuredOverheadSec);
        Assert.Equal(0.85, result.OverlapRatioGamma);
    }
}
