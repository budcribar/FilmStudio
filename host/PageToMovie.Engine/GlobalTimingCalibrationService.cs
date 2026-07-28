using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Connects ClipTimingTelemetryRepository SQLite storage with ActionCameraOverheadLedger.
/// Continuously recalibrates in-memory overheads as new scene cuts are processed and verified.
/// </summary>
public sealed class GlobalTimingCalibrationService
{
    private readonly ClipTimingTelemetryRepository _repo;
    private readonly ActionCameraOverheadLedger _ledger;
    private readonly ILogger<GlobalTimingCalibrationService>? _log;

    public GlobalTimingCalibrationService(
        ClipTimingTelemetryRepository repo,
        ActionCameraOverheadLedger ledger,
        ILogger<GlobalTimingCalibrationService>? log = null)
    {
        _repo = repo;
        _ledger = ledger;
        _log = log;
    }

    public async Task<TimingCacheStats> GetStatsAsync()
    {
        return await _repo.GetCacheTelemetryStatsAsync().ConfigureAwait(false);
    }

    public async Task<List<TimingTrendPoint>> GetTrendAsync(int maxPoints = 30)
    {
        return await _repo.GetTrendHistoryAsync(maxPoints).ConfigureAwait(false);
    }

    public async Task RecordCutTelemetryAsync(
        string projectId,
        int sceneNumber,
        string videoModelId,
        string videoModelVersion,
        string evaluatorModelId,
        string evaluatorModelVersion,
        string? cameraCategory,
        string? actionCategory,
        int wordCount,
        double clipDurationSec,
        double measuredCamOverheadSec,
        double measuredActionOverheadSec,
        bool dialogueTruncated)
    {
        var record = new TimingTelemetryRecord(
            Id: Guid.NewGuid().ToString("N"),
            ProjectId: projectId,
            SceneNumber: sceneNumber,
            VideoModelId: videoModelId,
            VideoModelVersion: videoModelVersion,
            EvaluatorModelId: evaluatorModelId,
            EvaluatorModelVersion: evaluatorModelVersion,
            CameraCategory: cameraCategory ?? "",
            ActionCategory: actionCategory ?? "",
            WordCount: wordCount,
            ClipDurationSec: clipDurationSec,
            MeasuredCamOverheadSec: measuredCamOverheadSec,
            MeasuredActionOverheadSec: measuredActionOverheadSec,
            DialogueTruncated: dialogueTruncated,
            CreatedAt: DateTime.UtcNow.ToString("o"));

        await _repo.RecordTelemetryAsync(record).ConfigureAwait(false);
        _log?.LogInformation("[GlobalTimingCalibration] Logged timing telemetry for scene {Scene} ({VideoModel} / {EvaluatorModel})", sceneNumber, videoModelId, evaluatorModelId);
    }
}
