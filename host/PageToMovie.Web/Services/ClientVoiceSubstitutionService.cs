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

    /// <summary>Result of stitching one scene and overlaying its single cloned-voice narration track.</summary>
    public sealed record SceneOverlayResult(int Scene, bool Success, string? Url, string? Error);

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

        onProgress?.Invoke("Placing your voice over each scene…");
        var overlays = await ApplyAcrossMovieAsync(projectId, ct);
        var ordered = overlays
            .Where(o => o.Success && !string.IsNullOrWhiteSpace(o.Url))
            .OrderBy(o => o.Scene)
            .Select(o => o.Url!)
            .ToList();
        var failed = overlays.Count(o => !o.Success);
        if (ordered.Count == 0)
            return new DubMovieResult(false, null, 0, failed,
                "No scenes could be voiced — check that the movie's clips are available and a voice has been recorded.");

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
    /// Overlay the cloned voice across the movie, one continuous narration track per SCENE: stitch the
    /// scene's clips into a scene video, overlay the single scene voice track, and return one result
    /// per scene (final blob URL on success). Never throws for a single-scene failure — that scene is
    /// reported failed and the rest continue.
    /// </summary>
    public async Task<IReadOnlyList<SceneOverlayResult>> ApplyAcrossMovieAsync(
        string projectId, CancellationToken ct = default)
    {
        var results = new List<SceneOverlayResult>();
        var alignment = await _engine.GetVoiceAlignmentAsync(projectId, ct);
        if (alignment is null || alignment.SceneVoices.Count == 0)
            return results;

        foreach (var sv in alignment.SceneVoices.OrderBy(v => v.Scene))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // 1. Stitch the scene's clips into one scene video.
                var clipUrls = await _stitch.CollectClipUrlsAsync(projectId, sv.Scene, ct: ct);
                if (clipUrls.Count == 0)
                {
                    results.Add(new SceneOverlayResult(sv.Scene, false, null, "no clips on disk for scene"));
                    continue;
                }

                string sceneVideoUrl;
                if (clipUrls.Count == 1)
                {
                    sceneVideoUrl = clipUrls[0];
                }
                else
                {
                    var stitched = await _stitch.ConcatAsync(clipUrls, ct);
                    if (!stitched.Success || string.IsNullOrWhiteSpace(stitched.Url))
                    {
                        results.Add(new SceneOverlayResult(sv.Scene, false, null, stitched.Error ?? "scene stitch failed"));
                        continue;
                    }
                    sceneVideoUrl = stitched.Url!;
                }

                // 2. No narration for this scene → keep it as-is (un-narrated).
                if (string.IsNullOrWhiteSpace(sv.VoiceAudioRelativePath))
                {
                    results.Add(new SceneOverlayResult(sv.Scene, true, sceneVideoUrl, null));
                    continue;
                }

                var audioUrl = await _media.GetLocalBlobUrlAsync(projectId, sv.VoiceAudioRelativePath);
                if (string.IsNullOrWhiteSpace(audioUrl))
                {
                    // Voice not synced locally — keep the un-narrated scene rather than dropping it.
                    results.Add(new SceneOverlayResult(sv.Scene, true, sceneVideoUrl, "voice audio not synced"));
                    continue;
                }

                // 3. Overlay the single continuous narration onto the whole scene video (plays from the
                //    scene start; the browser mix ducks the bed and boosts the voice).
                var overlaySegments = new object[]
                {
                    new { audioUrl, startSec = 0.0, endSec = 0.0 },
                };
                var overlay = await _js.InvokeAsync<JsOverlayResult>(
                    "PageToMovieFfmpeg.overlayVoiceSegmentsAsync",
                    ct, sceneVideoUrl, overlaySegments, new { });

                if (overlay is { Success: true } && !string.IsNullOrWhiteSpace(overlay.Url))
                    results.Add(new SceneOverlayResult(sv.Scene, true, overlay.Url, null));
                else
                    results.Add(new SceneOverlayResult(sv.Scene, false, null, overlay?.Error ?? "overlay failed"));
            }
            catch (Exception ex)
            {
                results.Add(new SceneOverlayResult(sv.Scene, false, null, ex.Message));
            }
        }

        return results;
    }

    private sealed class JsOverlayResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }
}
