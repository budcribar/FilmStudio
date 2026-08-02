using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Options;
using PageToMovie.Core.Utils;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

public sealed record CameraDirective(
    string ShotScale,
    string LensSpec,
    string CameraMovement,
    string FramingPrompt);

/// <summary>
/// AI Classifier acting as a Virtuoso Film Director / Director of Photography.
/// Assigns cinematic lens choices, camera movements (push-in, tracking, dolly),
/// and shot framing per beat ID based on narrative emotion.
/// </summary>
public sealed class CameraDirectorClassifier
{
    public const string PromptVersion = "v1_product";

    private readonly IChatClient _chat;
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<CameraDirectorClassifier> _log;
    private readonly GenerationErrorLogger? _errorLogger;

    public CameraDirectorClassifier(
        IChatClient chat,
        IOptions<PageToMovieOptions> opts,
        ILogger<CameraDirectorClassifier> log,
        GenerationErrorLogger? errorLogger = null)
    {
        _chat = chat;
        _opts = opts.Value;
        _log = log;
        _errorLogger = errorLogger;
    }

    public bool IsEnabled => _opts.ClassifyCameraDirectorWithChat && _chat.IsConfigured;

    public static string SystemPrompt() => """
        You are a Virtuoso Film Director and Director of Photography (DP) directing camera composition and movement.

        Your task: Given a list of scene beats, assign cinematic camera directives per beat ID based on film grammar and narrative tension.

        DIRECTIVES TO ASSIGN PER BEAT:
        1. shot_scale: "wide", "medium", "close_up", or "extreme_close_up".
        2. lens_spec: Choice of lens (e.g. "24mm wide anamorphic lens", "35mm prime lens", "85mm f/1.4 portrait lens", "100mm macro lens").
        3. camera_movement: Specific cinematic movement (e.g. "slow 10% dolly push-in", "locked tripod hold", "low-angle slow tracking shot", "steady handheld tilt").
        4. framing_prompt: A 10–25 word description of the camera shot composition (e.g. "Low-angle medium shot, 35mm lens, camera slowly pushes in as character speaks").

        TWO-SPEAKER BEATS: some beats show a "Then spoken (...)" second line — the clip holds one
        continuous take covering both speakers, not a cut between them. For these, camera_movement
        must describe a pan/reframe move from the first speaker to the second, timed to land on the
        second speaker as they begin their line (e.g. "pan left from Character_A to Character_B,
        settling as B begins speaking"), and framing_prompt must describe a composition that reads
        naturally as it starts on speaker one and ends on speaker two.

        OUTPUT FORMAT:
        Return ONLY valid JSON matching this schema:
        {
          "directives": [
            {
              "beat_id": "b1",
              "shot_scale": "wide",
              "lens_spec": "24mm wide anamorphic lens",
              "camera_movement": "locked tripod establishing shot",
              "framing_prompt": "Establishing wide shot, 24mm anamorphic lens, static locked camera framing subject centrally."
            },
            ...
          ]
        }
        """;

    public async Task<Dictionary<string, CameraDirective>?> ClassifySceneCameraAsync(
        Dictionary<string, object?> scene,
        List<Dictionary<string, object?>> beats,
        Action<string>? onProgress = null,
        CancellationToken ct = default,
        string? model = null)
    {
        if (!IsEnabled || beats.Count == 0) return null;

        onProgress?.Invoke($"AI Camera Director: Directing camera lenses & movement for {beats.Count} beats…");

        try
        {
            var userPrompt = BuildUserPrompt(scene, beats);
            var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model : _opts.CameraDirectorClassifyModel;
            var requestedIds = beats
                .Select(b => b.GetValueOrDefault("beat_id")?.ToString() ?? "")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            var retry = await AiRetryPolicy.RunWithCoverageRetryAsync(
                requestedIds,
                () => _chat.CompleteAsync(
                    SystemPrompt(),
                    userPrompt,
                    effectiveModel,
                    // 0, not 0.2 — see BeatPacingClassifier for why (cacheable categorical labeling).
                    temperature: 0,
                    ct: ct,
                    mode: ChatCallModes.CameraDirectorClassify),
                ParseCameraResponse,
                maxAttempts: AiRetryPolicy.DefaultCoverageMaxAttempts,
                backoffBaseMs: AiRetryPolicy.DefaultCoverageBackoffMs,
                ct: ct).ConfigureAwait(false);

            if (_errorLogger is not null)
            {
                var sceneNum = ToIntOrNull(scene.GetValueOrDefault("scene_number"));
                await _errorLogger.LogCoverageResultAsync(
                    "camera_director_classifier", effectiveModel, ResolveProvider(effectiveModel), sceneNum,
                    requestedIds, retry, ct).ConfigureAwait(false);
            }

            return retry.Result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to run AI camera director classification for scene {Scene}", scene.GetValueOrDefault("scene_number"));
            return null;
        }
    }

    private static int? ToIntOrNull(object? val) => val switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var p) => p,
        _ => null,
    };

    private static string? ResolveProvider(string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : PageToMovie.Core.Models.SupportedModelCatalog.Find(model)?.ProviderId;

    private static string BuildUserPrompt(Dictionary<string, object?> scene, List<Dictionary<string, object?>> beats)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"SCENE {scene.GetValueOrDefault("scene_number")}: {scene.GetValueOrDefault("setting")}");
        sb.AppendLine();
        sb.AppendLine("BEATS TO DIRECT:");

        foreach (var b in beats)
        {
            var id = b.GetValueOrDefault("beat_id") ?? "b";
            var action = b.GetValueOrDefault("visual_event") ?? "";
            var spk = b.GetValueOrDefault("speaker") ?? "";
            var dlg = b.GetValueOrDefault("dialogue") ?? "";
            var ac = b.GetValueOrDefault("action_class") ?? "";
            var spk2 = b.GetValueOrDefault("secondary_speaker") ?? "";
            var dlg2 = b.GetValueOrDefault("secondary_dialogue") ?? "";

            sb.AppendLine($"Beat '{id}' (class: {ac}):");
            if (!string.IsNullOrWhiteSpace(spk?.ToString()) || !string.IsNullOrWhiteSpace(dlg?.ToString()))
                sb.AppendLine($"  Spoken ({spk}): \"{dlg}\"");
            if (!string.IsNullOrWhiteSpace(spk2?.ToString()) || !string.IsNullOrWhiteSpace(dlg2?.ToString()))
                sb.AppendLine($"  Then spoken ({spk2}): \"{dlg2}\"");
            if (!string.IsNullOrWhiteSpace(action?.ToString()))
                sb.AppendLine($"  Action prose: {action}");
        }

        return sb.ToString();
    }

    private Dictionary<string, CameraDirective>? ParseCameraResponse(string rawJson)
    {
        try
        {
            var cleaned = Regex.Replace(rawJson, @"```json|```", "").Trim();
            using var doc = JsonDocument.Parse(cleaned);
            if (!doc.RootElement.TryGetProperty("directives", out var dirArray) ||
                dirArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new Dictionary<string, CameraDirective>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dirArray.EnumerateArray())
            {
                var id = item.GetStringProp("beat_id");
                var scale = item.GetStringProp("shot_scale", "medium");
                var lens = item.GetStringProp("lens_spec", "35mm lens");
                var move = item.GetStringProp("camera_movement", "locked tripod");
                var framing = item.GetStringProp("framing_prompt");

                if (!string.IsNullOrWhiteSpace(id))
                {
                    result[id] = new CameraDirective(scale, lens, move, framing);
                }
            }

            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse AI camera director response JSON: {RawJson}", rawJson);
            return null;
        }
    }
}
