using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PageToMovie.Engine;

/// <summary>
/// Shared retry/coverage helpers for AI call sites. Two independent concerns:
/// <list type="bullet">
/// <item><b>Coverage retry</b> — a batched classifier call asked for N ids and a response
/// covering fewer than N is a silent quality gap today (see <see cref="CheckCoverage"/> and
/// <see cref="RunWithCoverageRetryAsync{T}"/>).</item>
/// <item><b>Transient HTTP retry</b> — chat clients currently throw immediately on 429/5xx or
/// network/timeout failures with no retry at all (see <see cref="ExecuteWithTransientRetryAsync{T}"/>).</item>
/// </list>
/// Backoff reuses <see cref="ClassifierJsonParser.BackoffAsync"/>'s quadratic shape — the one
/// retry primitive that already existed in this codebase — rather than introducing a new pattern
/// or an external retry-framework dependency.
/// </summary>
public static class AiRetryPolicy
{
    /// <summary>Default attempt cap for batched classifier coverage retries (small — same call, same instruction).</summary>
    public const int DefaultCoverageMaxAttempts = 3;

    /// <summary>Default backoff base (ms) for coverage retries — matches <c>SilentBeatClassifyBackoffBaseMs</c>'s default.</summary>
    public const int DefaultCoverageBackoffMs = 400;

    /// <summary>Default attempt cap for chat-client transient HTTP/network retries.</summary>
    public const int DefaultTransientMaxAttempts = 3;

    /// <summary>Default backoff base (ms) for chat-client transient retries.</summary>
    public const int DefaultTransientBackoffMs = 500;

    // ── Coverage checking ───────────────────────────────────────────────────

    /// <summary>
    /// Pure comparison of requested vs. returned ids (case-insensitive). Used to detect a
    /// batched classifier response that silently covers fewer ids than were asked for.
    /// </summary>
    public static (IReadOnlyList<string> Missing, bool FullyCovered) CheckCoverage(
        IEnumerable<string>? requestedIds,
        IEnumerable<string>? returnedIds)
    {
        var requested = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in requestedIds ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (seen.Add(id)) requested.Add(id);
        }

