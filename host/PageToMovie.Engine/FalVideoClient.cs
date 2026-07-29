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

        var numFrames = durationSeconds is > 0 and <= 4 ? 85 : 129;

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["aspect_ratio"] = "16:9",
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
            endpoint = "fal-ai/hunyuan-video/image-to-video";
            var bytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
            var b64 = Convert.ToBase64String(bytes);
            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg",
            };
            payload["image_url"] = $"data:{mime};base64,{b64}";
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

        _log.LogInformation("Fal.ai HunyuanVideo job submitted: {RequestId}", reqId);
        return reqId;
    }

    public async Task<string> PollForVideoUrlAsync(string requestId, Action<string>? onProgress, CancellationToken ct)
    {
        var apiKey = ResolveApiKey()
            ?? throw new InvalidOperationException($"Fal.ai API key is missing ({SupportedModelCatalog.FalApiKeyEnv}).");

        var statusUrl = $"fal-ai/hunyuan-video/requests/{requestId}/status";
        var resultUrl = $"fal-ai/hunyuan-video/requests/{requestId}";

        var delay = TimeSpan.FromSeconds(3);
        var maxAttempts = 120; // 6 minutes max

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
}
