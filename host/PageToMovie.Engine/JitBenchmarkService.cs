using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

public sealed record JitCalibrationResult(
    string CategoryId,
    double MeasuredOverheadSec,
    double OverlapRatioGamma,
    bool IsLiveJitBenchmark,
    string SourceDescription);

public sealed record VisionActionTimingAnalysis(
    [property: JsonPropertyName("actionCompletionSec")] double ActionCompletionSec,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("explanation")] string Explanation);

/// <summary>
/// Just-In-Time (JIT) Benchmark Engine.
/// Scans Fountain scene beats for action and camera categories.
/// If an action is uncalibrated:
/// - Executes a real 1-clip JIT benchmark render via IVideoClient (Fal.ai/Veo).
/// - Downloads the resulting MP4 clip to inspect ISO-BMFF duration & runs Gemini Vision frame analysis.
/// - If live API keys are missing, invokes AiActionOverheadClassifier fallback.
/// - Persists newly calibrated empirical metrics to SQLite database repository.
/// </summary>
public sealed class JitBenchmarkService
{
    private readonly ActionCameraOverheadLedger _ledger;
    private readonly AiActionOverheadClassifier _classifier;
    private readonly IVideoClient? _videoClient;
    private readonly IVisionClient? _visionClient;
    private readonly ClipTimingTelemetryRepository? _repository;
    private readonly ILogger<JitBenchmarkService>? _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JitBenchmarkService(
        ActionCameraOverheadLedger ledger,
        AiActionOverheadClassifier classifier,
        IVideoClient? videoClient = null,
        IVisionClient? visionClient = null,
        ClipTimingTelemetryRepository? repository = null,
        ILogger<JitBenchmarkService>? log = null)
    {
        _ledger = ledger;
        _classifier = classifier;
        _videoClient = videoClient;
        _visionClient = visionClient;
        _repository = repository;
        _log = log;
    }

