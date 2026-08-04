using Microsoft.JSInterop;
using PageToMovie.Core.Models;

namespace PageToMovie.Web.Services;

/// <summary>
/// Browser-side orchestration of the "substitute my cloned voice" overlay, the client half of the
/// movie-wide voice-substitution feature. The server job already synthesized each dialogue line in
/// the character's cloned voice and saved the per-clip <see cref="ProjectVoiceAlignment"/>. This
/// service, per clip:
///   1. detects the real speech windows with ffmpeg silence detection (free, local) — unless the
///      alignment already has detected timestamps from a prior run, in which case detection is
///      skipped;
///   2. persists any newly detected windows back to the server (server matches them to the known
///      lines) so subsequent runs are fast;
///   3. overlays the cloned-voice clips onto the ORIGINAL clip audio at those windows, ducking the
///      original only during speech so ambience/music/SFX survive.
///
/// All ffmpeg work runs in <c>PageToMovieFfmpeg</c> (ffmpeg.wasm); the API host never spawns ffmpeg.
/// </summary>
public sealed class ClientVoiceSubstitutionService
{
    private readonly IJSRuntime _js;
    private readonly EngineApiClient _engine;
    private readonly ClientMediaFolderService _media;
    private readonly ClientVideoStitchService _stitch;

    public ClientVoiceSubstitutionService(
        IJSRuntime js,
        EngineApiClient engine,
        ClientMediaFolderService media,
        ClientVideoStitchService stitch)
    {
        _js = js;
        _engine = engine;
        _media = media;
        _stitch = stitch;
    }

    /// <summary>Result of applying cloned-voice overlay to one clip.</summary>
    public sealed record ClipOverlayResult(int Scene, int Clip, bool Success, string? Url, string? Error);

    /// <summary>Outcome of the full "dub this movie in my voice" flow.</summary>
    public sealed record DubMovieResult(bool Ok, string? DownloadUrl, int ClipsDubbed, int ClipsFailed, string? Error);

    /// <summary>
    /// Full "make this movie in my cloned voice" flow, tying the server + client halves together:
    /// start the voice-substitution job (cloned-voice TTS per line + alignment), wait for it, sync the
    /// audio + clips locally, overlay the cloned voice onto each clip, stitch the dubbed clips into one
    /// movie, and hand back a downloadable blob URL. Narrator by default (server defaults the CharKey).
    /// Requires the media folder to be connected (clips + synthesized audio live there).
    /// </summary>
    public async Task<DubMovieResult> DubMovieInMyVoiceAsync(
        string projectId,
        string? charKey = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        onProgress?.Invoke("Generating your voice for each line…");
        var job = await _engine.StartVoiceSubstitutionAsync(
            new StartVoiceSubstitutionRequest { ProjectId = projectId, CharKey = charKey ?? "" }, ct);
        if (job is null)
            return new DubMovieResult(false, null, 0, 0, "Could not start the voice job.");

        var terminal = await _engine.WaitForJobTerminalAsync(job.JobId, TimeSpan.FromMinutes(15), ct);
        var status = terminal?.Status ?? "";
        var jobOk = string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
        if (!jobOk)
            return new DubMovieResult(false, null, 0, 0, terminal?.Error ?? terminal?.Message ?? "The voice job did not finish.");

        onProgress?.Invoke("Syncing clips and audio…");
        try { await _media.SyncProjectMediaToClientAsync(projectId); } catch { /* best effort — overlay reads whatever is local */ }

        onProgress?.Invoke("Placing your voice over each clip…");
        var overlays = await ApplyAcrossMovieAsync(projectId, ct);
        var ordered = overlays
            .Where(o => o.Success && !string.IsNullOrWhiteSpace(o.Url))
            .OrderBy(o => o.Scene).ThenBy(o => o.Clip)
            .Select(o => o.Url!)
            .ToList();
        var failed = overlays.Count(o => !o.Success);
        if (ordered.Count == 0)
            return new DubMovieResult(false, null, 0, failed,
                "No clips could be voiced — check that the movie's clips are available and a voice has been recorded.");

        onProgress?.Invoke("Stitching your movie…");
        var stitched = await _stitch.ConcatAsync(ordered, ct);
        if (!stitched.Success || string.IsNullOrWhiteSpace(stitched.Url))
            return new DubMovieResult(false, null, ordered.Count, failed, stitched.Error ?? "Could not stitch the dubbed movie.");

        return new DubMovieResult(true, stitched.Url, ordered.Count, failed, null);
    }

