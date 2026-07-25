using Microsoft.JSInterop;
using PageToMovie.Core.Models;

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
            if (url.StartsWith('/'))
            {
                // same-origin relative
            }

            var saved = await _js.InvokeAsync<JsSaveResult>(
                "PageToMovieMedia.saveFromUrlAsync",
                url,
                snap.ClientRelativePath);

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
                Kind = "clip",
                Scene = scene,
                Clip = clip,
            });

            LastStatus = $"Saved {Path.GetFileName(snap.ClientRelativePath)} ({saved.SizeBytes / 1024} KB)";
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            LastStatus = ex.Message;
            Changed?.Invoke();
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

    private sealed class JsResult
    {
        public bool Success { get; set; }
        public string? FolderName { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsSaveResult
    {
        public bool Success { get; set; }
        public string? Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public string? RelativePath { get; set; }
        public string? Error { get; set; }
    }

    private sealed class JsBlobResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }
}
