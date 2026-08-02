using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// One row destined for the <c>generation_errors</c> SQLite table — partial-coverage / structural
/// gate / transient-retry events. A different, more general concept than
/// <see cref="ApiCallTelemetry"/> (which logs every call, success or failure); this only logs the
/// "something the pipeline treats as a quality gap today, silently" cases.
/// </summary>
public sealed class GenerationErrorRecord
{
    public DateTimeOffset? Ts { get; set; }
    public string? UserId { get; set; }
    public string? ProjectId { get; set; }
    public string? JobId { get; set; }
    public int? Scene { get; set; }
    public int? Clip { get; set; }
    /// <summary>Call-site tag, e.g. "beat_pacing_classifier", "grok_chat_completion", "book_to_fountain_chunk".</summary>
    public required string Stage { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    /// <summary>http_error | exception | partial_coverage | empty_response | structural_gate_failure</summary>
    public required string ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public int? HttpStatus { get; set; }
    public int? RequestedCount { get; set; }
    public int? ReturnedCount { get; set; }
    public IReadOnlyList<string>? MissingIds { get; set; }
    public int Attempt { get; set; } = 1;
    /// <summary>True when a later attempt succeeded (or fully recovered coverage) after this failure was seen.</summary>
    public bool Resolved { get; set; }
    public string? RequestSummary { get; set; }
    public string? ResponseSummary { get; set; }
}

/// <summary>
/// Thin wrapper around <see cref="UserDatabaseService.InsertGenerationErrorAsync"/>. Never throws
/// to the caller — same contract as <see cref="ProjectTelemetryService.LogApiCallAsync"/>: wrap in
/// try/catch, log a warning on failure, swallow it. Generation-error logging must never break the
/// real AI call it's observing.
/// </summary>
public sealed class GenerationErrorLogger
{
    private readonly UserDatabaseService _db;
    private readonly ProjectTelemetryService? _telemetry;
    private readonly ILogger<GenerationErrorLogger> _log;

    public GenerationErrorLogger(
        UserDatabaseService db,
        ILogger<GenerationErrorLogger> log,
        ProjectTelemetryService? telemetry = null)
    {
        _db = db;
        _log = log;
        _telemetry = telemetry;
    }

    public async Task LogAsync(GenerationErrorRecord rec, CancellationToken ct = default)
    {
        if (rec is null) return;
        try
        {
            rec.Ts ??= DateTimeOffset.UtcNow;
            rec.ProjectId ??= _telemetry?.CurrentProjectId;
            rec.UserId ??= UserApiCallScope.UserId;
            await _db.InsertGenerationErrorAsync(rec, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "generation_errors log failed (stage={Stage}, type={ErrorType})", rec.Stage, rec.ErrorType);
        }
    }

    /// <summary>
    /// Convenience wrapper for the 7 per-scene batched classifiers: logs a
    /// <c>partial_coverage</c>/<c>empty_response</c> row only when a retry was actually triggered
    /// (i.e. the first attempt didn't already fully cover the request) — the success path (full
    /// coverage on attempt 1) never logs anything, matching today's behavior exactly.
    /// </summary>
    public Task LogCoverageResultAsync<T>(
        string stage,
        string? model,
        string? provider,
        int? scene,
        IReadOnlyList<string> requestedIds,
        AiRetryPolicy.CoverageRetryResult<T> result,
        CancellationToken ct = default)
    {
        if (result.Attempts <= 1 && result.FullyCovered)
            return Task.CompletedTask; // no gap ever observed — nothing to log

        var missingPreview = string.Join(",", result.Missing.Take(20));
        var message = $"{result.ReturnedCount}/{requestedIds.Count} ids covered after {result.Attempts} attempt(s)" +
                      (result.Missing.Count > 0 ? $"; missing: {missingPreview}" : "") +
                      (string.IsNullOrWhiteSpace(result.LastError) ? "" : $"; last error: {result.LastError}");

        return LogAsync(new GenerationErrorRecord
        {
            Stage = stage,
            Provider = provider,
            Model = model,
            Scene = scene,
            ErrorType = result.ReturnedCount == 0 ? "empty_response" : "partial_coverage",
            ErrorMessage = message,
            RequestedCount = requestedIds.Count,
            ReturnedCount = result.ReturnedCount,
            MissingIds = result.Missing,
            Attempt = result.Attempts,
            Resolved = result.FullyCovered,
            RequestSummary = $"requested_ids=[{string.Join(",", requestedIds.Take(30))}]",
            ResponseSummary = result.LastRawResponse,
        }, ct);
    }
}
