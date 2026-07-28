using ClassifierBenchmarks;
using Xunit;

namespace PageToMovie.Tests;

public class VideoTimingBenchmarkTests
{
    [Fact]
    public void VideoTimingResultRow_CalculatesDeltaCorrectly()
    {
        var row = new VideoTimingResultRow(
            Id: "cam_push_in",
            Category: "Camera Movement",
            Prompt: "Test push-in prompt",
            EstimatedDurationSec: 1.8,
            ActualDurationSec: 1.5,
            DeltaSec: -0.3,
            ConcurrencyMode: "serial",
            ConcurrencyFactor: 0.0,
            ModelUsed: "fal-ai/hunyuan-video",
            ProviderUsed: "Fal",
            ExecutionSource: "Empirical Overhead Ledger");

        Assert.Equal("cam_push_in", row.Id);
        Assert.Equal(1.8, row.EstimatedDurationSec);
        Assert.Equal(1.5, row.ActualDurationSec);
        Assert.Equal(-0.3, row.DeltaSec);
    }
}
