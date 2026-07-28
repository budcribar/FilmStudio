using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ActionCameraOverheadLedgerTests
{
    [Fact]
    public void GetOverheadSec_ReturnsEmpiricalBenchmarkValue()
    {
        var ledger = new ActionCameraOverheadLedger();

        Assert.Equal(1.6, ledger.GetOverheadSec("cam_push_in"));
        Assert.Equal(1.9, ledger.GetOverheadSec("act_knife_pull"));
        Assert.Equal(3.1, ledger.GetOverheadSec("act_stabbing"));
    }

    [Fact]
    public void CalculateEffectiveSpeechWindowSec_DeductsCameraAndActionOverheads_SerialMode()
    {
        var ledger = new ActionCameraOverheadLedger();

        // 5.0s clip - 1.6s push-in - (1.0 * 1.9s knife pull) = 1.5s remaining for speech
        double speechWindow = ledger.CalculateEffectiveSpeechWindowSec(
            totalClipDurationSec: 5.0,
            cameraCategoryId: "cam_push_in",
            actionCategoryId: "act_knife_pull",
            concurrencyFactorGamma: 0.0);

        Assert.Equal(1.5, speechWindow, 1);
    }

    [Fact]
    public void CalculateEffectiveSpeechWindowSec_AppliesConcurrencyFactor_ConcurrentMode()
    {
        var ledger = new ActionCameraOverheadLedger();

        // 5.0s clip - 1.6s push-in - ((1 - 0.85) * 2.9s pills sorting) = 5.0 - 1.6 - 0.435 = 2.965s
        double speechWindow = ledger.CalculateEffectiveSpeechWindowSec(
            totalClipDurationSec: 5.0,
            cameraCategoryId: "cam_push_in",
            actionCategoryId: "act_pills_sorting",
            concurrencyFactorGamma: 0.85);

        Assert.Equal(2.965, speechWindow, 3);
    }

    [Fact]
    public void ActionConcurrencyAnalyzer_DetectsConcurrentAndSerialVerbs()
    {
        var concurrentResult = ActionConcurrencyAnalyzer.AnalyzeBeat("Pacing nervously across the room", "(while sorting pills)");
        Assert.Equal("concurrent", concurrentResult.Mode);
        Assert.Equal(0.85, concurrentResult.OverlapRatioGamma);

        var serialResult = ActionConcurrencyAnalyzer.AnalyzeBeat("Reaches into jacket and pulls out switchblade", "(pauses, then speaks)");
        Assert.Equal("serial", serialResult.Mode);
        Assert.Equal(0.0, serialResult.OverlapRatioGamma);
    }
}
