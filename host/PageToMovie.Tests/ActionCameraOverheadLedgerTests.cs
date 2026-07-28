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
        Assert.Equal(2.0, ledger.GetOverheadSec("act_knife_pull"));
        Assert.Equal(3.1, ledger.GetOverheadSec("act_stabbing"));
    }

    [Fact]
    public void CalculateEffectiveSpeechWindowSec_DeductsCameraAndActionOverheads()
    {
        var ledger = new ActionCameraOverheadLedger();

        // 5.0s clip - 1.6s push-in - 2.0s knife pull = 1.4s remaining for speech
        double speechWindow = ledger.CalculateEffectiveSpeechWindowSec(5.0, "cam_push_in", "act_knife_pull");
        Assert.Equal(1.4, speechWindow, 1);
    }

    [Fact]
    public void ExceedsSpeechCapacity_DetectsOverloadedDialogue()
    {
        var ledger = new ActionCameraOverheadLedger();

        // 1.4s speech window @ 2.6 words/sec = max ~3 words allowed.
        // 15 words exceeds capacity!
        bool overloaded = ledger.ExceedsSpeechCapacity(
            dialogueWordCount: 15,
            totalClipDurationSec: 5.0,
            cameraCategoryId: "cam_push_in",
            actionCategoryId: "act_knife_pull",
            wordsPerSecond: 2.6);

        Assert.True(overloaded);
    }
}
