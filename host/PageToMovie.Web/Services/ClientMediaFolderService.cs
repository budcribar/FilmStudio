using Microsoft.JSInterop;
using PageToMovie.Core.Models;
using PageToMovie.Core.Utils;
using System.Linq;

namespace PageToMovie.Web.Services;

/// <summary>
/// Binds a user media folder and syncs gen clips from server proxy → disk → hash registry.
/// </summary>
public sealed class ClientMediaFolderService
{
    private readonly IJSRuntime _js;
    private readonly EngineApiClient _api;
    private readonly JobHubClient _hub;
    private bool _hubHooked;
    /// <summary>In-flight saves keyed by projectId|relativePath — avoids double JobUpdated.</summary>
    private readonly HashSet<string> _savingKeys = new(StringComparer.OrdinalIgnoreCase);

    public ClientMediaFolderService(IJSRuntime js, EngineApiClient api, JobHubClient hub)
    {
        _js = js;
        _api = api;
        _hub = hub;
    }

    public string? FolderName { get; private set; }
    public bool IsConnected => !string.IsNullOrEmpty(FolderName);
    public string? LastStatus { get; private set; }
    public event Action? Changed;

    public async Task EnsureHubHookAsync()
    {
        if (_hubHooked) return;
        _hubHooked = true;
        await _hub.EnsureStartedAsync();
        _hub.JobUpdated += OnJobUpdated;
    }

