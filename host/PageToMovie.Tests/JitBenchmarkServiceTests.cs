using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class JitBenchmarkServiceTests
{
    private sealed class TestChatClient : IChatClient
    {
        public bool IsConfigured => true;
        public string ResponseToReturn { get; set; } = "";

        public Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            string model = "grok-4.5",
            double temperature = 0.2,
            CancellationToken ct = default,
            string? mode = null)
        {
            return Task.FromResult(ResponseToReturn);
        }
    }

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
    public async Task AiActionOverheadClassifierAsync_InvokesLlmClassifierWhenAvailable()
    {
        var ledger = new ActionCameraOverheadLedger();
        var router = new SmartClassifierModelRouter();
        var testChat = new TestChatClient
        {
            ResponseToReturn = """
            {
              "matchCategoryId": "act_stabbing",
              "estimatedOverheadSec": 3.1,
              "confidenceScore": 0.98,
              "explanation": "Llm classified as physical stabbing."
            }
            """
        };

        var classifier = new AiActionOverheadClassifier(router, ledger, testChat);
        var estimation = await classifier.ClassifyNovelActionAsync("Lunges forward with a spear", "(screaming)");

        Assert.Equal("act_stabbing", estimation.MatchCategoryId);
        Assert.Equal(3.1, estimation.EstimatedOverheadSec);
        Assert.Equal(0.98, estimation.ConfidenceScore);
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
