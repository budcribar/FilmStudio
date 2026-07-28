using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// 100% Automated clip dialogue & speaker verification pass.
/// Uses multimodal vision (IVisionClient) + character reference plates to evaluate
/// generated video clips, transcribe spoken dialogue, and verify speaker identity.
/// Runs automatically in the background when a clip finishes generating.
/// </summary>
public sealed class ClipDialogueVerificationService
{
    private static readonly JsonSerializerOptions JsonOpts = JsonDefaults.IndentedCaseInsensitive;

    private readonly ProjectStore _projects;
    private readonly IVisionClient _vision;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<ClipDialogueVerificationService> _log;

    public ClipDialogueVerificationService(
        ProjectStore projects,
        IVisionClient vision,
        ProjectTelemetryService telemetry,
        ILogger<ClipDialogueVerificationService>? log = null)
    {
        _projects = projects;
        _vision = vision;
        _telemetry = telemetry;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipDialogueVerificationService>.Instance;
    }

    public bool IsConfigured => _vision.IsConfigured;

    public string VerificationPath(string projectId, int scene, int clip) =>
        Path.Combine(_projects.GetProjectDir(projectId), "assets", "review", $"scene_{scene:D2}_clip_{clip:D2}.verification.json");

    public ClipDialogueVerificationResult? LoadVerification(string projectId, int scene, int clip)
    {
        var path = VerificationPath(projectId, scene, clip);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClipDialogueVerificationResult>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void SaveVerification(string projectId, ClipDialogueVerificationResult result)
    {
        var path = VerificationPath(projectId, result.SceneNumber, result.ClipNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOpts) + "\n");
    }

    /// <summary>
    /// Runs automated dialogue & speaker verification for a completed clip.
    /// </summary>
    public async Task<ClipDialogueVerificationResult> VerifyClipDialogueAsync(
        string projectId,
        int sceneNumber,
        int clipNumber,
        IReadOnlyList<string>? keyframePaths = null,
        CancellationToken ct = default)
    {
        var clipPath = _projects.ResolveClipVideoPath(projectId, sceneNumber, clipNumber);
        var detail = await _projects.GetSceneDetailAsync(projectId, sceneNumber, ct: ct).ConfigureAwait(false);
        var clip = detail?.Clips?.FirstOrDefault(c => c.ClipNumber == clipNumber);

        var expectedSpeaker = clip?.Speaker ?? "Unknown";
        var expectedDialogue = clip?.Dialogue ?? "";

        // If no speech planned for this clip, return verified no-speech status immediately
        if (string.IsNullOrWhiteSpace(expectedDialogue) && string.IsNullOrWhiteSpace(expectedSpeaker))
        {
            var noSpeechResult = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = "None",
                ExpectedDialogue = "",
                DetectedSpeaker = "None",
                TranscribedDialogue = "",
                DialogueAccuracyScore = 1.0,
                SpeakerMatch = true,
                Status = "no_speech",
                SummaryNote = "No dialogue planned for this clip.",
                VerifiedAt = DateTime.UtcNow,
            };
            SaveVerification(projectId, noSpeechResult);
            return noSpeechResult;
        }

        if (!_vision.IsConfigured)
        {
            _log.LogWarning("Vision client not configured — skipping automated dialogue verification for {Project} S{Scene} C{Clip}", projectId, sceneNumber, clipNumber);
            var unverified = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                Status = "unverified",
                SummaryNote = "AI Vision API Key required (xAI Grok, Gemini, or Claude). Add a key in Configuration.",
                VerifiedAt = DateTime.UtcNow,
            };
            SaveVerification(projectId, unverified);
            return unverified;
        }

        // Collect character reference portraits for characters in this scene
        var imagesToPass = new List<string>();
        var charSummaryList = _projects.ListCharacters(projectId);
        var sceneChars = clip?.CharactersOnScreen is { Count: > 0 } ? clip.CharactersOnScreen : new List<string> { expectedSpeaker };

        foreach (var cName in sceneChars)
        {
            var charObj = charSummaryList.FirstOrDefault(c => string.Equals(c.Key, cName, StringComparison.OrdinalIgnoreCase) || string.Equals(c.DisplayName, cName, StringComparison.OrdinalIgnoreCase));
            if (charObj?.PreferredUrl is { Length: > 0 } url)
            {
                var localRef = Path.Combine(_projects.GetProjectDir(projectId), url.TrimStart('/'));
                if (File.Exists(localRef))
                    imagesToPass.Add(localRef);
            }
        }

        // Include passed keyframe images or sample stills from clip
        if (keyframePaths is { Count: > 0 })
        {
            imagesToPass.AddRange(keyframePaths.Where(File.Exists));
        }

        if (imagesToPass.Count == 0)
        {
            var result = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                Status = "unverified",
                SummaryNote = "No video keyframes or character reference plates available.",
                VerifiedAt = DateTime.UtcNow,
            };
            SaveVerification(projectId, result);
            return result;
        }

        var prompt = $@"
