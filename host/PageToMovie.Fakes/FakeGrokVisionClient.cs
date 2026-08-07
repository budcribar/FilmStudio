using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeGrokVisionClient : IVisionClient
{
    private readonly ILogger<FakeGrokVisionClient> _log;

    public FakeGrokVisionClient(ILogger<FakeGrokVisionClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string> TranscribePageAsync(
        string imagePath,
        int page,
        string model = "grok-4.5",
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake vision transcribe page={Page}", page);
        return Task.FromResult("(illustration only)");
    }

    public Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(
        string imagePath,
        int page,
        IReadOnlyList<CharacterClassifyHint> cast,
        string model = "grok-4.5",
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake vision classify page={Page} cast={N}", page, cast.Count);
        return Task.FromResult(new CharacterPageClassification
        {
            Page = page,
            PageKind = "illustration",
            Matches = new List<CharacterPageMatch>(),
        });
    }

    public Task<string> CompleteWithImagesAsync(
        string prompt,
        IReadOnlyList<string> imagePaths,
        string model = "grok-4.5",
        string detail = "low",
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake vision multi-image n={N}", imagePaths?.Count ?? 0);
        // Portrait style gate (CharacterDesignService.EnsurePortraitStyleAllowedAsync) — always pass
        // for fakes so UI tests can lock a portrait. Match markers that are ACTUALLY in the gate
        // prompt ("Expected medium for this project" / "Classify the image medium"); the old
        // "PORTRAIT STYLE GATE" string never appeared, so the gate fell through to auto-review JSON,
        // read medium=other, and every portrait lock failed in fakes mode ("could not read the image").
        if (!string.IsNullOrEmpty(prompt) &&
            (prompt.Contains("Classify the image medium", StringComparison.OrdinalIgnoreCase) ||
             prompt.Contains("Expected medium for this project", StringComparison.OrdinalIgnoreCase)))
        {
            var expectedIllustration = prompt.Contains("Expected medium for this project: illustration", StringComparison.OrdinalIgnoreCase);
            // Test hook: force a style-mismatch verdict so the "Use this look anyway" override path is
            // reachable in fakes mode (the gate otherwise always passes). Reports the OPPOSITE medium.
            var forceReject = string.Equals(Environment.GetEnvironmentVariable("PAGETOMOVIE_FAKE_STYLE_REJECT"), "1", StringComparison.Ordinal)
                || string.Equals(Environment.GetEnvironmentVariable("PAGETOMOVIE_FAKE_STYLE_REJECT"), "true", StringComparison.OrdinalIgnoreCase);
            if (forceReject)
            {
                var wrong = expectedIllustration ? "photoreal" : "illustration";
                return Task.FromResult(
                    $"{{\"pass\":false,\"medium\":\"{wrong}\",\"reason\":\"Fake forced style-mismatch for override testing.\"}}");
            }
            var medium = expectedIllustration
                ? "illustration"
                : "photoreal";
            return Task.FromResult(
                $"{{\"pass\":true,\"medium\":\"{medium}\",\"reason\":\"Fake style gate pass.\"}}");
        }

        if (!string.IsNullOrEmpty(prompt) &&
            prompt.Contains("music supervisor", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("""
                {
                  "1": { "prompt": "Dark orchestral theme with low cello and tense pulse.", "genre": "Thriller", "mood": "Tense", "tempo": "90 BPM" },
                  "2": { "prompt": "Subtle atmospheric ambient drone with eerie strings.", "genre": "Ambient", "mood": "Unsettling", "tempo": "75 BPM" }
                }
                """);
        }

        // Minimal valid auto-review JSON for UI/job testing without spend
        return Task.FromResult("""
            {
              "suggestion": "unclear",
              "category": "other",
              "confidence": "low",
              "continuity": "unclear",
              "note": "Fake review — connect API for real analysis.",
              "suggestions": []
            }
            """);
    }
}
