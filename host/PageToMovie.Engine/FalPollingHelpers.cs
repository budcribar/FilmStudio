using System.Net;

namespace PageToMovie.Engine;

/// <summary>
/// Shared Fal.ai polling bits reused by the Fal video and lip-sync clients, whose status-poll
/// loops otherwise diverge (one throws on error, the other returns null).
/// </summary>
internal static class FalPollingHelpers
{
    private const string RateLimitProgressMessage = "Fal.ai rate limited (HTTP 429) — retrying in 5s…";

    /// <summary>
    /// Handles Fal's 429 rate-limit response during a status poll: when the response is a 429,
    /// reports progress and waits 5 seconds so the caller can <c>continue</c> its loop. Returns
    /// true iff the response was a 429 (i.e. the caller should retry).
    /// </summary>
    public static async Task<bool> HandleRateLimitAsync(
        HttpResponseMessage resp, Action<string>? onProgress, CancellationToken ct)
    {
        if (resp.StatusCode != HttpStatusCode.TooManyRequests) return false;
        onProgress?.Invoke(RateLimitProgressMessage);
        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        return true;
    }
}