    private void OnJobUpdated(JobSnapshot snap)
    {
        if (snap is null) return;
        if (!string.Equals(snap.Status, "done", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snap.Status, "running", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(snap.ClientMediaUrl) ||
            string.IsNullOrWhiteSpace(snap.ClientRelativePath) ||
            string.IsNullOrWhiteSpace(snap.ProjectId))
            return;
        _ = SaveJobMediaAsync(snap);
    }

    public async Task<bool> ConnectFolderAsync()
    {
        try
        {
            var r = await _js.InvokeAsync<JsResult>("PageToMovieMedia.connectFolderAsync");
            if (r is { Success: true })
            {
                FolderName = r.FolderName;
                LastStatus = $"Media folder: {FolderName}";
                Changed?.Invoke();
                await EnsureHubHookAsync();
                return true;
            }
            LastStatus = r?.Error ?? "Could not connect folder";
            Changed?.Invoke();
            return false;
        }
        catch (Exception ex)
        {
            LastStatus = ex.Message;
            Changed?.Invoke();
            return false;
        }
    }

    public async Task SaveJobMediaAsync(JobSnapshot snap)
    {
        var key = $"{snap.ProjectId}|{snap.ClientRelativePath}";
        lock (_savingKeys)
        {
            if (!_savingKeys.Add(key))
                return; // already saving this path
        }

        try
        {
            if (!IsConnected)
            {
                // Prompt once when gen finishes
                var ok = await ConnectFolderAsync();
                if (!ok) return;
            }

            LastStatus = $"Saving {snap.ClientRelativePath}…";
            Changed?.Invoke();

            var url = snap.ClientMediaUrl!;

            // Silence-trim in browser (ffmpeg.wasm) before write. Decision logic
            // (where to cut) lives once in ClipSilenceTrimmer (Core) — JS only does
            // the ffmpeg I/O. Longer breath tail for speech-style clips; lead trim on clip 2+.
            var clipNum = snap.Clip ?? 1;
            var isCredits = (snap.ClientRelativePath ?? "")
                .Contains("credits.mp4", StringComparison.OrdinalIgnoreCase);
            var keepTail = isCredits
                ? ClipSilenceTrimmer.DefaultKeepTailSeconds
                : ClipSilenceTrimmer.SpeechBreathTailSeconds; // safe default without dialogue metadata

            string? silenceMessage = null;
            string? trimmedBlobUrl = null;
            var urlToSave = url;
            if (!isCredits) // credits plate is a title card; leave full length
            {
                var (trimmed, trimUrl, message) = await SilenceTrimAsync(
                    url,
                    keepTailSeconds: keepTail,
                    trimLeading: clipNum > 1,
                    keepHeadSeconds: 0.08);
                silenceMessage = message;
                if (trimmed && !string.IsNullOrWhiteSpace(trimUrl))
                {
                    trimmedBlobUrl = trimUrl;
                    urlToSave = trimUrl!;
                }
            }

            try
            {
                var saved = await _js.InvokeAsync<JsSaveResult>(
                    "PageToMovieMedia.saveFromUrlAsync",
                    urlToSave,
                    snap.ClientRelativePath,
                    null);

                if (saved is not { Success: true } || string.IsNullOrWhiteSpace(saved.Sha256))
                {
                    LastStatus = saved?.Error ?? "Save failed";
                    Changed?.Invoke();
                    return;
                }

                var scene = snap.Scene;
                var clip = snap.Clip;
                await _api.RegisterMediaAsync(snap.ProjectId!, new MediaRegisterRequest
                {
                    RelativePath = saved.RelativePath ?? snap.ClientRelativePath!,
                    Sha256 = saved.Sha256,
                    SizeBytes = saved.SizeBytes,
                    Kind = isCredits ? "credits" : "clip",
                    Scene = scene,
                    Clip = clip,
                });

                var sil = string.IsNullOrWhiteSpace(silenceMessage)
                    ? ""
                    : $" · silence: {silenceMessage}";
                LastStatus =
                    $"Saved {Path.GetFileName(snap.ClientRelativePath)} ({saved.SizeBytes / 1024} KB){sil}";
                Changed?.Invoke();
            }
            finally
            {
                if (trimmedBlobUrl is not null)
                {
                    try { await _js.InvokeVoidAsync("PageToMovieMedia.revokeUrl", trimmedBlobUrl); }
                    catch { /* best effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            LastStatus = ex.Message;
            Changed?.Invoke();
        }
        finally
        {
            lock (_savingKeys)
                _savingKeys.Remove(key);
        }
    }

    /// <summary>
    /// Analyze a clip's silence (browser ffmpeg.wasm), decide cut points with the real
    /// <see cref="ClipSilenceTrimmer"/> math (no JS port to drift), and either encode the
    /// trimmed slice or discard the analysis session. Never throws — failures degrade to
    /// "not trimmed" so a save is never blocked by a browser/codec hiccup.
    /// </summary>
    private async Task<(bool Trimmed, string? Url, string? Message)> SilenceTrimAsync(
        string url,
        double keepTailSeconds,
        bool trimLeading,
        double keepHeadSeconds,
        double minTrimSavings = 0.4)
    {
        string? token = null;
        try
        {
            var analysis = await _js.InvokeAsync<JsSilenceAnalysis>(
                "PageToMovieFfmpeg.analyzeSilenceAsync", url, new { });
            if (analysis is not { Success: true })
                return (false, null, "skip: " + (analysis?.Error ?? "analyze failed"));
            if (analysis.Token is null)
                return (false, null, analysis.Error ?? "skip: nothing to analyze");

            token = analysis.Token;
            var total = analysis.TotalSec;
            double startSec = 0, endSec = total;
            var notes = new List<string>();

            var cutAt = ClipSilenceTrimmer.ComputeCutPoint(analysis.Log ?? "", total, keepTailSeconds);
            if (cutAt is { } cut && (total - cut) >= minTrimSavings)
            {
                endSec = cut;
                notes.Add($"tail −{(total - cut):F2}s");
            }

            if (trimLeading)
            {
                var lead = ClipSilenceTrimmer.ComputeLeadInPoint(analysis.Log ?? "", total, keepHeadSeconds);
                if (lead is { } l && l >= 0.25 && endSec - l >= ClipSilenceTrimmer.MinClipSeconds - 0.25)
                {
                    startSec = l;
                    notes.Add($"head −{l:F2}s");
                }
            }

            if (startSec <= 0.001 && endSec >= total - 0.05)
            {
                await _js.InvokeVoidAsync("PageToMovieFfmpeg.discardSessionAsync", token);
                token = null;
                return (false, null, notes.Count > 0 ? string.Join("; ", notes) : "skip: no trailing/leading silence");
            }

            var durationSec = Math.Max(0.5, endSec - startSec);
            var enc = await _js.InvokeAsync<JsSilenceEncode>(
                "PageToMovieFfmpeg.encodeSliceAsync", token, startSec, durationSec);
            token = null; // encodeSliceAsync always consumes/cleans up the session
            if (enc is not { Success: true } || string.IsNullOrWhiteSpace(enc.Url))
                return (false, null, "skip: re-encode failed — " + (enc?.Error ?? ""));

            return (true, enc.Url, notes.Count > 0 ? string.Join("; ", notes) : "trimmed");
        }
        catch (Exception ex)
        {
            return (false, null, "skip: " + ex.Message);
        }
        finally
        {
            if (token is not null)
            {
                try { await _js.InvokeVoidAsync("PageToMovieFfmpeg.discardSessionAsync", token); }
                catch { /* best effort */ }
            }
        }
    }

    public async Task<string?> GetLocalBlobUrlAsync(string relativePath)
    {
        if (!IsConnected) return null;
        try
        {
            var r = await _js.InvokeAsync<JsBlobResult>("PageToMovieMedia.getBlobUrlAsync", relativePath);
            return r is { Success: true } ? r.Url : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Ok, string? Sha, long Size, string? Error)> RegisterBlobAsExportAsync(
        string projectId,
        string blobUrl,
        string relativePath)
    {
        try
        {
            if (!IsConnected && !await ConnectFolderAsync())
                return (false, null, 0, "Media folder required");

            var saved = await _js.InvokeAsync<JsSaveResult>(
                "PageToMovieMedia.saveBlobUrlAsync", blobUrl, relativePath);
            if (saved is not { Success: true } || string.IsNullOrWhiteSpace(saved.Sha256))
                return (false, null, 0, saved?.Error ?? "Save failed");

            await _api.RegisterMediaAsync(projectId, new MediaRegisterRequest
            {
                RelativePath = relativePath,
                Sha256 = saved.Sha256,
                SizeBytes = saved.SizeBytes,
                Kind = "export",
            });
            return (true, saved.Sha256, saved.SizeBytes, null);
        }
        catch (Exception ex)
        {
            return (false, null, 0, ex.Message);
        }
    }

    /// <summary>Archived previous versions of one clip's video, newest first (see ClipPromptCompareViewer).</summary>
    public async Task<IReadOnlyList<string>> ListClipHistoryRelativePathsAsync(int scene, int clip)
    {
        if (!IsConnected) return Array.Empty<string>();
        try
        {
            var r = await _js.InvokeAsync<JsHistoryResult>(
                "PageToMovieMedia.listClipHistoryAsync", scene, clip);
            return r is { Success: true, Entries: not null }
                ? r.Entries.Select(e => e.RelativePath ?? "").Where(p => p.Length > 0).ToList()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private sealed class JsResult
    {
        public bool Success { get; set; }
        public string? FolderName { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsHistoryResult
    {
        public bool Success { get; set; }
        public List<JsHistoryEntry>? Entries { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsHistoryEntry
    {
        public string? RelativePath { get; set; }
        public long TimestampMs { get; set; }
    }

    private sealed class JsSaveResult
    {
        public bool Success { get; set; }
        public string? Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public string? RelativePath { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsSilenceAnalysis
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public double TotalSec { get; set; }
        public string? Log { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsSilenceEncode
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsBlobResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }
}