    /// <summary>Download a produced (blob) movie URL to the user's device.</summary>
    public async Task DownloadAsync(string url, string fileName)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        await _js.InvokeVoidAsync("PageToMovieMedia.downloadFromUrlAsync", url,
            string.IsNullOrWhiteSpace(fileName) ? "movie-in-my-voice.mp4" : fileName);
    }

    /// <summary>
    /// Apply cloned-voice overlay across every clip in the persisted alignment. Detects + persists
    /// timestamps as needed. Returns one result per clip (final blob URL on success). Never throws for
    /// a single-clip failure — that clip is reported as failed and the rest continue.
    /// </summary>
    public async Task<IReadOnlyList<ClipOverlayResult>> ApplyAcrossMovieAsync(
        string projectId, CancellationToken ct = default)
    {
        var results = new List<ClipOverlayResult>();
        var alignment = await _engine.GetVoiceAlignmentAsync(projectId, ct);
        if (alignment is null || alignment.Clips.Count == 0)
            return results;

        var pendingTimestampUpdates = new List<ClipTimestampUpdate>();

        foreach (var clip in alignment.Clips)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var clipUrl = await _stitch.ResolveClipUrlAsync(projectId, clip.Scene, clip.Clip, ct);
                if (string.IsNullOrWhiteSpace(clipUrl))
                {
                    results.Add(new ClipOverlayResult(clip.Scene, clip.Clip, false, null, "clip video not found"));
                    continue;
                }

                // 1. Detect speech windows unless we already have detected timestamps for this clip.
                if (!clip.IsDetected)
                {
                    var detect = await _js.InvokeAsync<JsSpeechDetectResult>(
                        "PageToMovieFfmpeg.detectSpeechSegmentsAsync", ct, clipUrl, new { });
                    if (detect is { Success: true } && detect.Segments is not null)
                    {
                        var update = new ClipTimestampUpdate
                        {
                            Scene = clip.Scene,
                            Clip = clip.Clip,
                            ClipDurationSeconds = detect.TotalSec,
                            Windows = detect.Segments
                                .Select(s => new SpeechWindow { StartSec = s.StartSec, EndSec = s.EndSec })
                                .ToList(),
                        };
                        pendingTimestampUpdates.Add(update);

                        // Apply to the in-memory copy too so the overlay below uses real windows.
                        ApplyWindowsLocally(clip, detect);
                    }
                }

                // 2. Build overlay segments from each segment's cloned-voice audio + its window.
                var overlaySegments = new List<object>();
                foreach (var seg in clip.Segments.OrderBy(s => s.Index))
                {
                    if (string.IsNullOrWhiteSpace(seg.VoiceAudioRelativePath)) continue;
                    var audioUrl = await _media.GetLocalBlobUrlAsync(projectId, seg.VoiceAudioRelativePath);
                    if (string.IsNullOrWhiteSpace(audioUrl)) continue;
                    overlaySegments.Add(new
                    {
                        audioUrl,
                        startSec = seg.StartSec,
                        endSec = seg.EndSec,
                    });
                }

                if (overlaySegments.Count == 0)
                {
                    results.Add(new ClipOverlayResult(clip.Scene, clip.Clip, false, null, "no cloned-voice audio synced"));
                    continue;
                }

                var overlay = await _js.InvokeAsync<JsOverlayResult>(
                    "PageToMovieFfmpeg.overlayVoiceSegmentsAsync",
                    ct, clipUrl, overlaySegments.ToArray(), new { });

                if (overlay is { Success: true } && !string.IsNullOrWhiteSpace(overlay.Url))
                    results.Add(new ClipOverlayResult(clip.Scene, clip.Clip, true, overlay.Url, null));
                else
                    results.Add(new ClipOverlayResult(clip.Scene, clip.Clip, false, null, overlay?.Error ?? "overlay failed"));
            }
            catch (Exception ex)
            {
                results.Add(new ClipOverlayResult(clip.Scene, clip.Clip, false, null, ex.Message));
            }
        }

        // 3. Persist any newly detected timestamps so a re-run skips detection (best-effort).
        if (pendingTimestampUpdates.Count > 0)
        {
            try { await _engine.PostVoiceAlignmentTimestampsAsync(projectId, pendingTimestampUpdates, ct); }
            catch { /* non-fatal: overlay already produced; persistence can retry next run */ }
        }

        return results;
    }

    /// <summary>
    /// Assign detected windows to the clip's segments by order/count (a light client-side mirror of
    /// the server's authoritative match, used only so the immediate overlay uses real windows; the
    /// server re-matches and persists the canonical result).
    /// </summary>
    private static void ApplyWindowsLocally(ClipSpeechAlignment clip, JsSpeechDetectResult detect)
    {
        var windows = (detect.Segments ?? new List<JsSpeechWindow>())
            .Where(w => w.EndSec - w.StartSec >= 0.15)
            .OrderBy(w => w.StartSec)
            .ToList();
        if (windows.Count == 0 || clip.Segments.Count == 0) return;

        if (windows.Count == clip.Segments.Count)
        {
            for (var i = 0; i < clip.Segments.Count; i++)
            {
                clip.Segments[i].StartSec = windows[i].StartSec;
                clip.Segments[i].EndSec = windows[i].EndSec;
                clip.Segments[i].Source = SpeechTimestampSource.Silence;
            }
            return;
        }

        // Counts differ: spread the overall detected span across segments by text length.
        var spanStart = windows[0].StartSec;
        var spanEnd = Math.Max(windows[^1].EndSec, spanStart + 0.01);
        var weights = clip.Segments.Select(s => (double)Math.Max(1, (s.DialogueText ?? "").Trim().Length)).ToList();
        var total = weights.Sum();
        var span = spanEnd - spanStart;
        var cursor = spanStart;
        for (var i = 0; i < clip.Segments.Count; i++)
        {
            var end = i == clip.Segments.Count - 1 ? spanEnd : cursor + span * (weights[i] / total);
            clip.Segments[i].StartSec = Math.Round(cursor, 3);
            clip.Segments[i].EndSec = Math.Round(end, 3);
            clip.Segments[i].Source = SpeechTimestampSource.Silence;
            cursor = end;
        }
    }

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

    private sealed class JsOverlayResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }
}
