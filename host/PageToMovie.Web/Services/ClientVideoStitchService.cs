using Microsoft.JSInterop;
using PageToMovie.Core.Models;

namespace PageToMovie.Web.Services;

/// <summary>
/// Browser-side video concat via <c>PageToMovieFfmpeg</c> (ffmpeg.wasm).
/// Offloads multi-clip / multi-scene preview stitch from the API host.
/// </summary>
public sealed class ClientVideoStitchService
{
    private readonly IJSRuntime _js;
    private readonly EngineApiClient _engine;

    public ClientVideoStitchService(IJSRuntime js, EngineApiClient engine)
    {
        _js = js;
        _engine = engine;
    }

    /// <summary>
    /// Ordered media URLs for the given scenes: prefer a fresh composite, else on-disk clips.
    /// </summary>
    public async Task<IReadOnlyList<string>> CollectSceneMediaUrlsAsync(
        string projectId,
        IReadOnlyList<int> sceneNumbers,
        IReadOnlyList<SceneSummary>? sceneList,
        IReadOnlySet<int>? staleScenes,
        CancellationToken ct = default)
    {
        var urls = new List<string>();
        foreach (var sn in sceneNumbers)
        {
            ct.ThrowIfCancellationRequested();
            var summary = sceneList?.FirstOrDefault(s => s.SceneNumber == sn);
            var compositeOk = summary?.CompositeExists == true
                              && (staleScenes is null || !staleScenes.Contains(sn));

            if (compositeOk)
            {
                urls.Add(_engine.CompositeVideoUrl(projectId, sn));
                continue;
            }

            // Need clip list (summary alone has counts only)
            SceneDetail? detail = null;
            try
            {
                detail = (await _engine.GetSceneDetailAsync(projectId, sn, ct))?.Scene;
            }
            catch
            {
                // fall through
            }

            var clips = detail?.Clips?
                .Where(c => c.OnDisk)
                .OrderBy(c => c.ClipNumber)
                .ToList();

            if (clips is { Count: > 0 })
            {
                foreach (var c in clips)
                    urls.Add(_engine.ClipVideoUrl(projectId, sn, c.ClipNumber));
                continue;
            }

            // Last resort: composite may exist but be marked stale — still playable for preview
            if (summary?.CompositeExists == true || detail?.CompositeExists == true)
                urls.Add(_engine.CompositeVideoUrl(projectId, sn));
        }

        return urls;
    }

    /// <summary>On-disk clip URLs for one scene (ordered).</summary>
    public async Task<IReadOnlyList<string>> CollectClipUrlsAsync(
        string projectId,
        int sceneNumber,
        SceneDetail? detail = null,
        CancellationToken ct = default)
    {
        if (detail is null)
            detail = (await _engine.GetSceneDetailAsync(projectId, sceneNumber, ct))?.Scene;
        if (detail?.Clips is null || detail.Clips.Count == 0)
            return Array.Empty<string>();

        return detail.Clips
            .Where(c => c.OnDisk)
            .OrderBy(c => c.ClipNumber)
            .Select(c => _engine.ClipVideoUrl(projectId, sceneNumber, c.ClipNumber))
            .ToList();
    }

    public async Task<ClientStitchResult> ConcatAsync(
        IReadOnlyList<string> urls,
        CancellationToken ct = default)
    {
        if (urls is null || urls.Count == 0)
            return ClientStitchResult.Fail("No video URLs to combine");

        if (urls.Count == 1)
            return ClientStitchResult.Ok(urls[0], count: 1, single: true);

        try
        {
            var raw = await _js.InvokeAsync<JsConcatResult>(
                "PageToMovieFfmpeg.concatVideosAsync",
                ct,
                urls.ToArray());

            if (raw is null)
                return ClientStitchResult.Fail("No response from browser stitch");

            if (!raw.Success)
                return ClientStitchResult.Fail(raw.Error ?? "Browser stitch failed");

            if (string.IsNullOrWhiteSpace(raw.Url))
                return ClientStitchResult.Fail("Stitch produced no video URL");

            return ClientStitchResult.Ok(raw.Url!, raw.Count > 0 ? raw.Count : urls.Count, raw.Single);
        }
        catch (JSException jex)
        {
            return ClientStitchResult.Fail(jex.Message);
        }
        catch (Exception ex)
        {
            return ClientStitchResult.Fail(ex.Message);
        }
    }

    public async Task RevokePreviewUrlAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("PageToMovieFfmpeg.revokePreviewUrl");
        }
        catch
        {
            // optional
        }
    }

    private sealed class JsConcatResult
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
        public int Count { get; set; }
        public bool Single { get; set; }
    }
}

public sealed class ClientStitchResult
{
    public bool Success { get; init; }
    public string? Url { get; init; }
    public string? Error { get; init; }
    public int Count { get; init; }
    public bool Single { get; init; }

    public static ClientStitchResult Ok(string url, int count = 1, bool single = false) =>
        new() { Success = true, Url = url, Count = count, Single = single };

    public static ClientStitchResult Fail(string error) =>
        new() { Success = false, Error = error };
}
