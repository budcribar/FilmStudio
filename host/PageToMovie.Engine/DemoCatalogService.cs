using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Public demo gallery: uploaded movies under <c>{WorkspaceRoot}/_demos/{id}/</c>.
/// Each demo has <c>meta.json</c> + <c>movie.mp4</c>.
/// </summary>
public sealed class DemoCatalogService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ProjectStore _projects;
    private readonly ILogger<DemoCatalogService> _log;
    private readonly object _lock = new();

    public DemoCatalogService(ProjectStore projects, ILogger<DemoCatalogService> log)
    {
        _projects = projects;
        _log = log;
    }

    public string DemosDir => Path.Combine(_projects.WorkspaceRoot, "_demos");

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
    }

    public IReadOnlyList<DemoEntry> List(int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        lock (_lock)
        {
            if (!Directory.Exists(DemosDir))
                return Array.Empty<DemoEntry>();

            var list = new List<DemoEntry>();
            foreach (var dir in Directory.EnumerateDirectories(DemosDir))
            {
                try
                {
                    var metaPath = Path.Combine(dir, "meta.json");
                    var moviePath = Path.Combine(dir, "movie.mp4");
                    if (!File.Exists(metaPath) || !File.Exists(moviePath))
                        continue;
                    var entry = JsonSerializer.Deserialize<DemoEntry>(File.ReadAllText(metaPath), JsonOpts);
                    if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
                        continue;
                    if (entry.SizeBytes <= 0)
                    {
                        try { entry.SizeBytes = new FileInfo(moviePath).Length; }
                        catch { /* ignore */ }
                    }
                    list.Add(entry);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Skip bad demo dir {Dir}", dir);
                }
            }

            return list
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .ToList();
        }
    }

    public DemoEntry? TryGet(string id)
    {
        if (!IsValidId(id)) return null;
        lock (_lock)
        {
            var metaPath = Path.Combine(DemosDir, id, "meta.json");
            if (!File.Exists(metaPath)) return null;
            try
            {
                return JsonSerializer.Deserialize<DemoEntry>(File.ReadAllText(metaPath), JsonOpts);
            }
            catch
            {
                return null;
            }
        }
    }

    public string? ResolveMoviePath(string id)
    {
        if (!IsValidId(id)) return null;
        var path = Path.Combine(DemosDir, id, "movie.mp4");
        return File.Exists(path) ? path : null;
    }

    /// <summary>Copy an existing on-disk WIP into a new demo entry.</summary>
    public DemoEntry PublishFromWip(string projectId, string title, string? description, string? createdBy)
    {
        var wip = _projects.ResolveWipMoviePath(projectId)
                  ?? throw new InvalidOperationException("WIP movie not found — build the cut first.");
        return PublishFromFile(wip, title, description, projectId, createdBy);
    }

    /// <summary>Store an uploaded stream as a new demo.</summary>
    public async Task<DemoEntry> PublishFromStreamAsync(
        Stream content,
        string title,
        string? description,
        string? projectId,
        string? createdBy,
        CancellationToken ct = default)
    {
        if (content is null || !content.CanRead)
            throw new InvalidOperationException("Empty upload");

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
                await content.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            var fi = new FileInfo(moviePath);
            if (fi.Length < 1024)
                throw new InvalidOperationException("Uploaded video is too small");

            var entry = new DemoEntry
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(title) ? (projectId ?? "Demo") : title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim(),
                CreatedBy = createdBy,
                CreatedAt = DateTimeOffset.UtcNow,
                SizeBytes = fi.Length,
                ContentType = "video/mp4",
            };
            await File.WriteAllTextAsync(
                    Path.Combine(dir, "meta.json"),
                    JsonSerializer.Serialize(entry, JsonOpts) + "\n",
                    ct)
                .ConfigureAwait(false);

            _log.LogInformation(
                "Demo {Id} published ({Bytes} bytes) project={Project} by={User}",
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
        string? createdBy)
    {
        if (string.IsNullOrWhiteSpace(sourceMp4Path) || !File.Exists(sourceMp4Path))
            throw new InvalidOperationException("Source movie not found");

        var id = GenerateId();
        var dir = Path.Combine(DemosDir, id);
        Directory.CreateDirectory(dir);
        var moviePath = Path.Combine(dir, "movie.mp4");
        try
        {
            File.Copy(sourceMp4Path, moviePath, overwrite: false);
            var fi = new FileInfo(moviePath);
            if (fi.Length < 1024)
                throw new InvalidOperationException("Movie file is too small");

            var entry = new DemoEntry
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(title) ? (projectId ?? "Demo") : title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim(),
                CreatedBy = createdBy,
                CreatedAt = DateTimeOffset.UtcNow,
                SizeBytes = fi.Length,
                ContentType = "video/mp4",
            };
            File.WriteAllText(
                Path.Combine(dir, "meta.json"),
                JsonSerializer.Serialize(entry, JsonOpts) + "\n");

            _log.LogInformation(
                "Demo {Id} published from file ({Bytes} bytes) project={Project} by={User}",
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

    public bool Delete(string id, string? requesterUserId, bool isAdmin)
    {
        var entry = TryGet(id);
        if (entry is null) return false;
        if (!isAdmin &&
            !string.Equals(entry.CreatedBy, requesterUserId, StringComparison.OrdinalIgnoreCase))
            return false;

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