        var returned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in returnedIds ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(id)) returned.Add(id);
        }

        var missing = requested.Where(id => !returned.Contains(id)).ToList();
        return (missing, missing.Count == 0);
    }

    /// <summary>Result of a batched-classify-with-coverage-retry run.</summary>
    public sealed class CoverageRetryResult<T>
    {
        /// <summary>Merged id→value map across all attempts (null only if nothing ever parsed).</summary>
        public Dictionary<string, T>? Result { get; init; }
        public IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();
        public bool FullyCovered { get; init; }
        /// <summary>How many attempts actually ran (1 = no retry needed/possible).</summary>
        public int Attempts { get; init; }
        /// <summary>How many of the originally requested ids ended up covered.</summary>
        public int ReturnedCount { get; init; }
        public string? LastRawResponse { get; init; }
        public string? LastError { get; init; }
    }

    /// <summary>
    /// Re-issues the SAME classify call (same instruction — no payload filtering) up to
    /// <paramref name="maxAttempts"/> times while any requested id remains uncovered, merging
    /// newly-covered ids into the running result each time. Mirrors
    /// <c>OnScreenCastClassifier.ClassifyStage1Async</c>'s retry-until-covered shape, generalized
    /// for single-call-per-scene classifiers. Never throws for ordinary AI-call failures (a bad
    /// attempt is recorded in <see cref="CoverageRetryResult{T}.LastError"/> and the loop moves on) —
    /// only <see cref="OperationCanceledException"/> propagates.
    /// </summary>
    public static async Task<CoverageRetryResult<T>> RunWithCoverageRetryAsync<T>(
        IReadOnlyList<string> requestedIds,
        Func<Task<string>> callChat,
        Func<string, Dictionary<string, T>?> parseResponse,
        int maxAttempts,
        int backoffBaseMs,
        CancellationToken ct = default)
    {
        maxAttempts = Math.Max(1, maxAttempts);
        var merged = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        string? lastRaw = null;
        string? lastError = null;
        var attemptsUsed = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            attemptsUsed = attempt;
            try
            {
                var raw = await callChat().ConfigureAwait(false);
                lastRaw = raw;
                var parsed = parseResponse(raw);
                if (parsed is not null)
                {
                    foreach (var kv in parsed)
                        merged[kv.Key] = kv.Value;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            var (missing, fullyCovered) = CheckCoverage(requestedIds, merged.Keys);
            if (fullyCovered) break;
            if (attempt < maxAttempts)
                await ClassifierJsonParser.BackoffAsync(attempt, backoffBaseMs, ct).ConfigureAwait(false);
        }

        var (finalMissing, finalCovered) = CheckCoverage(requestedIds, merged.Keys);
        return new CoverageRetryResult<T>
        {
            Result = merged.Count > 0 ? merged : null,
            Missing = finalMissing,
            FullyCovered = finalCovered,
            Attempts = attemptsUsed,
            ReturnedCount = requestedIds.Count - finalMissing.Count,
            LastRawResponse = lastRaw,
            LastError = lastError,
        };
    }

    // ── Transient HTTP / network retry ──────────────────────────────────────

    /// <summary>
    /// 429/500/502/503/504 — worth retrying. 400/401/403/404 are client errors; retrying never
    /// helps. Deliberately NOT the whole 500-599 range: e.g. 501 (Not Implemented) and 505 (HTTP
    /// Version Not Supported) are permanent conditions a retry can't fix.
    /// </summary>
    public static bool IsTransientHttpStatus(int status) =>
        status is 429 or 500 or 502 or 503 or 504;

    /// <summary>
    /// True for network/timeout failures worth retrying. Never true for
    /// <see cref="OperationCanceledException"/> — that's either a real caller cancellation or a
    /// timeout surfaced as cancellation; either way retrying blindly here would ignore the
    /// caller's cancellation token, so callers that want timeout-as-transient should map it to
    /// <see cref="TimeoutException"/> before this check, or rely on <see cref="HttpRequestException"/>
    /// (HttpClient's own timeout path).
    /// </summary>
    public static bool IsTransientException(Exception ex) => ex switch
    {
        OperationCanceledException => false,
        HttpRequestException => true,
        TimeoutException => true,
        SocketException => true,
        IOException => true,
        _ => false,
    };

    /// <summary>
    /// Retries <paramref name="attempt"/> up to <paramref name="maxAttempts"/> times while
    /// <paramref name="isTransient"/> returns true for the thrown exception, with quadratic
    /// backoff between attempts. The final attempt's exception always propagates unmodified
    /// (never wrapped) if it also fails. <paramref name="onRetry"/> (if given) runs once per
    /// failed-but-retried attempt, before the backoff delay — callers use it to log the attempt.
    /// </summary>
    public static async Task<T> ExecuteWithTransientRetryAsync<T>(
        Func<int, Task<T>> attempt,
        Func<Exception, bool> isTransient,
        int maxAttempts,
        int backoffBaseMs = DefaultTransientBackoffMs,
        Func<int, Exception, Task>? onRetry = null,
        CancellationToken ct = default)
    {
        maxAttempts = Math.Max(1, maxAttempts);
        for (var i = 1; i <= maxAttempts; i++)
        {
            try
            {
                return await attempt(i).ConfigureAwait(false);
            }
            catch (Exception ex) when (i < maxAttempts && isTransient(ex))
            {
                if (onRetry is not null)
                    await onRetry(i, ex).ConfigureAwait(false);
                await ClassifierJsonParser.BackoffAsync(i, backoffBaseMs, ct).ConfigureAwait(false);
            }
        }

        // Unreachable in practice: for i == maxAttempts the exception filter above is false
        // (i < maxAttempts fails), so the original exception propagates out of the try/await
        // above instead of reaching here.
        throw new InvalidOperationException("ExecuteWithTransientRetryAsync exhausted attempts unexpectedly.");
    }
}

/// <summary>
/// Chat-client HTTP failure carrying the response status code so callers can classify it as
/// transient (429/5xx) vs. permanent (4xx) without parsing the message text. Deliberately an
/// <see cref="InvalidOperationException"/> subtype so existing <c>ex is not InvalidOperationException</c>
/// telemetry-dedup catches in <c>GrokChatClient</c>/<c>AnthropicChatClient</c>/<c>GeminiChatClient</c>
/// keep working unchanged.
/// </summary>
public sealed class ChatHttpStatusException : InvalidOperationException
{
    public int StatusCode { get; }

    public ChatHttpStatusException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
