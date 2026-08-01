using System.Linq;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Pulls the connected channel's uploads into the demo catalog so /demo lists YouTube as SoT.
/// </summary>
public sealed class YouTubeChannelGallerySync
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
    private readonly YouTubeAuthService _youTube;
    private readonly DemoCatalogService _demos;
    private readonly ILogger<YouTubeChannelGallerySync> _log;
    private readonly object _gate = new();
    private DateTimeOffset _lastAttemptUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSuccessUtc = DateTimeOffset.MinValue;
    private string? _lastError;

    public YouTubeChannelGallerySync(
        YouTubeAuthService youTube,
        DemoCatalogService demos,
        ILogger<YouTubeChannelGallerySync> log)
    {
        _youTube = youTube;
        _demos = demos;
        _log = log;
    }

    public DateTimeOffset? LastSuccessUtc
    {
        get { lock (_gate) return _lastSuccessUtc == DateTimeOffset.MinValue ? null : _lastSuccessUtc; }
    }

    public string? LastError
    {
        get { lock (_gate) return _lastError; }
    }

    /// <summary>
    /// Sync channel → catalog. When <paramref name="force"/> is false, skips if last attempt was recent.
    /// No-op when OAuth is not connected.
    /// </summary>
    public async Task<(int Added, int Updated, int Total, bool Skipped)> EnsureSyncedAsync(
        bool force = false,
        string? createdBy = "youtube-channel",
        int maxVideos = 50,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!force && DateTimeOffset.UtcNow - _lastAttemptUtc < MinInterval)
                return (0, 0, 0, true);
            _lastAttemptUtc = DateTimeOffset.UtcNow;
        }

        if (!_youTube.IsConfigured || !await _youTube.IsConnectedAsync(ct).ConfigureAwait(false))
        {
            lock (_gate) _lastError = "YouTube not connected";
            return (0, 0, 0, true);
        }

        try
        {
            var uploads = await _youTube.ListChannelUploadsAsync(maxVideos, ct).ConfigureAwait(false);
            if (uploads.Count == 0)
            {
                // Prior bug: hide-on-empty wiped the wall. Restore channel-hidden entries.
                var restored = _demos.RestoreChannelHiddenDemos();
                lock (_gate)
                    _lastError = restored > 0
                        ? $"Channel returned 0 videos; restored {restored} previously hidden demo(s)"
                        : "Channel returned 0 videos — left gallery unchanged (no wipe)";
                _log.LogWarning(
                    "YouTube channel sync returned 0 videos; restored {Restored} channel-hidden demos",
                    restored);
                return (0, 0, restored, false);
            }

            var (added, updated, total) = _demos.SyncFromChannelUploads(uploads, createdBy);
            // Only prune after a non-empty successful list so a bad OAuth session cannot empty the wall.
            var hidden = _demos.HideDemosNotOnChannel(uploads.Select(u => u.VideoId).ToList());
            lock (_gate)
            {
                _lastSuccessUtc = DateTimeOffset.UtcNow;
                _lastError = null;
            }
            _log.LogInformation(
                "YouTube channel sync: {Total} videos ({Added} new, {Updated} updated, {Hidden} not on channel)",
                total, added, updated, hidden);
            return (added, updated, total, false);
        }
        catch (Exception ex)
        {
            lock (_gate) _lastError = ex.Message;
            _log.LogWarning(ex, "YouTube channel gallery sync failed");
            if (force) throw;
            return (0, 0, 0, false);
        }
    }
}
