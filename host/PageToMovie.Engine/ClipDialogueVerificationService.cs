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
using PageToMovie.Core.Utils;
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
    private readonly GeminiChatClient? _gemini;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<ClipDialogueVerificationService> _log;

    public ClipDialogueVerificationService(
        ProjectStore projects,
        IVisionClient vision,
        ProjectTelemetryService telemetry,
        GeminiChatClient? gemini = null,
        ILogger<ClipDialogueVerificationService>? log = null)
    {
        _projects = projects;
        _vision = vision;
        _gemini = gemini;
        _telemetry = telemetry;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipDialogueVerificationService>.Instance;
    }

    public bool IsConfigured => _vision.IsConfigured || (_gemini?.IsConfigured ?? false);

    public string VerificationPath(string projectId, int scene, int clip) =>
        BuildVerificationPath(_projects.GetProjectDir(projectId), scene, clip);

    /// <summary>
    /// Static so callers that already have projectDir (e.g. ProjectStore, which this service
    /// itself depends on and so can't take an instance dependency on) can build the exact same
    /// path without duplicating the naming convention — a prior duplication of this path drifted
    /// out of sync and left dialogue verification results permanently unreadable.
    /// </summary>
    public static string BuildVerificationPath(string projectDir, int scene, int clip) =>
        Path.Combine(projectDir, "assets", "qa", $"scene_{scene:D2}_clip_{clip:D2}_dialogue_verification.json");

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

    public async Task<ClipDialogueVerificationResult?> LoadVerificationAsync(string projectId, int scene, int clip, CancellationToken ct = default)
    {
        var path = VerificationPath(projectId, scene, clip);
        return await StreamJsonStore.LoadAsync<ClipDialogueVerificationResult>(path, JsonOpts, ct).ConfigureAwait(false);
    }

    private static readonly byte[] NewLineBytes = new byte[] { (byte)'\n' };

    public async Task SaveVerificationAsync(string projectId, ClipDialogueVerificationResult result, CancellationToken ct = default)
    {
        var path = VerificationPath(projectId, result.SceneNumber, result.ClipNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Write to a temp file then atomically rename — a crash/cancellation mid-write must never
        // leave the verification file truncated. Readers (ProjectStore's mtime-validated cache)
        // rely on this atomicity to treat "file exists" as "file is a complete, valid write."
        var tmp = path + ".tmp";
        await using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, result, JsonOpts, ct).ConfigureAwait(false);
            await stream.WriteAsync(NewLineBytes, ct).ConfigureAwait(false);
        }
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Runs automated dialogue & speaker verification for a completed clip.
    /// </summary>
    public async Task<ClipDialogueVerificationResult> VerifyClipDialogueAsync(
        string projectId,
        int sceneNumber,
        int clipNumber,
        IReadOnlyList<string>? keyframePaths = null,
        string? overrideVideoPath = null,
        CancellationToken ct = default)
    {
        var clipPath = (!string.IsNullOrWhiteSpace(overrideVideoPath) && File.Exists(overrideVideoPath))
            ? overrideVideoPath
            : _projects.ResolveClipVideoPath(projectId, sceneNumber, clipNumber);
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
            await SaveVerificationAsync(projectId, noSpeechResult, ct).ConfigureAwait(false);
            return noSpeechResult;
        }

        if (!IsConfigured)
        {
            _log.LogWarning("Vision/Gemini client not configured — skipping dialogue verification for {Project} S{Scene} C{Clip}", projectId, sceneNumber, clipNumber);
            var unverified = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                Status = "unverified",
                SummaryNote = "Google Gemini key (GEMINI_API_KEY) required for native MP4 video & audio dialogue verification. Please set key in Configuration.",
                VerifiedAt = DateTime.UtcNow,
            };
            await SaveVerificationAsync(projectId, unverified, ct).ConfigureAwait(false);
            return unverified;
        }

        var mediaToPass = new List<string>();

        // Attach actual MP4 clip file so Gemini can evaluate video lip sync + audio track
        if (!string.IsNullOrWhiteSpace(clipPath) && File.Exists(clipPath))
        {
            mediaToPass.Add(clipPath);
        }

        // Collect character reference portraits for characters in this scene
        var charSummaryList = _projects.ListCharacters(projectId);
        var sceneChars = clip?.CharactersOnScreen is { Count: > 0 } ? clip.CharactersOnScreen : new List<string> { expectedSpeaker };

        foreach (var cName in sceneChars)
        {
            var charObj = charSummaryList.FirstOrDefault(c => string.Equals(c.Key, cName, StringComparison.OrdinalIgnoreCase) || string.Equals(c.DisplayName, cName, StringComparison.OrdinalIgnoreCase));
            if (charObj?.PreferredUrl is { Length: > 0 } url)
            {
                var localRef = Path.Combine(_projects.GetProjectDir(projectId), url.TrimStart('/'));
                if (File.Exists(localRef))
                    mediaToPass.Add(localRef);
            }
        }

        // Include passed keyframe images as supplementary input
        if (keyframePaths is { Count: > 0 })
        {
            mediaToPass.AddRange(keyframePaths.Where(File.Exists));
        }

        if (mediaToPass.Count == 0)
        {
            var result = new ClipDialogueVerificationResult
            {
                SceneNumber = sceneNumber,
                ClipNumber = clipNumber,
                ExpectedSpeaker = expectedSpeaker,
                ExpectedDialogue = expectedDialogue,
                Status = "unverified",
                SummaryNote = "Clip video file (.mp4) not found on server disk. Please generate video clips for this scene first.",
                VerifiedAt = DateTime.UtcNow,
            };
            await SaveVerificationAsync(projectId, result, ct).ConfigureAwait(false);
            return result;
        }

        var prompt = $@"