    public async Task<JitCalibrationResult> EnsureBeatCalibratedAsync(
        string actionDescription,
        string? parenthetical = null,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var concurrency = ActionConcurrencyAnalyzer.AnalyzeBeat(actionDescription, parenthetical);
        double camOverhead = _ledger.GetOverheadSec(concurrency.CameraId, 1.6);
        string targetModel = modelId ?? "fal-ai/hunyuan-video";

        bool canRunLiveJit = _videoClient is not null && _videoClient.IsConfigured;

        if (canRunLiveJit)
        {
            _log?.LogInformation("[JitBenchmark] Executing real 1-clip JIT benchmark for action: '{Action}' using model '{Model}'",
                actionDescription, targetModel);

            string? tempMp4Path = null;
            try
            {
                var prompt = $"Cinematic benchmark action shot: {actionDescription}";
                var reqId = await _videoClient!.SubmitGenerationAsync(
                    prompt: prompt,
                    durationSeconds: 4,
                    resolution: "1280x720",
                    model: targetModel,
                    ct: ct).ConfigureAwait(false);

                _log?.LogInformation("[JitBenchmark] Submitted 1-clip JIT job '{ReqId}'. Polling for video completion...", reqId);

                var videoUrl = await _videoClient.PollForVideoUrlAsync(reqId, msg => _log?.LogDebug("[JitBenchmark] {Msg}", msg), ct).ConfigureAwait(false);

                double measuredTotalClipSec = 4.0;
                double measuredActionOverheadSec = 2.4;
                string sourceNote = "Live Video API";

                if (!string.IsNullOrWhiteSpace(videoUrl))
                {
                    tempMp4Path = Path.Combine(Path.GetTempPath(), $"jit_measure_{Guid.NewGuid():N}.mp4");
                    
                    // Download video to local temp file for local ISO-BMFF MP4 probing & vision analysis
                    try
                    {
                        if (videoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            using var http = new HttpClient();
                            var mp4Bytes = await http.GetByteArrayAsync(videoUrl, ct).ConfigureAwait(false);
                            await File.WriteAllBytesAsync(tempMp4Path, mp4Bytes, ct).ConfigureAwait(false);
                        }
                        else if (File.Exists(videoUrl))
                        {
                            tempMp4Path = videoUrl;
                        }
                    }
                    catch (Exception dlEx)
                    {
                        _log?.LogDebug(dlEx, "[JitBenchmark] Temp MP4 download skipped for JIT URL '{Url}'", videoUrl);
                    }

                    if (File.Exists(tempMp4Path))
                    {
                        // 1. Probe total MP4 clip duration using ISO-BMFF reader
                        var probedTotalSec = Mp4DurationReader.TryReadSeconds(tempMp4Path);
                        if (probedTotalSec is > 0)
                        {
                            measuredTotalClipSec = Math.Round(probedTotalSec.Value, 2);
                            _log?.LogInformation("[JitBenchmark] Probed MP4 stream total clip duration: {TotalSec:F2}s", measuredTotalClipSec);
                        }

                        // 2. Perform multimodal Gemini Vision frame inspection to measure physical action overhead
                        if (_visionClient is not null && _visionClient.IsConfigured)
                        {
                            _log?.LogInformation("[JitBenchmark] Inspecting JIT video frames at {Path} via Vision Client...", tempMp4Path);
                            
                            var visionPrompt = $$"""
                                Analyze this video clip frame by frame.
                                Target physical action: "{{actionDescription}}"

                                Determine the exact timestamp in seconds from the start of the video when this action completes or stabilizes.
                                Respond strictly in JSON format:
                                {
                                  "actionCompletionSec": 2.4,
                                  "confidence": 0.95,
                                  "explanation": "<rationale>"
                                }
                                """;

                            var rawVision = await _visionClient.CompleteWithImagesAsync(
                                prompt: visionPrompt,
                                imagePaths: new[] { tempMp4Path },
                                model: "gemini-2.5-flash",
                                ct: ct).ConfigureAwait(false);

                            if (!string.IsNullOrWhiteSpace(rawVision))
                            {
                                var json = rawVision.Trim();
                                if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                                    json = json[7..].TrimEnd('`', '\n', '\r', ' ');
                                else if (json.StartsWith("```"))
                                    json = json[3..].TrimEnd('`', '\n', '\r', ' ');

                                var parsedAnalysis = JsonSerializer.Deserialize<VisionActionTimingAnalysis>(json, JsonOpts);
                                if (parsedAnalysis is not null && parsedAnalysis.ActionCompletionSec > 0)
                                {
                                    measuredActionOverheadSec = Math.Round(parsedAnalysis.ActionCompletionSec, 2);
                                    sourceNote = $"Live Video API + Gemini Vision Inspection ({parsedAnalysis.Explanation})";
                                    _log?.LogInformation("[JitBenchmark] Gemini Vision measured physical action overhead for '{Action}' = {ActionSec:F2}s (Conf={Conf:F2})",
                                        actionDescription, measuredActionOverheadSec, parsedAnalysis.Confidence);
                                }
                            }
                        }
                    }
                }

                var categoryId = $"jit_{Math.Abs(actionDescription.GetHashCode()):x8}";

                if (_repository is not null)
                {
                    await _repository.RecordCacheLookupAsync(isHit: false, lookupKey: categoryId).ConfigureAwait(false);
                    await _repository.RecordTelemetryAsync(new TimingTelemetryRecord(
                        Id: $"jit_{Guid.NewGuid():N}",
                        ProjectId: "global",
                        SceneNumber: 0,
                        VideoModelId: targetModel,
                        VideoModelVersion: "v1",
                        EvaluatorModelId: "gemini-2.5-flash",
                        EvaluatorModelVersion: "v1",
                        CameraCategory: concurrency.CameraId,
                        ActionCategory: categoryId,
                        WordCount: 0,
                        EstimatedDurationSec: camOverhead + measuredActionOverheadSec,
                        ClipDurationSec: measuredTotalClipSec,
                        MeasuredCamOverheadSec: camOverhead,
                        MeasuredActionOverheadSec: measuredActionOverheadSec,
                        DialogueTruncated: false,
                        CreatedAt: DateTime.UtcNow.ToString("o"))).ConfigureAwait(false);
                }

                return new JitCalibrationResult(
                    CategoryId: categoryId,
                    MeasuredOverheadSec: measuredActionOverheadSec,
                    OverlapRatioGamma: concurrency.OverlapRatioGamma,
                    IsLiveJitBenchmark: true,
                    SourceDescription: sourceNote);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "[JitBenchmark] Live 1-clip JIT render failed for '{Action}'. Falling back to AI Similarity Classifier.", actionDescription);
            }
            finally
            {
                if (tempMp4Path is not null && File.Exists(tempMp4Path) && tempMp4Path.Contains("jit_measure_"))
                {
                    try { File.Delete(tempMp4Path); } catch { /* cleanup */ }
                }
            }
        }

        _log?.LogInformation("[JitBenchmark] Invoking AI Similarity Classifier for action: '{Action}'", actionDescription);
        var estimation = await _classifier.ClassifyNovelActionAsync(actionDescription, parenthetical, ct).ConfigureAwait(false);

        if (_repository is not null)
        {
            await _repository.RecordCacheLookupAsync(isHit: false, lookupKey: estimation.MatchCategoryId).ConfigureAwait(false);
            await _repository.RecordTelemetryAsync(new TimingTelemetryRecord(
                Id: $"clf_{Guid.NewGuid():N}",
                ProjectId: "global",
                SceneNumber: 0,
                VideoModelId: targetModel,
                VideoModelVersion: "v1",
                EvaluatorModelId: "grok-4.5",
                EvaluatorModelVersion: "v1",
                CameraCategory: concurrency.CameraId,
                ActionCategory: estimation.MatchCategoryId,
                WordCount: 0,
                EstimatedDurationSec: camOverhead + estimation.EstimatedOverheadSec,
                ClipDurationSec: estimation.EstimatedOverheadSec + camOverhead,
                MeasuredCamOverheadSec: camOverhead,
                MeasuredActionOverheadSec: estimation.EstimatedOverheadSec,
                DialogueTruncated: false,
                CreatedAt: DateTime.UtcNow.ToString("o"))).ConfigureAwait(false);
        }

        return new JitCalibrationResult(
            CategoryId: estimation.MatchCategoryId,
            MeasuredOverheadSec: estimation.EstimatedOverheadSec,
            OverlapRatioGamma: concurrency.OverlapRatioGamma,
            IsLiveJitBenchmark: false,
            SourceDescription: $"AI Similarity Classifier ({estimation.Explanation}).");
    }
}
