using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Demo gallery under <c>{WorkspaceRoot}/_demos/{id}/</c> (meta.json + movie.mp4).
/// Public wall shows only <see cref="DemoStatuses.Public"/> entries after human review.
/// No ML / API moderation — publish → pending → admin approve/reject.
/// </summary>
public sealed class DemoCatalogService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public const long MaxUploadBytes = 512L * 1024 * 1024;
    public const long MinUploadBytes = 1024;
    /// <summary>Max demos a user may submit per rolling 24h window.</summary>
    public const int MaxPublishesPerUserPerDay = 2;
    /// <summary>Max simultaneous pending demos per user.</summary>
    public const int MaxPendingPerUser = 5;
    /// <summary>Max open report notes stored on one demo.</summary>
    public const int MaxReportNotes = 20;

    private readonly ProjectStore _projects;
    private readonly ILogger<DemoCatalogService> _log;
    private readonly object _lock = new();

    public DemoCatalogService(ProjectStore projects, ILogger<DemoCatalogService> log)
    {
        _projects = projects;
        _log = log;
    }

    public string DemosDir => Path.Combine(_projects.WorkspaceRoot, "_demos");

    public static class DemoStatuses
    {
        public const string Pending = "pending";
        public const string Public = "public";
        public const string Rejected = "rejected";
        public const string Removed = "removed";

        public static bool IsKnown(string? s) =>
            string.Equals(s, Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, Public, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, Rejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, Removed, StringComparison.OrdinalIgnoreCase);

        public static string Normalize(string? s) =>
            IsKnown(s) ? s!.Trim().ToLowerInvariant() : Public; // legacy metas without status → public
    }

    public sealed class DemoEntry
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? ProjectId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public long SizeBytes { get; set; }
        public string? ContentType { get; set; }
        /// <summary>pending | public | rejected | removed</summary>
        public string Status { get; set; } = DemoStatuses.Pending;
        public bool AcceptedGuidelines { get; set; }
        public int ReportCount { get; set; }
        public List<string> ReportNotes { get; set; } = new();
        public string? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }

        // YouTube publish metadata (declared by the submitter; used at actual upload time,
        // whether that's immediate auto-approval or a later admin approval).
        public bool MadeForKids { get; set; }
        public bool IsAiSyntheticContent { get; set; } = true;
        public string PrivacyStatus { get; set; } = "public";
        public List<string>? Tags { get; set; }

        /// <summary>none | uploading | done | failed. "done" means the video now lives on YouTube
        /// and <see cref="Id"/>'s local movie.mp4 has been deleted (server footprint goal).</summary>
        public string YoutubeUploadStatus { get; set; } = "none";
        public string? YoutubeId { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? YoutubeUploadError { get; set; }
    }

    public IReadOnlyList<DemoEntry> List(int take = 50, string? status = null)
    {
        take = Math.Clamp(take, 1, 200);
        lock (_lock)
        {
            return LoadAllUnlocked()
                .Where(e => status is null
                    || string.Equals(e.Status, status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .ToList();
        }
    }

    /// <summary>Public gallery: only approved public demos.</summary>
    public IReadOnlyList<DemoEntry> ListPublic(int take = 50) =>
        List(take, DemoStatuses.Public);

    public DemoEntry? TryGet(string id)
    {
        if (!IsValidId(id)) return null;
        lock (_lock)
            return ReadUnlocked(id);
    }

    public string? ResolveMoviePath(string id)
    {
        if (!IsValidId(id)) return null;
        var path = Path.Combine(DemosDir, id, "movie.mp4");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Whether anonymous/public viewers may stream this demo.
    /// Pending/rejected/removed are not world-readable.
    /// </summary>
    public bool IsPubliclyStreamable(DemoEntry? e) =>
        e is not null
        && string.Equals(e.Status, DemoStatuses.Public, StringComparison.OrdinalIgnoreCase);

    public bool CanUserViewVideo(DemoEntry e, string? userId, bool isAdmin)
    {
        if (IsPubliclyStreamable(e))
            return true;
        if (isAdmin)
            return true;
        if (!string.IsNullOrWhiteSpace(userId)
            && string.Equals(e.CreatedBy, userId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(e.Status, DemoStatuses.Removed, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Enforce publish rate / pending caps before accepting a new demo.</summary>
    public void EnsureUserMayPublish(string? userId, bool isAdmin)
    {
        if (isAdmin)
            return;
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Sign in required to publish a demo.");

        lock (_lock)
        {
            var mine = LoadAllUnlocked()
                .Where(e => string.Equals(e.CreatedBy, userId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pending = mine.Count(e =>
                string.Equals(e.Status, DemoStatuses.Pending, StringComparison.OrdinalIgnoreCase));
            if (pending >= MaxPendingPerUser)
            {
                throw new InvalidOperationException(
                    $"You already have {pending} demos waiting for review (max {MaxPendingPerUser}). " +
                    "Wait for admin approval before submitting more.");
            }

            var since = DateTimeOffset.UtcNow.AddHours(-24);
            var recent = mine.Count(e => e.CreatedAt >= since);
            if (recent >= MaxPublishesPerUserPerDay)
            {
                throw new InvalidOperationException(
                    $"Publish limit reached ({MaxPublishesPerUserPerDay} demos per 24 hours). Try again later.");
            }
        }
    }

    public DemoEntry PublishFromWip(
        string projectId,
        string title,
        string? description,
        string? createdBy,
        bool acceptedGuidelines,
        bool madeForKids = false,
        bool isAiSyntheticContent = true,
        string privacyStatus = "public",
        List<string>? tags = null)
    {
        var wip = _projects.ResolveWipMoviePath(projectId)
                  ?? throw new InvalidOperationException("WIP movie not found — build the cut first.");
        return PublishFromFile(
            wip, title, description, projectId, createdBy, acceptedGuidelines,
            madeForKids, isAiSyntheticContent, privacyStatus, tags);
    }

    public async Task<DemoEntry> PublishFromStreamAsync(
        Stream content,
        string title,
        string? description,
        string? projectId,
        string? createdBy,
        bool acceptedGuidelines,
        bool madeForKids = false,
        bool isAiSyntheticContent = true,
        string privacyStatus = "public",
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        if (content is null || !content.CanRead)
            throw new InvalidOperationException("Empty upload");
        if (!acceptedGuidelines)
            throw new InvalidOperationException("You must accept the gallery guidelines to publish.");

        var id = GenerateId();
        var dir = Path.Combine(DemosDir, id);
        Directory.CreateDirectory(dir);
        var moviePath = Path.Combine(dir, "movie.mp4");
        try
        {
            await using (var fs = new FileStream(
                             moviePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyWithSizeCapAsync(content, fs, MaxUploadBytes, ct).ConfigureAwait(false);
            }

            var fi = new FileInfo(moviePath);
            if (fi.Length < MinUploadBytes)
                throw new InvalidOperationException("Uploaded video is too small");
            if (!LooksLikeMp4(moviePath))
                throw new InvalidOperationException(
                    "Upload is not a valid MP4 (missing ftyp box). Only MP4 video is accepted.");

            var entry = NewPendingEntry(
                id, title, description, projectId, createdBy, fi.Length, acceptedGuidelines,
                madeForKids, isAiSyntheticContent, privacyStatus, tags);
            await WriteMetaAsync(dir, entry, ct).ConfigureAwait(false);

            _log.LogInformation(
                "Demo {Id} submitted pending review ({Bytes} bytes) project={Project} by={User}",
                id, entry.SizeBytes, projectId, createdBy);
            return entry;
        }
        catch
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { /* ignore */ }
            throw;
        }
    }

    public DemoEntry PublishFromFile(
        string sourceMp4Path,
        string title,
        string? description,
        string? projectId,
        string? createdBy,
        bool acceptedGuidelines,
        bool madeForKids = false,
        bool isAiSyntheticContent = true,
        string privacyStatus = "public",
        List<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(sourceMp4Path) || !File.Exists(sourceMp4Path))
            throw new InvalidOperationException("Source movie not found");
        if (!acceptedGuidelines)
            throw new InvalidOperationException("You must accept the gallery guidelines to publish.");

        var id = GenerateId();
        var dir = Path.Combine(DemosDir, id);
        Directory.CreateDirectory(dir);
        var moviePath = Path.Combine(dir, "movie.mp4");
        try
        {
            File.Copy(sourceMp4Path, moviePath, overwrite: false);
            var fi = new FileInfo(moviePath);
            if (fi.Length < MinUploadBytes)
                throw new InvalidOperationException("Movie file is too small");
            if (!LooksLikeMp4(moviePath))
                throw new InvalidOperationException("Source file is not a valid MP4.");

            var entry = NewPendingEntry(
                id, title, description, projectId, createdBy, fi.Length, acceptedGuidelines,
                madeForKids, isAiSyntheticContent, privacyStatus, tags);
            File.WriteAllText(
                Path.Combine(dir, "meta.json"),
                JsonSerializer.Serialize(entry, JsonOpts) + "\n");

            _log.LogInformation(
                "Demo {Id} submitted pending review from file ({Bytes} bytes) project={Project} by={User}",
                id, entry.SizeBytes, projectId, createdBy);
            return entry;
        }
        catch
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { /* ignore */ }
            throw;
        }
    }

    public DemoEntry? Report(string id, string? note, string? reporterUserId)
    {
        lock (_lock)
        {
            var entry = ReadUnlocked(id);
            if (entry is null)
                return null;
            if (!string.Equals(entry.Status, DemoStatuses.Public, StringComparison.OrdinalIgnoreCase))
                return entry; // ignore reports on non-public

            entry.ReportCount = Math.Max(0, entry.ReportCount) + 1;
            var line = $"{DateTimeOffset.UtcNow:o} by {reporterUserId ?? "anon"}: "
                       + (string.IsNullOrWhiteSpace(note) ? "(no note)" : note.Trim());
            entry.ReportNotes ??= new List<string>();
            entry.ReportNotes.Add(line);
            while (entry.ReportNotes.Count > MaxReportNotes)
                entry.ReportNotes.RemoveAt(0);

            // Auto-hide after enough community reports until admin re-reviews.
            if (entry.ReportCount >= 3
                && string.Equals(entry.Status, DemoStatuses.Public, StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = DemoStatuses.Pending;
                entry.ReviewNote = $"Auto-queued after {entry.ReportCount} reports";
                _log.LogWarning("Demo {Id} auto-pending after {N} reports", id, entry.ReportCount);
            }

            SaveUnlocked(entry);
            return entry;
        }
    }

    public DemoEntry? SetStatus(
        string id,
        string status,
        string? reviewerUserId,
        string? reviewNote)
    {
        if (!DemoStatuses.IsKnown(status))
            throw new InvalidOperationException($"Unknown status: {status}");

        lock (_lock)
        {
            var entry = ReadUnlocked(id);
            if (entry is null)
                return null;

            entry.Status = status.Trim().ToLowerInvariant();
            entry.ReviewedBy = reviewerUserId;
            entry.ReviewedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(reviewNote))
                entry.ReviewNote = reviewNote.Trim();

            SaveUnlocked(entry);
            _log.LogInformation(
                "Demo {Id} → {Status} by {Reviewer}",
                id, entry.Status, reviewerUserId);
            return entry;
        }
    }

    /// <summary>Hard-delete every demo created by <paramref name="userId"/> (admin cascade).</summary>
    public int HardDeleteAllByUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;
        List<string> ids;
        lock (_lock)
        {
            ids = LoadAllUnlocked()
                .Where(e => string.Equals(e.CreatedBy, userId, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Id)
                .ToList();
        }
        var n = 0;
        foreach (var id in ids)
        {
            if (Delete(id, requesterUserId: null, isAdmin: true))
                n++;
        }
        return n;
    }

    public bool Delete(string id, string? requesterUserId, bool isAdmin)
    {
        var entry = TryGet(id);
        if (entry is null) return false;
        if (!isAdmin &&
            !string.Equals(entry.CreatedBy, requesterUserId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Soft-delete for non-admin owner: mark removed so admin has audit trail optional.
        // Hard-delete for admin always; owner can hard-delete their own pending/rejected.
        if (isAdmin || !string.Equals(entry.Status, DemoStatuses.Public, StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                var dir = Path.Combine(DemosDir, id);
                if (!Directory.Exists(dir)) return false;
                try
                {
                    Directory.Delete(dir, recursive: true);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to delete demo {Id}", id);
                    return false;
                }
            }
        }

        // Public demos: owner request → removed (hidden), admin can hard-delete later.
        return SetStatus(id, DemoStatuses.Removed, requesterUserId, "Removed by publisher") is not null;
    }

    public static bool LooksLikeMp4(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 12)
                return false;
            Span<byte> header = stackalloc byte[12];
            var n = fs.Read(header);
            if (n < 12)
                return false;
            return header[4] == (byte)'f'
                   && header[5] == (byte)'t'
                   && header[6] == (byte)'y'
                   && header[7] == (byte)'p';
        }
        catch
        {
            return false;
        }
    }

    private DemoEntry NewPendingEntry(
        string id,
        string title,
        string? description,
        string? projectId,
        string? createdBy,
        long sizeBytes,
        bool acceptedGuidelines,
        bool madeForKids = false,
        bool isAiSyntheticContent = true,
        string privacyStatus = "public",
        List<string>? tags = null) =>
        new()
        {
            Id = id,
            Title = string.IsNullOrWhiteSpace(title) ? (projectId ?? "Demo") : title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            SizeBytes = sizeBytes,
            ContentType = "video/mp4",
            Status = DemoStatuses.Pending,
            AcceptedGuidelines = acceptedGuidelines,
            MadeForKids = madeForKids,
            IsAiSyntheticContent = isAiSyntheticContent,
            PrivacyStatus = privacyStatus is "public" or "unlisted" or "private" ? privacyStatus : "public",
            Tags = tags is { Count: > 0 } ? tags : null,
        };

    /// <summary>
    /// Record a YouTube upload attempt/result. On success, deletes the local movie.mp4 (server
    /// footprint goal) — the entry stays valid without it once <see cref="DemoEntry.YoutubeId"/> is set.
    /// </summary>
    public DemoEntry? SetYouTubeUploadStatus(
        string id,
        string status,
        string? youtubeId = null,
        string? youtubeUrl = null,
        string? error = null)
    {
        lock (_lock)
        {
            var entry = ReadUnlocked(id);
            if (entry is null)
                return null;

            entry.YoutubeUploadStatus = status;
            entry.YoutubeUploadError = error;
            if (!string.IsNullOrWhiteSpace(youtubeId))
                entry.YoutubeId = youtubeId;
            if (!string.IsNullOrWhiteSpace(youtubeUrl))
                entry.YoutubeUrl = youtubeUrl;

            SaveUnlocked(entry);

            if (string.Equals(status, "done", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var moviePath = Path.Combine(DemosDir, entry.Id, "movie.mp4");
                    if (File.Exists(moviePath))
                    {
                        File.Delete(moviePath);
                        _log.LogInformation(
                            "Demo {Id} moved to YouTube ({YoutubeId}); deleted local movie.mp4.",
                            entry.Id, youtubeId);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to delete local movie.mp4 for demo {Id} after YouTube upload.", entry.Id);
                }
            }

            return entry;
        }
    }

    private List<DemoEntry> LoadAllUnlocked()
    {
        if (!Directory.Exists(DemosDir))
            return new List<DemoEntry>();

        var list = new List<DemoEntry>();
        foreach (var dir in Directory.EnumerateDirectories(DemosDir))
        {
            try
            {
                var id = Path.GetFileName(dir);
                var entry = ReadUnlocked(id);
                if (entry is not null)
                    list.Add(entry);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Skip bad demo dir {Dir}", dir);
            }
        }
        return list;
    }

    private DemoEntry? ReadUnlocked(string id)
    {
        if (!IsValidId(id)) return null;
        var metaPath = Path.Combine(DemosDir, id, "meta.json");
        var moviePath = Path.Combine(DemosDir, id, "movie.mp4");
        if (!File.Exists(metaPath))
            return null;
        try
        {
            var entry = JsonSerializer.Deserialize<DemoEntry>(File.ReadAllText(metaPath), JsonOpts);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                return null;
            entry.Status = DemoStatuses.Normalize(
                string.IsNullOrWhiteSpace(entry.Status) ? null : entry.Status);
            var hasLocalMovie = File.Exists(moviePath);
            // Valid without a local file only once it has moved to YouTube; otherwise the movie
            // is genuinely missing (corrupt/partial write) and the entry should not surface.
            if (!hasLocalMovie && string.IsNullOrWhiteSpace(entry.YoutubeId))
                return null;
            if (hasLocalMovie && entry.SizeBytes <= 0)
            {
                try { entry.SizeBytes = new FileInfo(moviePath).Length; }
                catch { /* ignore */ }
            }
            entry.ReportNotes ??= new List<string>();
            return entry;
        }
        catch
        {
            return null;
        }
    }

    private void SaveUnlocked(DemoEntry entry)
    {
        var dir = Path.Combine(DemosDir, entry.Id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "meta.json"),
            JsonSerializer.Serialize(entry, JsonOpts) + "\n");
    }

    private static async Task WriteMetaAsync(string dir, DemoEntry entry, CancellationToken ct) =>
        await File.WriteAllTextAsync(
                Path.Combine(dir, "meta.json"),
                JsonSerializer.Serialize(entry, JsonOpts) + "\n",
                ct)
            .ConfigureAwait(false);

    private static async Task CopyWithSizeCapAsync(
        Stream source,
        Stream dest,
        long maxBytes,
        CancellationToken ct)
    {
        var buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            var n = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (n <= 0) break;
            total += n;
            if (total > maxBytes)
                throw new InvalidOperationException(
                    $"Upload exceeds size limit ({maxBytes:N0} bytes).");
            await dest.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
        }
    }

    private static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && id.Length is >= 8 and <= 40
        && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private static string GenerateId()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
