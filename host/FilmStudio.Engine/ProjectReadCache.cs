using System.Collections.Concurrent;
using System.Text.Json;
using FilmStudio.Core.Models;

namespace FilmStudio.Engine;

/// <summary>
/// Hot-path read caches for multi-user browse: project list, blueprint file bytes, asset dir indexes.
/// Entries are mtime/size validated (or short TTL for the project list) and explicitly invalidated on writes.
/// </summary>
public sealed class ProjectReadCache
{
    private static readonly TimeSpan ProjectsListTtl = TimeSpan.FromSeconds(10);

    private readonly object _projectsGate = new();
    private IReadOnlyList<ProjectInfo>? _projects;
    private DateTimeOffset _projectsAt;

    private readonly ConcurrentDictionary<string, BlueprintEntry> _blueprints =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _blueprintPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DirEntry> _dirs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _buildLocks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When false, every call is a full rebuild (A/B soaks).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cached project list with short TTL (new folders appear within ~10s).</summary>
    public IReadOnlyList<ProjectInfo> GetOrBuildProjects(Func<IReadOnlyList<ProjectInfo>> build)
    {
        if (!Enabled)
            return build() ?? Array.Empty<ProjectInfo>();

        lock (_projectsGate)
        {
            if (_projects is not null && DateTimeOffset.UtcNow - _projectsAt <= ProjectsListTtl)
                return CloneProjects(_projects);
        }

        var built = build() ?? Array.Empty<ProjectInfo>();
        var snap = CloneProjects(built);
        lock (_projectsGate)
        {
            _projects = snap;
            _projectsAt = DateTimeOffset.UtcNow;
            return CloneProjects(snap);
        }
    }

    public void InvalidateProjectsList()
    {
        lock (_projectsGate)
        {
            _projects = null;
            _projectsAt = default;
        }
    }

