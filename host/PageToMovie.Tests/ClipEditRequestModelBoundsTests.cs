using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ClipEditRequestModelBoundsTests
{
    private static ClipEditRequest ValidRequest(int durationSeconds) => new()
    {
        ProjectId = "p1",
        Scene = 1,
        Clip = 1,
        VisualPrompt = "A character stands in a room.",
        DurationSeconds = durationSeconds,
    };

    [Fact]
    public void ValidateClipEditRequest_UsesGlobalDefaultsWhenBoundsOmitted()
    {
        var fields = ValidRequest(ClipDurationEstimator.AbsMaxSeconds + 1);
        Assert.Throws<InvalidOperationException>(() => ProjectStore.ValidateClipEditRequest(fields));
    }

    [Fact]
    public void ValidateClipEditRequest_HonorsNarrowerModelAbsMax()
    {
        // A duration that's fine under the global default AbsMaxSeconds must still be rejected
        // when the caller passes a narrower model-specific ceiling.
        var narrowAbsMax = ClipDurationEstimator.AbsMaxSeconds - 2;
        var fields = ValidRequest(ClipDurationEstimator.AbsMaxSeconds - 1);

        Assert.Throws<InvalidOperationException>(() =>
            ProjectStore.ValidateClipEditRequest(fields, absMaxSeconds: narrowAbsMax));
    }

    [Fact]
    public void ValidateClipEditRequest_HonorsWiderModelMin()
    {
        // A duration that's fine under the global default MinSeconds must still be rejected
        // when the caller passes a higher model-specific floor.
        var fields = ValidRequest(ClipDurationEstimator.MinSeconds);
        var widerMin = ClipDurationEstimator.MinSeconds + 2;

        Assert.Throws<InvalidOperationException>(() =>
            ProjectStore.ValidateClipEditRequest(fields, minSeconds: widerMin));
    }

    [Fact]
    public void ValidateClipEditRequest_AllowsDurationWithinResolvedModelBounds()
    {
        var fields = ValidRequest(ClipDurationEstimator.MinSeconds);
        var exception = Record.Exception(() =>
            ProjectStore.ValidateClipEditRequest(fields, minSeconds: ClipDurationEstimator.MinSeconds));
        Assert.Null(exception);
    }
}