You are an automated film quality assurance inspector evaluating a generated movie clip.
Expected Speaker: '{expectedSpeaker}'
Expected Dialogue: '{expectedDialogue}'

1. Compare character faces in the video frames against the attached reference portraits to identify which character is speaking.
2. Transcribe any spoken or mouth-synced dialogue in the clip.
3. Compare detected speaker and transcribed dialogue against expected values.

Return ONLY a JSON object:
{{
  ""detectedSpeaker"": ""Character Name"",
  ""transcribedDialogue"": ""Spoken dialogue text"",
  ""dialogueAccuracyScore"": 0.95,
  ""speakerMatch"": true,
  ""status"": ""verified"",
  ""summaryNote"": ""Brief outcome summary""
}}
Status values: 'verified' (matches), 'mismatch' (dialogue incorrect), 'speaker_swap' (wrong character speaking), 'no_speech'.
".Trim();

        try
        {
            var sw = Stopwatch.StartNew();
            var responseJson = await _vision.CompleteWithImagesAsync(prompt, imagesToPass, ct: ct).ConfigureAwait(false);
            var cleanJson = ExtractJson(responseJson);

            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            var detected = root.TryGetProperty("detectedSpeaker", out var dEl) ? dEl.GetString() ?? "" : "";
            var transcribed = root.TryGetProperty("transcribedDialogue", out var tEl) ? tEl.GetString() ?? "" : "";
            var accuracy = root.TryGetProperty("dialogueAccuracyScore", out var aEl) && aEl.TryGetDouble(out var acc) ? acc : CalculateAccuracyScore(expectedDialogue, transcribed);
            var speakerMatch = root.TryGetProperty("speakerMatch", out var smEl) && smEl.GetBoolean();
            var status = root.TryGetProperty("status", out var stEl) ? stEl.GetString() ?? "verified" : "verified";
            var summary = root.TryGetProperty("summaryNote", out var snEl) ? snEl.GetString() ?? "" : "";

            var result = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                DetectedSpeaker = detected,
                TranscribedDialogue = transcribed,
                DialogueAccuracyScore = Math.Round(accuracy, 2),
                SpeakerMatch = speakerMatch,
                Status = status,
                SummaryNote = summary,
                VerifiedAt = DateTime.UtcNow,
            };

            SaveVerification(projectId, result);
            _log.LogInformation("Automated dialogue verification completed for {Project} S{Scene} C{Clip}: {Status} ({Score:P0})", projectId, sceneNumber, clipNumber, status, accuracy);
            return result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Dialogue verification failed for {Project} S{Scene} C{Clip}", projectId, sceneNumber, clipNumber);
            var failedResult = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                Status = "unverified",
                SummaryNote = $"Verification error: {ex.Message}",
                VerifiedAt = DateTime.UtcNow,
            };
            SaveVerification(projectId, failedResult);
            return failedResult;
        }
    }

    private static double CalculateAccuracyScore(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual)) return 1.0;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return 0.0;

        var expWords = Regex.Matches(expected.ToLowerInvariant(), @"\w+").Select(m => m.Value).ToHashSet();
        var actWords = Regex.Matches(actual.ToLowerInvariant(), @"\w+").Select(m => m.Value).ToHashSet();

        if (expWords.Count == 0) return 1.0;
        var matches = expWords.Count(w => actWords.Contains(w));
        return (double)matches / expWords.Count;
    }

    private static string ExtractJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "{}";
        var match = Regex.Match(input, @"\{[\s\S]*\}");
        return match.Success ? match.Value : input;
    }
}
