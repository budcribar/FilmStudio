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
    private readonly ActiveProjectState _activeProject;
    private bool _hubHooked;
    /// <summary>In-flight saves keyed by projectId|relativePath — avoids double JobUpdated.</summary>
    private readonly HashSet<string> _savingKeys = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Completed saves keyed by projectId|relativePath — a later notification for the same
    /// path (e.g. a single-clip job's "done" tick after its "running" tick already saved it) is a no-op.</summary>
    private readonly HashSet<string> _savedKeys = new(StringComparer.OrdinalIgnoreCase);

    public ClientMediaFolderService(IJSRuntime js, EngineApiClient api, JobHubClient hub, ActiveProjectState activeProject)
    {
        _js = js;
        _api = api;
        _hub = hub;
        _activeProject = activeProject;
        _activeProject.Changed += OnActiveProjectChanged;
    }

    private void OnActiveProjectChanged()
    {
        TriggerAutoSyncIfConnected();
    }

    public void TriggerAutoSyncIfConnected()
    {
        if (IsConnected && !IsSyncing && !string.IsNullOrWhiteSpace(_activeProject.ProjectId))
        {
            _ = SyncProjectMediaToClientAsync(_activeProject.ProjectId);
        }
    }

    public string? FolderName { get; private set; }
    public string? FullPath { get; private set; }
    public bool IsConnected => !string.IsNullOrEmpty(FolderName) || !string.IsNullOrEmpty(FullPath);
    public string? LastStatus { get; private set; }

    public async Task SetFullPathAsync(string? path)
    {
        FullPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        try
        {
            await _js.InvokeVoidAsync("PageToMovieMedia.setFullPath", FullPath);
        }
        catch { /* ignore */ }
        Changed?.Invoke();
    }

    private async Task RefreshFullPathAsync()
    {
        try
        {
            var p = await _js.InvokeAsync<string?>("PageToMovieMedia.getFullPath");
            if (!string.IsNullOrWhiteSpace(p))
                FullPath = p.Trim();
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// A previously-connected folder was found (persisted via IndexedDB) but the browser needs a
    /// user gesture to re-grant permission — call <see cref="ReconnectAsync"/> from a button click.
    /// Set by <see cref="TryReconnectAsync"/>; distinct from "never connected" (<see cref="IsConnected"/> false,
    /// this also false) so the UI can offer a 1-click "Reconnect {name}" instead of a fresh folder picker.
    /// </summary>
    public bool NeedsReconnect { get; private set; }
    public string? PendingReconnectFolderName { get; private set; }

    /// <summary>
    /// One-shot operator message when a clip finished with a client proxy URL
    /// but was not saved to a local media folder (feature 8 / fallback path).
    /// </summary>
    public string? LocalSaveWarning { get; private set; }

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
        // "done"-only would drop every clip but the last in a multi-clip batch: ClientMediaUrl/
        // ClientRelativePath are set per-clip while Status stays "running" for the whole batch loop
        // (FilmJobService.RunBatchGenAsync → GenerateOneClipAsync), only flipping to "done" once, at
        // the very end. So both statuses must be accepted here; _savedKeys below is what prevents a
        // path that already saved on its "running" tick from being re-saved on a later "done" tick.
        if (!string.Equals(snap.Status, "done", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snap.Status, "running", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(snap.ClientMediaUrl) ||
            string.IsNullOrWhiteSpace(snap.ClientRelativePath) ||
            string.IsNullOrWhiteSpace(snap.ProjectId))
            return;

        var key = $"{snap.ProjectId}|{snap.ClientRelativePath}";
        lock (_savingKeys)
        {
            if (_savedKeys.Contains(key)) return; // already completed
        }
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
                await RefreshFullPathAsync();
                LastStatus = $"Media folder: {FullPath ?? FolderName}";
                LocalSaveWarning = null; // folder connected — clear fallback warning
                Changed?.Invoke();
                await EnsureHubHookAsync();
                TriggerAutoSyncIfConnected();
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

    /// <summary>
    /// Silent, no-gesture attempt to resume a previously-connected folder (the actual
    /// FileSystemDirectoryHandle persisted to IndexedDB by a prior <see cref="ConnectFolderAsync"/>).
    /// Call on app start (e.g. NavMenu's first render). If the browser still grants permission
    /// without asking, reconnects immediately with no UI at all. Otherwise sets
    /// <see cref="NeedsReconnect"/> so the UI can offer a 1-click "Reconnect" button wired to
    /// <see cref="ReconnectAsync"/> (which needs an actual click to satisfy the permission prompt).
    /// Never throws — a failed silent reconnect just leaves the folder disconnected, same as today.
    /// </summary>
    public async Task TryReconnectAsync()
    {
        if (IsConnected) return;
        try
        {
            var r = await _js.InvokeAsync<JsReconnectResult>("PageToMovieMedia.tryReconnectAsync");
            if (r is { Success: true })
            {
                FolderName = r.FolderName;
                await RefreshFullPathAsync();
                LastStatus = $"Media folder: {FullPath ?? FolderName}";
                NeedsReconnect = false;
                PendingReconnectFolderName = null;
                Changed?.Invoke();
                await EnsureHubHookAsync();
                TriggerAutoSyncIfConnected();
                return;
            }
            if (string.Equals(r?.Reason, "prompt", StringComparison.OrdinalIgnoreCase))
            {
                NeedsReconnect = true;
                PendingReconnectFolderName = r!.FolderName;
                Changed?.Invoke();
            }
        }
        catch
        {
            // best-effort only — silent reconnect failures are not user-visible errors
        }
    }

    /// <summary>
    /// Re-grants permission on the remembered folder from a real user gesture (button click) — no
    /// folder-browser dialog, just a permission re-prompt on the same previously-chosen handle.
    /// </summary>
    public async Task<bool> ReconnectAsync()
    {
        try
        {
            var r = await _js.InvokeAsync<JsReconnectResult>("PageToMovieMedia.reconnectAsync");
            if (r is { Success: true })
            {
                FolderName = r.FolderName;
                LastStatus = $"Media folder: {FolderName}";
                NeedsReconnect = false;
                PendingReconnectFolderName = null;
                LocalSaveWarning = null;
                Changed?.Invoke();
                await EnsureHubHookAsync();
                TriggerAutoSyncIfConnected();
                return true;
            }
            LastStatus = r?.Error ?? "Could not reconnect folder";
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

    /// <summary>Dismiss the local-save fallback warning (operator closed the banner).</summary>
    public void DismissLocalSaveWarning()
    {
        if (LocalSaveWarning is null) return;
        LocalSaveWarning = null;
        Changed?.Invoke();
    }

    private void NoteLocalSaveNeeded(string? connectError = null)
    {
        // Outcome-only copy (no server/provider jargon).
        if (!string.IsNullOrWhiteSpace(connectError) &&
            (connectError.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
             connectError.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
             connectError.Contains("does not support", StringComparison.OrdinalIgnoreCase) ||
             connectError.Contains("not support", StringComparison.OrdinalIgnoreCase)))
        {
            LocalSaveWarning =
                "Folder save requires Chrome or Edge. This clip is available for a limited time — open it soon, or use Chrome/Edge and connect a folder next time.";
        }
        else
        {
            LocalSaveWarning =
                "Your clip was generated but couldn’t be saved on this computer. Connect a folder to keep it permanently.";
        }
        LastStatus = LocalSaveWarning;
        Changed?.Invoke();
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
                // Offer folder picker once; if declined / unsupported, surface feature-8 fallback.
                var ok = await ConnectFolderAsync();
                if (!ok)
                {
                    NoteLocalSaveNeeded(LastStatus);
                    return;
                }
            }

            LastStatus = $"Saving {snap.ClientRelativePath}…";
            Changed?.Invoke();

            var url = snap.ClientMediaUrl!;

            // Silence-trim in browser (ffmpeg.wasm) before write. Decision logic
            // (where to cut) lives once in ClipSilenceTrimmer (Core) — JS only does
            // the ffmpeg I/O. Longer breath tail for speech-style clips; lead trim on clip 2+.
            var clipNum = snap.Clip ?? 1;
            var isCredits = (snap.ClientRelativePath ?? "").Contains("credits", StringComparison.OrdinalIgnoreCase) ||
                            (snap.ClientRelativePath ?? "").Contains("sc18", StringComparison.OrdinalIgnoreCase) ||
                            snap.Scene == 18 ||
                            string.Equals(snap.Kind, "credits", StringComparison.OrdinalIgnoreCase);
            var isMusic = string.Equals(snap.Kind, "music", StringComparison.OrdinalIgnoreCase);
            var keepTail = isCredits
                ? ClipSilenceTrimmer.DefaultKeepTailSeconds
                : ClipSilenceTrimmer.SpeechBreathTailSeconds; // safe default without dialogue metadata

            string? silenceMessage = null;
            string? trimmedBlobUrl = null;
            var urlToSave = url;
            if (!isCredits && !isMusic) // credits plate and music tracks have no dialogue to trim silence around
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
                    Kind = isCredits ? "credits" : isMusic ? "music" : "clip",
                    Scene = scene,
                    Clip = clip,
                });

                var sil = string.IsNullOrWhiteSpace(silenceMessage)
                    ? ""
                    : $" · silence: {silenceMessage}";
                LastStatus =
                    $"Saved {Path.GetFileName(snap.ClientRelativePath)} ({saved.SizeBytes / 1024} KB){sil}";
                Changed?.Invoke();

                lock (_savingKeys)
                    _savedKeys.Add(key);
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
            var r = await _js.InvokeAsync<JsBlobResult>("PageToMovieMedia.getBlobUrlAsync", relativePath, false);
            return r is { Success: true } ? r.Url : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Cheap metadata-only check of a local file — no blob URL created, no bytes read.</summary>
    public async Task<(bool Found, long SizeBytes)> StatLocalFileAsync(string relativePath)
    {
        if (!IsConnected) return (false, 0);
        try
        {
            var r = await _js.InvokeAsync<JsStatResult>("PageToMovieMedia.statLocalFileAsync", relativePath);
            return r is { Success: true } ? (true, r.SizeBytes) : (false, 0);
        }
        catch
        {
            return (false, 0);
        }
    }

    /// <summary>
    /// The generalized "is my local copy of this file still current" check every media-playback
    /// call site should use instead of trusting whatever happens to be at that path — a local
    /// file at the expected filename is not guaranteed to be the *current* version (a later
    /// regen/promote may never have re-synced it to this browser). Works for any media kind
    /// (video clip today, audio track once that syncs the same way) since it's driven entirely
    /// by size comparison against the server's registered value, not clip-specific fields.
    /// Returns the local blob URL only when the local file's size matches what the server has
    /// registered as current; otherwise null, so the caller falls back to streaming from server.
    /// </summary>
    public async Task<string?> GetCurrentBlobUrlAsync(string relativePath, long? expectedSizeBytes)
    {
        if (!IsConnected) return null;
        if (!await IsLocalCopyCurrentAsync(relativePath, expectedSizeBytes)) return null;
        return await GetLocalBlobUrlAsync(relativePath);
    }

    /// <summary>
    /// Shared freshness check behind <see cref="GetCurrentBlobUrlAsync"/> and
    /// <see cref="GetClipBytesAsync"/> — true when no expected size was given (nothing to compare
    /// against) or the local file's size matches it. Caller must have already confirmed
    /// <see cref="IsConnected"/>.
    /// </summary>
    private async Task<bool> IsLocalCopyCurrentAsync(string relativePath, long? expectedSizeBytes)
    {
        if (expectedSizeBytes is null or <= 0) return true;
        var (found, localSize) = await StatLocalFileAsync(relativePath);
        return found && localSize == expectedSizeBytes;
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

    /// <summary>True while MP4/sidecar files are actively downloading to the local client folder.</summary>
    public bool IsSyncing { get; private set; }
    public int SyncCurrent { get; private set; }
    public int SyncTotal { get; private set; }
    public string? SyncCurrentFile { get; private set; }
    public string? SyncProjectId { get; private set; }
    public double SyncPercent => SyncTotal > 0 ? Math.Round((double)SyncCurrent / SyncTotal * 100.0, 0) : 0;

    /// <summary>
    /// Sync project media files (MP4s and sidecars) from server to client local media folder.
    /// Called after Admin import or project load when a client folder is connected.
    /// </summary>
    public async Task<int> SyncProjectMediaToClientAsync(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return 0;

        if (!IsConnected)
        {
            await TryReconnectAsync();
        }

        if (!IsConnected)
        {
            LastStatus = "Connect local media folder to save project videos locally";
            Changed?.Invoke();
            return 0;
        }

        try
        {
            IsSyncing = true;
            SyncProjectId = projectId;
            SyncCurrent = 0;
            SyncTotal = 0;
            SyncCurrentFile = null;

            LastStatus = $"Syncing project '{projectId}' media to local folder…";
            Changed?.Invoke();

            var syncList = await _api.GetProjectMediaSyncListAsync(projectId);
            var count = 0;

            if (syncList?.Files is not null)
            {
                SyncTotal = syncList.Files.Count;
                Changed?.Invoke();

                for (var i = 0; i < syncList.Files.Count; i++)
                {
                    var file = syncList.Files[i];
                    SyncCurrent = i + 1;
                    SyncCurrentFile = file.FileName;
                    LastStatus = $"Downloading {file.FileName} to local folder ({SyncCurrent}/{SyncTotal})…";
                    Changed?.Invoke();

                    if (string.IsNullOrWhiteSpace(file.StreamUrl))
                        continue;

                    var saved = await _js.InvokeAsync<JsSaveResult>(
                        "PageToMovieMedia.saveFromUrlAsync",
                        file.StreamUrl,
                        file.RelativePath,
                        null);

                    if (saved is { Success: true } && !string.IsNullOrWhiteSpace(saved.Sha256))
                    {
                        count++;
                        await _api.RegisterMediaAsync(projectId, new MediaRegisterRequest
                        {
                            RelativePath = saved.RelativePath ?? file.RelativePath,
                            Sha256 = saved.Sha256,
                            SizeBytes = saved.SizeBytes,
                            Kind = file.IsMp4 ? "clip" : "sidecar",
                        });
                    }
                }
            }

            LastStatus = $"Media folder synced: {count} file(s) saved on local disk";
            Changed?.Invoke();
            return count;
        }
        catch (Exception ex)
        {
            LastStatus = $"Sync error: {ex.Message}";
            Changed?.Invoke();
            return 0;
        }
        finally
        {
            IsSyncing = false;
            SyncCurrentFile = null;
            Changed?.Invoke();
        }
    }

    private sealed class JsResult
    {
        public bool Success { get; set; }
        public string? FolderName { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsReconnectResult
    {
        public bool Success { get; set; }
        public string? FolderName { get; set; }
        /// <summary>When !Success: "none" (never connected before), "prompt" (needs a user gesture), "denied", or "error".</summary>
        public string? Reason { get; set; }
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

    /// <summary>Local blob URLs for a scene's background-music segments (in order), stopping at
    /// the first missing segment. Segment relative paths mirror
    /// MediaRegistryService.MusicSegmentRelativePath in PageToMovie.Engine (Web doesn't reference
    /// Engine, so the format is kept in sync here — same convention as the clip path below).</summary>
    public async Task<IReadOnlyList<string>> GetSceneMusicSegmentUrlsAsync(int scene, int maxSegments = 20)
    {
        var urls = new List<string>();
        if (!IsConnected) return urls;
        for (var seg = 1; seg <= maxSegments; seg++)
        {
            var relPath = $"assets/music/scene_{scene:D2}_seg_{seg:D2}.wav";
            var url = await GetLocalBlobUrlAsync(relPath);
            if (string.IsNullOrWhiteSpace(url)) break;
            urls.Add(url);
        }
        return urls;
    }

    /// <summary>
    /// Local clip bytes for upload (e.g. dialogue re-verification). Same staleness guard as
    /// <see cref="GetCurrentBlobUrlAsync"/> — without it, a stale local copy (an older take that
    /// hasn't been overwritten by a since-promoted regeneration) gets silently uploaded and
    /// verified as if it were current, making re-verification look like it never picked up the
    /// new clip. Pass the server's currently-registered size (GetClipMediaStatusAsync) to enable
    /// the check; omit it to keep the old unconditional-trust behavior for other callers.
    /// </summary>
    public async Task<byte[]?> GetClipBytesAsync(int scene, int clip, long? expectedSizeBytes = null)
    {
        if (!IsConnected) return null;
        try
        {
            var relPath = $"assets/video/scene_{scene:D2}_clip_{clip:D2}.mp4";
            if (!await IsLocalCopyCurrentAsync(relPath, expectedSizeBytes)) return null;
            var res = await _js.InvokeAsync<JsBytesResult>("PageToMovieMedia.getBytesAsync", relPath);
            return res is { Success: true, Bytes: not null } ? res.Bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class JsBytesResult
    {
        public bool Success { get; set; }
        public byte[]? Bytes { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsBlobResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public long SizeBytes { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsStatResult
    {
        public bool Success { get; set; }
        public long SizeBytes { get; set; }
        public long LastModifiedMs { get; set; }
        public string? Error { get; set; }
    }
}
