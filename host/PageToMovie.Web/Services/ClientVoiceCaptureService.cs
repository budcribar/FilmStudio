using Microsoft.JSInterop;
using PageToMovie.Core.Models;

namespace PageToMovie.Web.Services;

/// <summary>
/// Builds the once-per-book voice-capture phrase cache: for each narrator-only scene, stitch the
/// clips, detect speech windows, and for each window extract its audio and run it through Scribe
/// (STT) to VERIFY it contains the expected blueprint line. Confident (verified) windows become both
/// capture material and a trusted line↔window mapping for the dub overlay. Result persists to the
/// project as <c>assets/voice_capture/phrases.json</c>.
///
/// This is deliberately the slow, one-time path (one STT call per window). The capture UI and the
/// dub overlay read the cached result; they never re-run this.
/// </summary>
public sealed class ClientVoiceCaptureService
{
    private readonly IJSRuntime _js;
    private readonly EngineApiClient _engine;
    private readonly ClientVideoStitchService _stitch;

    public ClientVoiceCaptureService(IJSRuntime js, EngineApiClient engine, ClientVideoStitchService stitch)
    {
        _js = js;
        _engine = engine;
        _stitch = stitch;
    }

    /// <summary>Word-overlap (transcript vs expected line) at/above which a window is "confident".</summary>
    public const double ConfidenceThreshold = 0.7;

    /// <summary>
    /// Run the verification pass and save the phrase cache. Returns the built set (also persisted).
    /// Narrator-only scenes only (mixed scenes keep original audio and aren't capture material).
    /// </summary>
    public async Task<VoiceCapturePhrases?> BuildPhrasesAsync(
        string projectId, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        // Expected narrator line texts + which scenes are narrator-only, straight from the blueprint —
        // no dub/TTS needed, so this runs standalone from the capture page.
        var scenes = await _engine.GetNarratorLinesAsync(projectId, ct);
        if (scenes is null || scenes.Count == 0)
            return null;

        var phrases = new VoiceCapturePhrases { ProjectId = projectId, ConfidenceThreshold = ConfidenceThreshold };

        foreach (var sc in scenes.Where(s => !s.HasOtherSpeakers).OrderBy(s => s.Scene))
        {
            ct.ThrowIfCancellationRequested();
            var expectedLines = sc.Lines
                .Select(t => (t ?? "").Trim())
                .Where(t => t.Length > 0)
                .ToList();
            if (expectedLines.Count == 0) continue;

            onProgress?.Invoke($"Scanning scene {sc.Scene:D2}…");

            // Stitch the scene's clips into one video.
            var clipUrls = await _stitch.CollectClipUrlsAsync(projectId, sc.Scene, ct: ct);
            if (clipUrls.Count == 0) continue;
            string sceneVideoUrl;
            if (clipUrls.Count == 1)
            {
                sceneVideoUrl = clipUrls[0];
            }
            else
            {
                var stitched = await _stitch.ConcatAsync(clipUrls, ct);
                if (!stitched.Success || string.IsNullOrWhiteSpace(stitched.Url)) continue;
                sceneVideoUrl = stitched.Url!;
            }

            // Detect speech windows.
            var detect = await _js.InvokeAsync<JsSpeechDetectResult>(
                "PageToMovieFfmpeg.detectSpeechSegmentsAsync", ct, sceneVideoUrl, new { });
            var windows = (detect?.Segments ?? new List<JsSpeechWindow>())
                .Where(w => w.EndSec - w.StartSec >= 0.4)
                .OrderBy(w => w.StartSec)
                .ToList();

            // Extract + transcribe + match each window.
            for (var wi = 0; wi < windows.Count; wi++)
            {
                ct.ThrowIfCancellationRequested();
                var w = windows[wi];
                onProgress?.Invoke($"Scene {sc.Scene:D2}: verifying phrase {wi + 1}/{windows.Count}…");

                byte[]? audio;
                try
                {
                    audio = await _js.InvokeAsync<byte[]>(
                        "PageToMovieFfmpeg.extractAudioSegmentAsync", ct, sceneVideoUrl, w.StartSec, w.EndSec);
                }
                catch { continue; }
                if (audio is null || audio.Length < 256) continue;

                var transcript = await _engine.TranscribeSegmentAsync(audio, "segment.wav", ct);
                var heard = (transcript?.Text ?? "").Trim();
                if (heard.Length == 0) continue;

                // Keep the per-word timings (they're 0-based within this extracted window) so the
                // capture teleprompter can copy the narrator's exact rhythm, not an even glide.
                var timedWords = (transcript?.Words ?? new())
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text) &&
                                !string.Equals(w.Type, "spacing", StringComparison.OrdinalIgnoreCase))
                    .Select(w => new VoiceCaptureWord { Text = w.Text.Trim(), StartSec = Math.Max(0, w.Start), EndSec = Math.Max(0, w.End) })
                    .ToList();

                // Best-matching expected narrator line for this window.
                var bestLine = "";
                var bestScore = 0.0;
                foreach (var line in expectedLines)
                {
                    var s = WordOverlap(line, heard);
                    if (s > bestScore) { bestScore = s; bestLine = line; }
                }

                phrases.Phrases.Add(new VoiceCapturePhrase
                {
                    Scene = sc.Scene,
                    Clip = 0,
                    WindowStartSec = w.StartSec,
                    WindowEndSec = w.EndSec,
                    Text = bestLine,
                    TranscribedText = heard,
                    MatchScore = Math.Round(bestScore, 3),
                    Confident = bestScore >= ConfidenceThreshold,
                    Words = timedWords.Count > 0 ? timedWords : null,
                });
            }
        }

        // Rank confident phrases (longer = more capture material) and mark the selected pool.
        var confident = phrases.Phrases.Where(p => p.Confident).OrderByDescending(p => p.DurationSec).ToList();
        for (var i = 0; i < confident.Count; i++) confident[i].Rank = i;

        onProgress?.Invoke($"Verified {confident.Count} of {phrases.Phrases.Count} detected phrase(s).");
        await _engine.SaveVoiceCapturePhrasesAsync(projectId, phrases, ct);
        return phrases;
    }

    /// <summary>Fraction of the expected line's words that appear in the transcript (0..1).</summary>
    private static double WordOverlap(string expected, string heard)
    {
        var e = Tokenize(expected);
        if (e.Count == 0) return 0;
        var h = new HashSet<string>(Tokenize(heard));
        var hit = e.Count(w => h.Contains(w));
        return (double)hit / e.Count;
    }

    private static List<string> Tokenize(string s) =>
        new string((s ?? "").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

    private sealed class JsSpeechDetectResult
    {
        public bool Success { get; set; }
        public double TotalSec { get; set; }
        public List<JsSpeechWindow>? Segments { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsSpeechWindow
    {
        public double StartSec { get; set; }
        public double EndSec { get; set; }
    }
}
