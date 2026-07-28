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

    private sealed class TestVideoClient : IVideoClient
    {
        public bool IsConfigured => true;
        public bool GenerationSubmitted { get; private set; }

        public Task<string> SubmitGenerationAsync(
            string prompt,
            int durationSeconds,
            string resolution,
            string model,
            CancellationToken ct,
            IReadOnlyList<string>? referenceImagePaths = null,
            string? startFrameImagePath = null,
            string? continueFromVideoPath = null)
        {
            GenerationSubmitted = true;
            return Task.FromResult("jit_req_12345");
        }

        public Task<string> PollForVideoUrlAsync(
            string requestId,
            Action<string>? onProgress,
            CancellationToken ct)
        {
            return Task.FromResult("https://v3.fal.media/tokens/output.mp4");
        }

        public Task DownloadToFileAsync(string url, string destPath, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestVisionClient : IVisionClient
    {
        public bool IsConfigured => true;
        public bool VisionInvoked { get; private set; }

        public Task<string> TranscribePageAsync(string imagePath, int page, string model = "grok-4.5", CancellationToken ct = default)
            => Task.FromResult("");

        public Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(string imagePath, int page, IReadOnlyList<CharacterClassifyHint> cast, string model = "grok-4.5", CancellationToken ct = default)
            => Task.FromResult(new CharacterPageClassification());

        public Task<string> CompleteWithImagesAsync(string prompt, IReadOnlyList<string> imagePaths, string model = "grok-4.5", string detail = "low", CancellationToken ct = default)
        {
            VisionInvoked = true;
            return Task.FromResult("""
            {
              "actionCompletionSec": 2.15,
              "confidence": 0.96,
              "explanation": "Verified physical action completes at 2.15s."
            }
            """);
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
    public async Task EnsureBeatCalibratedAsync_ExecutesLiveJitAndVisionAnalysis()
    {
        var ledger = new ActionCameraOverheadLedger();
        var router = new SmartClassifierModelRouter();
        var classifier = new AiActionOverheadClassifier(router, ledger);
        var videoClient = new TestVideoClient();
        var visionClient = new TestVisionClient();
        var jitService = new JitBenchmarkService(ledger, classifier, videoClient, visionClient);

        var result = await jitService.EnsureBeatCalibratedAsync("Slashes with a machete", "(roaring)");

        Assert.True(result.IsLiveJitBenchmark);
        Assert.True(videoClient.GenerationSubmitted);
        Assert.Contains("Live Video API", result.SourceDescription);
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
        Assert.False(result.IsLiveJitBenchmark);
    }
}
