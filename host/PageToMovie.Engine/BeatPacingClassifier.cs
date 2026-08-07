using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine.ModelBacked;

/// <summary>
/// AI Classifier that dynamically calculates dramatic clip durations (2s–12s)
/// for scene beats based on narrative tension, emotional weight, and pacing rhythm.
/// Replaces static word/character count tables with cinematic rhythm analysis.
/// </summary>
public sealed class BeatPacingClassifier : BeatChatClassifierBase<int>
{
    public const string PromptVersion = "v1_product";

    public BeatPacingClassifier(
        IChatClient chat,
        IOptions<PageToMovieOptions> opts,
        ILogger<BeatPacingClassifier> log,
        GenerationErrorLogger? errorLogger = null)
        : base(chat, opts.Value, log, errorLogger)
    {
    }

    protected override bool OptionEnabled => _opts.ClassifyBeatPacingWithChat;
    protected override string DefaultModel => _opts.BeatPacingClassifyModel;
    protected override string? ChatMode => ChatCallModes.BeatPacingClassify;
    protected override string OperationName => "stage2_beat_pacing";
    protected override string ErrorLoggerName => "beat_pacing_classifier";
    protected override string LogNoun => "beat pacing";
    protected override string GetSystemPrompt() => SystemPrompt();
    protected override string ProgressMessage(int beatCount) =>
        $"AI Beat Pacing: Analyzing dramatic rhythm for {beatCount} beats…";

    public static string SystemPrompt() => """
        You are an expert film editor and director determining duration pacing for screenplay beats.

        Your task: Given a list of scene beats (dialogue, spoken lines, visual action descriptions), analyze the dramatic tension and emotional pacing to assign an optimal duration in seconds (between 2 and 12 seconds) for each beat.

        RULES (HARD):
        1. Range: Every duration MUST be an integer between 2 and 12 seconds.
        2. Pacing Guidelines:
           - Suspense / terror / tense waiting / silent observation: Assign longer duration (7s–12s) to allow visual tension to build.
           - Climax / sudden violent action / panic: Assign medium-short duration (4s–6s) for impact.
           - Rapid dialogue / brief interjection / fast movement: Assign short duration (2s–4s).
           - Monologue / steady dialogue: Base duration on spoken length (~2.5 words per second, min 3s, max 10s).
        3. Do NOT omit any beat IDs provided in the prompt.

        OUTPUT FORMAT:
        Return ONLY valid JSON matching this schema:
        {
          "pacing": [
            {
              "beat_id": "b1",
              "duration_seconds": 6,
              "reason": "tense observation"
            },
            ...
          ]
        }
        """;

    public Task<Dictionary<string, int>?> ClassifyScenePacingAsync(
        Dictionary<string, object?> scene,
        List<Dictionary<string, object?>> beats,
        Action<string>? onProgress = null,
        CancellationToken ct = default,
        string? model = null) => ClassifyAsync(scene, beats, onProgress, ct, model);

    protected override string BeatsHeading => "BEATS TO PACE:";

    protected override Dictionary<string, int>? ParseResponse(string rawJson)
    {
        try
        {
            var cleaned = ClassifierJsonParser.StripFences(rawJson);
            using var doc = JsonDocument.Parse(cleaned);
            if (!doc.RootElement.TryGetProperty("pacing", out var paceArray) ||
                paceArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in paceArray.EnumerateArray())
            {
                if (item.TryGetProperty("beat_id", out var bid) &&
                    item.TryGetProperty("duration_seconds", out var dur))
                {
                    var id = bid.GetString() ?? "";
                    var d = Math.Clamp(dur.GetInt32(), 2, 12);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        result[id] = d;
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse AI beat pacing response JSON: {RawJson}", rawJson);
            return null;
        }
    }
}
