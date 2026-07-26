using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine
{
    /// <summary>
    /// Background hosted service that prunes server-cached MP4/media binaries older than 48 hours
    /// or when disk usage threshold is exceeded, maintaining Railway container disk footprint &lt; 100 MB.
    /// </summary>
    public class ServerMediaPruningService : BackgroundService
    {
        private readonly ILogger<ServerMediaPruningService> _logger;
        private readonly string _projectsRoot;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _maxFileAge;
        private readonly double _maxDiskUsagePercent;

        public ServerMediaPruningService(
            ILogger<ServerMediaPruningService> logger,
            string? projectsRoot = null,
            TimeSpan? checkInterval = null,
            TimeSpan? maxFileAge = null,
            double maxDiskUsagePercent = 80.0)
        {
            _logger = logger;
            _projectsRoot = projectsRoot ?? Path.Combine(Directory.GetCurrentDirectory(), "projects");
            _checkInterval = checkInterval ?? TimeSpan.FromHours(1);
            _maxFileAge = maxFileAge ?? TimeSpan.FromHours(48);
            _maxDiskUsagePercent = maxDiskUsagePercent;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger?.LogInformation("ServerMediaPruningService started. Checking every {Interval}, max age {MaxAge}.", _checkInterval, _maxFileAge);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    PerformPruning(_projectsRoot, _maxFileAge, _maxDiskUsagePercent);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error occurred during server media pruning execution.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        /// <summary>
        /// Executes pruning pass over projects and demos media directories.
        /// </summary>
        public int PerformPruning(string rootPath, TimeSpan maxAge, double maxDiskPercent)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return 0;
            }

            int deletedCount = 0;
            DateTime cutoff = DateTime.UtcNow - maxAge;

            string[] mediaExtensions = new[] { ".mp4", ".webm", ".mov", ".wav", ".avi" };

            // 1. Prune files older than maxAge in projects and demos directories
            var allDirectories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .Concat(new[] { rootPath });

            foreach (var dir in allDirectories)
            {
                try
                {
                    var mediaFiles = Directory.GetFiles(dir)
                        .Where(f => mediaExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    foreach (var file in mediaFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoff)
                        {
                            try
                            {
                                fileInfo.Delete();
                                deletedCount++;
                                _logger?.LogInformation("Pruned old server media file: {FilePath} (Age: {Age}h)", file, (DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalHours);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Failed to delete old media file {FilePath}", file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to inspect directory {Dir} during pruning.", dir);
                }
            }

            // 2. Check disk usage threshold if drive info is accessible
            try
            {
                var pathRoot = Path.GetPathRoot(Path.GetFullPath(rootPath));
                if (string.IsNullOrWhiteSpace(pathRoot))
                    return deletedCount;

                var drive = new DriveInfo(pathRoot);
                if (drive.IsReady && drive.TotalSize > 0)
                {
                    double usedPercent = ((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100.0;
                    if (usedPercent > maxDiskPercent)
                    {
                        _logger?.LogWarning("Disk usage ({UsedPercent:F1}%) exceeds max threshold ({MaxPercent:F1}%). Executing emergency media prune.", usedPercent, maxDiskPercent);
                        
                        // Delete remaining media files ordered by oldest first until disk usage is below threshold
                        var remainingMedia = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => mediaExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .Select(f => new FileInfo(f))
                            .OrderBy(fi => fi.LastWriteTimeUtc)
                            .ToList();

                        foreach (var fi in remainingMedia)
                        {
                            try
                            {
                                fi.Delete();
                                deletedCount++;
                                double currentUsed = ((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100.0;
                                if (currentUsed <= maxDiskPercent)
                                {
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "DriveInfo inspection omitted during pruning.");
            }

            return deletedCount;
        }
    }
}