    /// <summary>
    /// Resolve blueprint path once per project until invalidated (config / blueprint rename).
    /// </summary>
    public string? GetOrFindBlueprintPath(string projectId, Func<string?> find)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(projectId))
            return find();

        var key = projectId.Trim();
        if (_blueprintPaths.TryGetValue(key, out var hit))
            return hit;

        var path = find();
        _blueprintPaths[key] = path;
        return path;
    }

    /// <summary>
    /// Shared parsed blueprint, reloaded only when file mtime/size changes.
    /// <para>
    /// <b>Do not dispose</b> the returned document — it is owned by the cache and is safe for concurrent reads.
    /// Use <see cref="CloneBlueprintDocument"/> when a caller needs an owned <see cref="JsonDocument"/>.
    /// </para>
    /// </summary>
    public JsonDocument? GetOrLoadBlueprintDocument(string? absolutePath)
    {
        var entry = GetOrLoadBlueprintEntry(absolutePath);
        return entry?.Doc;
    }

    /// <summary>UTF-8 bytes (same validity as the shared document).</summary>
    public byte[]? GetOrLoadBlueprintUtf8(string? absolutePath) =>
        GetOrLoadBlueprintEntry(absolutePath)?.Utf8;

    /// <summary>Owned copy for APIs that historically used <c>using var bp = LoadBlueprint(...)</c>.</summary>
    public static JsonDocument? CloneBlueprintDocument(JsonDocument? shared)
    {
        if (shared is null) return null;
        return JsonDocument.Parse(shared.RootElement.GetRawText());
    }

    private BlueprintEntry? GetOrLoadBlueprintEntry(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        // Disabled: no shared entry (caller should use owned parse via ProjectStore uncached path)
        if (!Enabled)
            return null;

        FileInfo fi;
        try { fi = new FileInfo(absolutePath); }
        catch { return null; }

        var key = fi.FullName;
        if (_blueprints.TryGetValue(key, out var hit) &&
            hit.Ticks == fi.LastWriteTimeUtc.Ticks &&
            hit.Length == fi.Length)
            return hit;

        var gate = _buildLocks.GetOrAdd("bp:" + key, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try
        {
            try { fi.Refresh(); }
            catch { return null; }

            if (_blueprints.TryGetValue(key, out hit) &&
                hit.Ticks == fi.LastWriteTimeUtc.Ticks &&
                hit.Length == fi.Length)
                return hit;

            var utf8 = File.ReadAllBytes(absolutePath);
            var doc = JsonDocument.Parse(utf8);
            var entry = new BlueprintEntry
            {
                Ticks = fi.LastWriteTimeUtc.Ticks,
                Length = fi.Length,
                Utf8 = utf8,
                Doc = doc,
            };

            if (_blueprints.TryRemove(key, out var old))
            {
                try { old.Doc.Dispose(); } catch { /* ignore */ }
            }

            _blueprints[key] = entry;
            return entry;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>File name → length map for a media directory (mtime-validated + single-flight).</summary>
    public Dictionary<string, long> GetOrIndexDir(string dir, Func<string, Dictionary<string, long>> index)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        if (!Enabled)
            return index(dir) ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        long dirTicks = 0;
        var exists = Directory.Exists(dir);
        if (exists)
        {
            try { dirTicks = Directory.GetLastWriteTimeUtc(dir).Ticks; }
            catch { /* ignore */ }
        }

        var key = Path.GetFullPath(dir);
        if (_dirs.TryGetValue(key, out var hit) && hit.Exists == exists && hit.DirTicks == dirTicks)
            return CloneDir(hit.Files);

        var gate = _buildLocks.GetOrAdd("dir:" + key, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try
        {
            exists = Directory.Exists(dir);
            if (exists)
            {
                try { dirTicks = Directory.GetLastWriteTimeUtc(dir).Ticks; }
                catch { dirTicks = 0; }
            }
            else
            {
                dirTicks = 0;
            }

            if (_dirs.TryGetValue(key, out hit) && hit.Exists == exists && hit.DirTicks == dirTicks)
                return CloneDir(hit.Files);

            var map = index(dir) ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var stored = CloneDir(map);
            _dirs[key] = new DirEntry
            {
                Exists = exists,
                DirTicks = dirTicks,
                Files = stored,
            };
            return CloneDir(stored);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Drop blueprint path/bytes and asset dir indexes for a project (or everything if null).</summary>
    public void InvalidateProject(string? projectId, string? projectDir = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            InvalidateAll();
            return;
        }

        _blueprintPaths.TryRemove(projectId.Trim(), out _);

        if (!string.IsNullOrWhiteSpace(projectDir))
        {
            try
            {
                var root = Path.GetFullPath(projectDir);
                foreach (var key in _blueprints.Keys.ToArray())
                {
                    if (key.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                        _blueprints.TryRemove(key, out var old))
                    {
                        try { old.Doc.Dispose(); } catch { /* ignore */ }
                    }
                }

                foreach (var key in _dirs.Keys.ToArray())
                {
                    if (key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        _dirs.TryRemove(key, out _);
                }
            }
            catch
            {
                // best-effort
            }
        }
    }

    public void InvalidateAll()
    {
        InvalidateProjectsList();
        _blueprintPaths.Clear();
        foreach (var key in _blueprints.Keys.ToArray())
        {
            if (_blueprints.TryRemove(key, out var old))
            {
                try { old.Doc.Dispose(); } catch { /* ignore */ }
            }
        }
        _dirs.Clear();
    }

    private static List<ProjectInfo> CloneProjects(IReadOnlyList<ProjectInfo> src)
    {
        var list = new List<ProjectInfo>(src.Count);
        foreach (var p in src)
        {
            list.Add(new ProjectInfo
            {
                Id = p.Id,
                Title = p.Title,
                Label = p.Label,
                Path = p.Path,
            });
        }
        return list;
    }

    private static Dictionary<string, long> CloneDir(Dictionary<string, long> src) =>
        new(src, StringComparer.OrdinalIgnoreCase);

    private sealed class BlueprintEntry
    {
        public long Ticks { get; init; }
        public long Length { get; init; }
        public byte[] Utf8 { get; init; } = Array.Empty<byte>();
        public JsonDocument Doc { get; init; } = null!;
    }

    private sealed class DirEntry
    {
        public bool Exists { get; init; }
        public long DirTicks { get; init; }
        public Dictionary<string, long> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