You are an automated film quality assurance inspector evaluating a generated movie clip.

EXPECTED SCRIPT:
- Expected Speaker: '{expectedSpeaker}'
- Expected Spoken Dialogue: '{expectedDialogue}'

TASKS:
1. Watch the attached MP4 video clip and LISTEN carefully to the audio track / spoken dialogue.
2. Observe on-screen character faces and mouth sync; compare against the attached character reference portraits to identify who is speaking.
3. Transcribe the EXACT spoken dialogue you hear in the video clip.
4. Compare detected speaker vs expected speaker, and transcribed dialogue vs expected dialogue.

Return ONLY a JSON object:
{{
  ""detectedSpeaker"": ""Character Name"",
  ""transcribedDialogue"": ""Spoken dialogue text heard in video audio track"",
  ""dialogueAccuracyScore"": 0.95,
  ""speakerMatch"": true,
  ""status"": ""verified"",
  ""summaryNote"": ""Expected: '{expectedDialogue}' | Heard: '...' (Match 95%)""
}}
Status options: 'verified' (dialogue & speaker match), 'mismatch' (dialogue incorrect), 'speaker_swap' (wrong character speaking), 'no_speech' (no spoken dialogue heard).
".Trim();

        try
        {
            var sw = Stopwatch.StartNew();
            var responseJson = (_gemini is not null && _gemini.IsConfigured)
                ? await _gemini.CompleteWithImagesAsync(prompt, mediaToPass, ct: ct).ConfigureAwait(false)
                : await _vision.CompleteWithImagesAsync(prompt, mediaToPass, ct: ct).ConfigureAwait(false);
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

            await SaveVerificationAsync(projectId, result, ct).ConfigureAwait(false);
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
            await SaveVerificationAsync(projectId, failedResult, ct).ConfigureAwait(false);
            return failedResult;
        }
    }

    /// <summary>
    /// Best-effort signal that a clip's dialogue was likely cut off because the clip ran out of time
    /// before the line finished, rather than misheard/substituted content or a speaker-identity
    /// mismatch. Feeds <c>ClipTimingTelemetryRepository</c>'s <c>DialogueTruncated</c> column so the
    /// timing ledger learns which categories/word-budgets actually cause truncation in practice,
    /// instead of that column staying permanently false.
    /// </summary>
    public static bool LooksTruncated(ClipDialogueVerificationResult result)
    {
        if (string.Equals(result.Status, "speaker_swap", StringComparison.OrdinalIgnoreCase))
            return false; // wrong speaker entirely — an identity problem, not a timing one

        var expectedWords = ClipDurationEstimator.CountWords(result.ExpectedDialogue);
        if (expectedWords == 0)
            return false; // nothing was supposed to be said

        var transcribedWords = ClipDurationEstimator.CountWords(result.TranscribedDialogue ?? "");
        // Meaningfully fewer words heard than expected suggests the line was cut off mid-delivery
        // rather than fully spoken with a few words misheard.
        return transcribedWords < expectedWords * 0.7;
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
