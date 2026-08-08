using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PageToMovie.Engine.Collaboration;

/// <summary>
/// Filesystem scene version snapshots under
/// {projectsRoot}/{projectId}/scene-versions/{sceneKey}/{versionId}/.
/// Each version has meta.json plus optional media/state files.
/// </summary>
public sealed class SceneVersionStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _projectsRoot;

    public SceneVersionStore(string projectsRoot)
    {
        _projectsRoot = projectsRoot ?? throw new ArgumentNullException(nameof(projectsRoot));
    }

    public string GetVersionsRoot(string projectId, string sceneKey)
    {
        var safeScene = Sanitize(sceneKey);
        return Path.Combine(_projectsRoot, projectId, "scene-versions", safeScene);
    }

    /// <summary>
    /// Snapshot current scene state + optional local media files.
    /// Returns the new version id (timestamp-hash).
    /// </summary>
    public async Task<SceneVersionInfo> SnapshotAsync(
        string projectId,
        string sceneKey,
        string? sceneStateJson,
        IReadOnlyDictionary<string, string>? localMediaPaths = null,
        string? note = null,
        string? createdBy = null,
        CancellationToken ct = default)
    {
        var versionId = MakeVersionId(sceneStateJson, localMediaPaths);
        var dir = Path.Combine(GetVersionsRoot(projectId, sceneKey), versionId);
        Directory.CreateDirectory(dir);

        var files = new List<string>();

        if (!string.IsNullOrWhiteSpace(sceneStateJson))
        {
            var statePath = Path.Combine(dir, "scene-state.json");
            await File.WriteAllTextAsync(statePath, sceneStateJson, ct);
            files.Add("scene-state.json");
        }

        if (localMediaPaths != null)
        {
            foreach (var (logicalName, sourcePath) in localMediaPaths)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    continue;
                var safeName = SanitizeFileName(logicalName);
                if (string.IsNullOrEmpty(safeName)) continue;
                var dest = Path.Combine(dir, safeName);
                File.Copy(sourcePath, dest, overwrite: true);
                files.Add(safeName);
            }
        }

        var info = new SceneVersionInfo
        {
            VersionId = versionId,
            SceneKey = sceneKey,
            CreatedUtc = DateTime.UtcNow,
            Note = note,
            CreatedBy = createdBy,
            Files = files
        };

        var metaPath = Path.Combine(dir, "meta.json");
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(info, JsonOpts), ct);
        return info;
    }

    public async Task<IReadOnlyList<SceneVersionInfo>> ListHistoryAsync(
        string projectId,
        string sceneKey,
        CancellationToken ct = default)
    {
        var root = GetVersionsRoot(projectId, sceneKey);
        if (!Directory.Exists(root))
            return Array.Empty<SceneVersionInfo>();

        var list = new List<SceneVersionInfo>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            ct.ThrowIfCancellationRequested();
            var metaPath = Path.Combine(dir, "meta.json");
            if (!File.Exists(metaPath)) continue;
            try
            {
                var json = await File.ReadAllTextAsync(metaPath, ct);
                var info = JsonSerializer.Deserialize<SceneVersionInfo>(json, JsonOpts);
                if (info != null && !string.IsNullOrWhiteSpace(info.VersionId))
                    list.Add(info);
            }
            catch
            {
                // skip corrupt version folders
            }
        }

        return list
            .OrderByDescending(v => v.CreatedUtc)
            .ToList();
    }

    public async Task<SceneVersionInfo?> GetAsync(
        string projectId,
        string sceneKey,
        string versionId,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(GetVersionsRoot(projectId, sceneKey), Sanitize(versionId));
        var metaPath = Path.Combine(dir, "meta.json");
        if (!File.Exists(metaPath)) return null;
        var json = await File.ReadAllTextAsync(metaPath, ct);
        return JsonSerializer.Deserialize<SceneVersionInfo>(json, JsonOpts);
    }

    /// <summary>
    /// Restore a version: returns scene-state JSON (if any) and copies media files
    /// into the provided target directory map (logical name → destination path).
    /// </summary>
    public async Task<SceneRestoreResult> RestoreAsync(
        string projectId,
        string sceneKey,
        string versionId,
        IReadOnlyDictionary<string, string>? mediaDestinations = null,
        CancellationToken ct = default)
    {
        var safeVersion = Sanitize(versionId);
        var dir = Path.Combine(GetVersionsRoot(projectId, sceneKey), safeVersion);
        if (!Directory.Exists(dir))
            return new SceneRestoreResult { Ok = false, Error = "Version not found." };

        var info = await GetAsync(projectId, sceneKey, safeVersion, ct);
        if (info == null)
            return new SceneRestoreResult { Ok = false, Error = "Version metadata missing." };

        string? stateJson = null;
        var statePath = Path.Combine(dir, "scene-state.json");
        if (File.Exists(statePath))
            stateJson = await File.ReadAllTextAsync(statePath, ct);

        var restoredFiles = new List<string>();
        if (mediaDestinations != null)
        {
            foreach (var (logicalName, destPath) in mediaDestinations)
            {
                ct.ThrowIfCancellationRequested();
                var safeName = SanitizeFileName(logicalName);
                var src = Path.Combine(dir, safeName);
                if (!File.Exists(src)) continue;
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(src, destPath, overwrite: true);
                restoredFiles.Add(logicalName);
            }
        }

        return new SceneRestoreResult
        {
            Ok = true,
            Version = info,
            SceneStateJson = stateJson,
            RestoredFiles = restoredFiles
        };
    }

    private static string MakeVersionId(string? stateJson, IReadOnlyDictionary<string, string>? media)
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        using var sha = SHA256.Create();
        var sb = new StringBuilder();
        sb.Append(stateJson ?? "");
        if (media != null)
        {
            foreach (var kv in media.OrderBy(k => k.Key))
            {
                sb.Append(kv.Key);
                if (File.Exists(kv.Value))
                {
                    var fi = new FileInfo(kv.Value);
                    sb.Append(fi.Length).Append(fi.LastWriteTimeUtc.Ticks);
                }
            }
        }
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())))[..8].ToLowerInvariant();
        return $"{ts}-{hash}";
    }

    private static string Sanitize(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "_";
        return PageToMovie.Core.Utils.FileNameSanitizer.SanitizeFileName(key.Trim());
    }

    private static string SanitizeFileName(string name)
    {
        var leaf = Path.GetFileName(name.Replace('\\', '/'));
        return Sanitize(leaf);
    }
}

public sealed class SceneVersionInfo
{
    public string VersionId { get; set; } = "";
    public string SceneKey { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string? Note { get; set; }
    public string? CreatedBy { get; set; }
    public List<string> Files { get; set; } = new();
}

public sealed class SceneRestoreResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public SceneVersionInfo? Version { get; set; }
    public string? SceneStateJson { get; set; }
    public List<string> RestoredFiles { get; set; } = new();
}
