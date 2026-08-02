using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Fal.ai serverless GPU client for HunyuanVideo (13B DiT open-source video generation).
/// Queue endpoint: https://queue.fal.run/fal-ai/hunyuan-video
/// </summary>
public sealed class FalVideoClient : IVideoClient
{
    public const string ApiBase = SupportedModelCatalog.FalApiBase;

    private readonly HttpClient _http;
    private readonly PageToMovieOptions _opts;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<FalVideoClient> _log;

    public FalVideoClient(
        HttpClient http,
        IOptions<PageToMovieOptions> opts,
        ProjectTelemetryService telemetry,
        ILogger<FalVideoClient> log)
    {
        _http = http;
        _opts = opts.Value;
        _telemetry = telemetry;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase.TrimEnd('/') + "/");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    private static string? ResolveApiKey()
    {
        var key = ApiKeyScope.CurrentFal
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyEnv)
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyFallbackEnv);
        if (!string.IsNullOrWhiteSpace(key)) return key.Trim(' ', '"', '\'', '\r', '\n', '\t');
        return null;
    }

    public async Task<string> SubmitGenerationAsync(
        string prompt,
        int durationSeconds,
        string resolution,
        string model,
        CancellationToken ct,
        IReadOnlyList<string>? referenceImagePaths = null,
        string? startFrameImagePath = null,
        string? continueFromVideoPath = null)
    {
        var apiKey = ResolveApiKey()
            ?? throw new InvalidOperationException($"Fal.ai API key is missing. Set {SupportedModelCatalog.FalApiKeyEnv} in environment or Configuration.");

        var maxLen = SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video).MaxPromptLength ?? 1000;
        if (prompt.Length > maxLen)
        {
            prompt = ClipVideoPromptBuilder.FitPromptToVideoBudget(prompt, maxLen);
        }

        var numFrames = durationSeconds is > 0 and <= 4 ? 85 : 129;

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["aspect_ratio"] = ResolveAspectRatio(model),
            ["num_frames"] = numFrames,
            ["num_inference_steps"] = 30,
        };

        var imagePath = !string.IsNullOrWhiteSpace(startFrameImagePath) && File.Exists(startFrameImagePath)
            ? startFrameImagePath
            : referenceImagePaths is { Count: > 0 } && File.Exists(referenceImagePaths[0])
                ? referenceImagePaths[0]
                : null;

        var endpoint = "fal-ai/hunyuan-video";
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            endpoint = "fal-ai/hunyuan-video-image-to-video";
            var maxDim = SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video).MaxReferenceImageDimension ?? 1280;
            payload["image_url"] = await PrepareOptimizedImageDataUriAsync(imagePath, maxDim, ct).ConfigureAwait(false);
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey);
        req.Content = JsonContent.Create(payload);

        var sw = Stopwatch.StartNew();
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Fal.ai HunyuanVideo submit failed HTTP {Status} ({Elapsed}ms): {Body}", resp.StatusCode, sw.ElapsedMilliseconds, body);
            throw new InvalidOperationException($"Fal.ai HunyuanVideo error {resp.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("request_id", out var reqIdEl) ||
            reqIdEl.GetString() is not { Length: > 0 } reqId)
        {
            throw new InvalidOperationException($"Fal.ai response missing request_id: {body}");
        }

        _log.LogInformation("Fal.ai HunyuanVideo job submitted to {Endpoint}: {RequestId}", endpoint, reqId);
        return $"{endpoint}:{reqId}";
    }

    public async Task<string> PollForVideoUrlAsync(string requestId, Action<string>? onProgress, CancellationToken ct)
    {
        var apiKey = ResolveApiKey()
            ?? throw new InvalidOperationException($"Fal.ai API key is missing ({SupportedModelCatalog.FalApiKeyEnv}).");

        var parts = requestId.Split(':', 2);
        var endpoint = parts.Length == 2 ? parts[0] : "fal-ai/hunyuan-video-image-to-video";
        var actualReqId = parts.Length == 2 ? parts[1] : requestId;

        var statusUrl = $"{endpoint}/requests/{actualReqId}/status";
        var resultUrl = $"{endpoint}/requests/{actualReqId}";

        var delay = TimeSpan.FromSeconds(3);
        var maxAttempts = 300; // 15 minutes max timeout

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var req = new HttpRequestMessage(HttpMethod.Get, statusUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var statusBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    onProgress?.Invoke("Fal.ai rate limited (HTTP 429) — retrying in 5s…");
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    continue;
                }

                _log.LogError("Fal.ai status query failed HTTP {Status} for request {RequestId}: {Body}", resp.StatusCode, requestId, statusBody);
                throw new InvalidOperationException($"Fal.ai status query error HTTP {resp.StatusCode}: {statusBody}");
            }

            using var doc = JsonDocument.Parse(statusBody);
            var status = doc.RootElement.TryGetProperty("status", out var stEl) ? stEl.GetString() ?? "" : "";
            var queuePos = doc.RootElement.TryGetProperty("queue_position", out var qEl) ? qEl.GetInt32().ToString() : null;

            onProgress?.Invoke(queuePos is not null ? $"Fal.ai status: {status} (queue position: {queuePos})" : $"Fal.ai status: {status}");

            if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                using var resultReq = new HttpRequestMessage(HttpMethod.Get, resultUrl);
                resultReq.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey);
                using var resultResp = await _http.SendAsync(resultReq, ct).ConfigureAwait(false);
                var resultBody = await resultResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!resultResp.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Fal.ai result fetch failed HTTP {resultResp.StatusCode}: {resultBody}");
                }

                using var resultDoc = JsonDocument.Parse(resultBody);
                if (resultDoc.RootElement.TryGetProperty("video", out var vEl) &&
                    vEl.TryGetProperty("url", out var urlEl) &&
                    urlEl.GetString() is { Length: > 0 } videoUrl)
                {
                    return videoUrl;
                }
                throw new InvalidOperationException($"Fal.ai result payload missing video.url: {resultBody}");
            }

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var err = doc.RootElement.TryGetProperty("error", out var eEl) ? eEl.GetString() : "Job failed";
                throw new InvalidOperationException($"Fal.ai generation failed on GPU: {err}");
            }

            await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"Fal.ai job {requestId} timed out after {maxAttempts * 3}s");
    }

    public async Task DownloadToFileAsync(string url, string destPath, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Catalog's <c>DefaultAspectRatio</c> for the requested Fal.ai model, falling back to the
    /// historical hardcoded "16:9" for models the catalog doesn't cover yet.
    /// </summary>
    private static string ResolveAspectRatio(string model) =>
        SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video).DefaultAspectRatio ?? "16:9";

    private static async Task<string> PrepareOptimizedImageDataUriAsync(string imagePath, int maxDim, CancellationToken ct)
    {
        try
        {
            var bytes = await Task.Run(() =>
            {
                using var original = SkiaSharp.SKBitmap.Decode(imagePath);
                if (original is null) return File.ReadAllBytes(imagePath);

                int width = original.Width;
                int height = original.Height;

                if (width > maxDim || height > maxDim)
                {
                    float scale = Math.Min((float)maxDim / width, (float)maxDim / height);
                    int newW = Math.Max(1, (int)(width * scale));
                    int newH = Math.Max(1, (int)(height * scale));

                    using var resized = original.Resize(new SkiaSharp.SKImageInfo(newW, newH), SkiaSharp.SKSamplingOptions.Default);
                    if (resized is not null)
                    {
                        using var image = SkiaSharp.SKImage.FromBitmap(resized);
                        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85);
                        return data.ToArray();
                    }
                }

                using var img = SkiaSharp.SKImage.FromBitmap(original);
                using var enc = img.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85);
                return enc.ToArray();
            }, ct).ConfigureAwait(false);

            return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            var rawBytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
            return $"data:image/jpeg;base64,{Convert.ToBase64String(rawBytes)}";
        }
    }
}
