using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;

namespace PageToMovie.Engine;

/// <summary>
/// Admin full-project zip export / import for local debugging.
/// Zip layout: <c>{projectId}/…</c> (project.json at that folder root).
/// </summary>
public sealed class ProjectArchiveService
{
    private static readonly JsonSerializerOptions JsonOpts = JsonDefaults.IndentedCaseInsensitive;

    /// <summary>Compressed zip size cap (matches Kestrel multipart limit for admin import).</summary>
    public const long MaxZipBytes = 512L * 1024 * 1024;
    /// <summary>Max entries in a project zip (directories + files).</summary>
    public const int MaxZipEntries = 50_000;
    /// <summary>Max total uncompressed payload extracted from a zip.</summary>
    public const long MaxUncompressedTotalBytes = 2L * 1024 * 1024 * 1024;
    /// <summary>Max size of any single extracted entry.</summary>
    public const long MaxSingleEntryUncompressedBytes = 512L * 1024 * 1024;

    private readonly ProjectStore _projects;
    private readonly ClipSidecarService? _sidecars;
    private readonly ProjectMigrationService? _migrations;
    private readonly ILogger<ProjectArchiveService> _log;

    public ProjectArchiveService(
        ProjectStore projects,
        ClipSidecarService sidecars,
        ProjectMigrationService migrations,
        ILogger<ProjectArchiveService>? log = null)
    {
        _projects = projects;
        _sidecars = sidecars;
        _migrations = migrations;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectArchiveService>.Instance;
    }

    public ProjectArchiveService(
        ProjectStore projects,
        ClipSidecarService sidecars,
        ILogger<ProjectArchiveService>? log)
        : this(projects, sidecars, null!, log)
    {
    }

    public ProjectArchiveService(ProjectStore projects, ILogger<ProjectArchiveService>? log)
        : this(projects, null!, null!, log)
    {
    }

    /// <summary>
    /// Build a zip of the entire project directory. Caller must dispose the stream
    /// (FileStream with DeleteOnClose).
    /// </summary>
    public async Task<ProjectExportResult> ExportAsync(string projectId, CancellationToken ct = default)
    {
        // ASP.NET keeps a single route segment's %2F encoded rather than decoding it (see
        // ProjectStore.NormalizeProjectId's own doc comment) — a composite "owner/slug" id
        // arrives here as e.g. "budcribar%2FTellTaleHeartV7" unless normalized. Without this,
        // both the download filename and every entry path inside the zip end up with a literal
        // "%2F" baked into them as text instead of the intended nested owner/slug structure.
        var id = ProjectStore.NormalizeProjectId((projectId ?? "").Trim());
        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("Project id required");

        var projectDir = _projects.GetProjectDir(id);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        // Filesystem-safe download filename — id may contain a real "/" (owner/slug), which
        // can't appear in an actual filename, so it's replaced here only; the zip's internal
        // entry paths below use the real id with its "/" intact to form genuine subfolders.
        var fileNameSafeId = id.Replace('/', '_');
        var fileName = $"PageToMovie_{fileNameSafeId}_{stamp}.zip";
        var tempPath = Path.Combine(Path.GetTempPath(), $"ptm-export-{Guid.NewGuid():N}.zip");

        if (_migrations is not null)
        {
            try
            {
                await _migrations.MigrateIfNeededAsync(projectDir, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Export: schema migration failed for {ProjectId}", id);
            }
        }
        else if (_sidecars is not null)
        {
            try
            {
                await _sidecars.ConvertProjectClipsToNewFormatAsync(projectDir, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Export: clip format conversion failed for {ProjectId}", id);
            }
        }

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // Manifest for importers
                    // Ensure project.json carries schema_version before packaging (for converters).
                    try
                    {
                        EnsureProjectSchemaVersionOnDisk(projectDir);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Export: could not stamp schema_version on {ProjectId}", id);
                    }

                    var projectSchema = ProjectFormatVersions.TryReadProjectSchemaVersion(projectDir)
                                        ?? ProjectFormatVersions.ProjectSchemaVersion;
                    var metaEntry = zip.CreateEntry($"{id}/_export_meta.json", CompressionLevel.Fastest);
                    using (var w = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                    {
                        w.Write(JsonSerializer.Serialize(
                            ProjectFormatVersions.BuildExportMeta(id, projectSchema),
                            JsonOpts));
                    }

                    foreach (var file in Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        var rel = Path.GetRelativePath(projectDir, file);
                        if (string.IsNullOrEmpty(rel) || rel.StartsWith("..", StringComparison.Ordinal))
                            continue;
                        // Skip OS junk
                        var name = Path.GetFileName(file);
                        if (name is "Thumbs.db" or ".DS_Store")
                            continue;

                        var entryName = $"{id}/{rel.Replace('\\', '/')}";
                        zip.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
                    }
                }
            }, ct).ConfigureAwait(false);

            var length = new FileInfo(tempPath).Length;
            _log.LogInformation("Exported project {ProjectId} → {Bytes} bytes", id, length);

            // Open for reading; delete when stream is disposed
            var read = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            return new ProjectExportResult
            {
                Stream = read,
                FileName = fileName,
                ContentType = "application/zip",
                ProjectId = id,
                ByteLength = length,
            };
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>
    /// Import a project zip. Supports:
    /// <list type="bullet">
    /// <item>Entries under <c>{id}/…</c> with project.json</item>
    /// <item>Entries with project.json at zip root</item>
    /// </list>
    /// </summary>
    public async Task<ProjectImportResult> ImportAsync(
        Stream zipStream,
        string? preferredId = null,
        bool overwrite = false,
        string? targetUserId = null,
        CancellationToken ct = default)
    {
        if (zipStream is null || !zipStream.CanRead)
            throw new InvalidOperationException("Zip stream required");

        var tempZip = Path.Combine(Path.GetTempPath(), $"ptm-import-{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(Path.GetTempPath(), $"ptm-import-dir-{Guid.NewGuid():N}");

        try
        {
            await using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CopyWithSizeCapAsync(zipStream, fs, MaxZipBytes, ct).ConfigureAwait(false);
            }

            Directory.CreateDirectory(tempExtract);
            ExtractZipSafely(tempZip, tempExtract, ct);

            var contentRoot = FindProjectContentRoot(tempExtract)
                ?? throw new InvalidOperationException(
                    "Zip does not look like a PageToMovie project (no project.json found).");

            var idFromMeta = TryReadProjectId(contentRoot);
            var idFromFolder = Path.GetFileName(contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var rawId = !string.IsNullOrWhiteSpace(preferredId)
                ? preferredId.Trim()
                : !string.IsNullOrWhiteSpace(idFromMeta)
                    ? idFromMeta!
                    : idFromFolder;

            // Preserves an "owner/slug" split (SanitizeProjectIdPublic alone would collapse the "/"
            // into "_", landing the import at a flat projects/{owner}_{slug}/ instead of the
            // namespaced projects/{owner}/{slug}/ layout the rest of the app expects).
            var id = ProjectStore.SanitizeComposeProjectIdPublic(rawId);
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("Could not derive a safe project id from the zip.");

            var projectsRoot = Path.GetFullPath(Path.Combine(_projects.WorkspaceRoot, "projects"));
            Directory.CreateDirectory(projectsRoot);
            var dest = Path.GetFullPath(Path.Combine(projectsRoot, id));

            if (!dest.StartsWith(projectsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid project destination path.");

            if (Directory.Exists(dest))
            {
                if (!overwrite)
                    throw new InvalidOperationException(
                        $"Project already exists: {id}. Enable overwrite or choose another id.");
                await _projects.DeleteProjectAsync(id, ct).ConfigureAwait(false);
            }

            // Copy extracted content into projects/{id}
            CopyDirectory(contentRoot, dest);

            var exportMeta = ProjectFormatVersions.TryReadExportMeta(contentRoot)
                             ?? ProjectFormatVersions.TryReadExportMeta(dest);
            var schemaBefore = ProjectFormatVersions.TryReadProjectSchemaVersion(dest)
                               ?? exportMeta?.ProjectSchemaVersion
                               ?? "v0";

            // Ensure project.json id and optional ownerUserId match
            await EnsureProjectJsonIdAsync(dest, id, targetUserId, ct).ConfigureAwait(false);

            var migrated = false;
            if (_migrations is not null)
            {
                try
                {
                    migrated = await _migrations.MigrateIfNeededAsync(dest, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Import: schema migration failed for {ProjectId}", id);
                }
            }

            var schemaAfter = ProjectFormatVersions.TryReadProjectSchemaVersion(dest)
                              ?? ProjectFormatVersions.ProjectSchemaVersion;

            _projects.InvalidateReadCaches(null);
            var info = await _projects.ActivateAsync(id, ct).ConfigureAwait(false);

            _log.LogInformation(
                "Imported project {ProjectId} from zip (overwrite={Overwrite}, exportFmt={ExportFmt}, schema {Before}→{After}, migrated={Migrated})",
                id, overwrite, exportMeta?.ExportFormatVersion, schemaBefore, schemaAfter, migrated);

            var msg = overwrite
                ? $"Imported and replaced project “{id}”"
                : $"Imported project “{id}”";
            if (migrated)
                msg += $" · converted project schema {schemaBefore} → {schemaAfter}";
            else if (!string.Equals(schemaBefore, schemaAfter, StringComparison.OrdinalIgnoreCase))
                msg += $" · schema {schemaAfter}";
            if (exportMeta?.ExportFormatVersion is int efv)
                msg += $" · export format v{efv}";

            return new ProjectImportResult
            {
                Ok = true,
                ProjectId = id,
                Project = info,
                Message = msg,
                ExportFormatVersion = exportMeta?.ExportFormatVersion,
                ProjectSchemaVersionBefore = schemaBefore,
                ProjectSchemaVersionAfter = schemaAfter,
                Migrated = migrated,
            };
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, recursive: true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Extract zip with entry-count / total-size / path-traversal guards (zip-bomb mitigation).
    /// </summary>
    internal static void ExtractZipSafely(string zipPath, string destDir, CancellationToken ct = default)
    {
        destDir = Path.GetFullPath(destDir);
        Directory.CreateDirectory(destDir);

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxZipEntries)
        {
            throw new InvalidOperationException(
                $"Zip has too many entries ({archive.Entries.Count:N0}; max {MaxZipEntries:N0}).");
        }

        long totalUncompressed = 0;
        var entryCount = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            entryCount++;
            if (entryCount > MaxZipEntries)
            {
                throw new InvalidOperationException(
                    $"Zip has too many entries (max {MaxZipEntries:N0}).");
            }

            // Directory entries end with / or \
            var name = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith('/'))
            {
                // Ensure directory exists (still count toward bomb limits via empty path only).
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var dirPath = Path.GetFullPath(Path.Combine(destDir, name));
                    EnsureUnderRoot(destDir, dirPath);
                    Directory.CreateDirectory(dirPath);
                }
                continue;
            }

            if (entry.Length < 0 || entry.Length > MaxSingleEntryUncompressedBytes)
            {
                throw new InvalidOperationException(
                    $"Zip entry too large: {entry.FullName} ({entry.Length:N0} bytes; max {MaxSingleEntryUncompressedBytes:N0}).");
            }

            totalUncompressed += entry.Length;
            if (totalUncompressed > MaxUncompressedTotalBytes)
            {
                throw new InvalidOperationException(
                    $"Zip uncompressed size exceeds limit ({MaxUncompressedTotalBytes:N0} bytes).");
            }

            var destPath = Path.GetFullPath(Path.Combine(destDir, name));
            EnsureUnderRoot(destDir, destPath);

            var parent = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            entry.ExtractToFile(destPath, overwrite: true);

            // Defense in depth: measure actual written size (some archives lie in headers).
            var written = new FileInfo(destPath).Length;
            if (written > MaxSingleEntryUncompressedBytes)
            {
                try { File.Delete(destPath); } catch { /* ignore */ }
                throw new InvalidOperationException(
                    $"Extracted entry exceeded size cap: {entry.FullName}");
            }
        }
    }

    private static void EnsureUnderRoot(string root, string candidate)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(candidate);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Zip entry path escapes destination directory.");
        }
    }

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
                    $"Zip file exceeds size limit ({maxBytes:N0} bytes).");
            await dest.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
        }
    }

    private static string? FindProjectContentRoot(string extractRoot)
    {
        var direct = Path.Combine(extractRoot, "project.json");
        if (File.Exists(direct))
            return extractRoot;

        // Single top-level folder with project.json
        var dirs = Directory.GetDirectories(extractRoot);
        foreach (var d in dirs)
        {
            if (File.Exists(Path.Combine(d, "project.json")))
                return d;
        }

        // Nested: projects/MyId/project.json
        var nested = Directory.GetFiles(extractRoot, "project.json", SearchOption.AllDirectories)
            .OrderBy(p => p.Length)
            .FirstOrDefault();
        if (nested is not null)
            return Path.GetDirectoryName(nested);

        return null;
    }

    private static string? TryReadProjectId(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "project.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("id", out var idEl))
                return idEl.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

    private static async Task EnsureProjectJsonIdAsync(string dest, string id, string? targetUserId, CancellationToken ct)
    {
        var path = Path.Combine(dest, "project.json");
        Dictionary<string, object?> meta;
        if (File.Exists(path))
        {
            try
            {
                meta = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                           await File.ReadAllTextAsync(path, ct).ConfigureAwait(false), JsonOpts)
                       ?? new Dictionary<string, object?>();
            }
            catch
            {
                meta = new Dictionary<string, object?>();
            }
        }
        else
        {
            meta = new Dictionary<string, object?>
            {
                ["title"] = id,
                ["blueprint_file"] = "blueprint.clips.grok.json",
                ["scenes_file"] = "scenes.json",
                ["config_file"] = "pipeline_config.json",
                ["state_file"] = "pipeline_state.json",
            };
        }

        meta["id"] = id;
        if (!meta.ContainsKey("title") || meta["title"] is null || string.IsNullOrWhiteSpace(meta["title"]?.ToString()))
            meta["title"] = id;

        if (!string.IsNullOrWhiteSpace(targetUserId))
        {
            meta["ownerUserId"] = targetUserId.Trim();
            meta["owner_user_id"] = targetUserId.Trim();
        }

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(meta, JsonOpts) + "\n",
            ct).ConfigureAwait(false);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            if (rel.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsafe path in archive: {rel}");
            var target = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>Stamp project.json schema_version when missing (export-time safety net).</summary>
    static void EnsureProjectSchemaVersionOnDisk(string projectDir)
    {
        var path = Path.Combine(projectDir, "project.json");
        if (!File.Exists(path)) return;
        var text = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("schema_version", out var existing) &&
            existing.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(existing.GetString()))
            return;

        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(text, JsonOpts)
                   ?? new Dictionary<string, object?>();
        dict["schema_version"] = ProjectFormatVersions.ProjectSchemaVersion;
        dict["schema_version_stamped_at_utc"] = DateTime.UtcNow.ToString("o");
        File.WriteAllText(path, JsonSerializer.Serialize(dict, JsonOpts) + "\n");
    }
}

public sealed class ProjectExportResult : IAsyncDisposable, IDisposable
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/zip";
    public string ProjectId { get; init; } = "";
    public long ByteLength { get; init; }

    public void Dispose() => Stream.Dispose();

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

public sealed class ProjectImportResult
{
    public bool Ok { get; init; }
    public string ProjectId { get; init; } = "";
    public ProjectInfo? Project { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    /// <summary>From _export_meta.json when present.</summary>
    public int? ExportFormatVersion { get; init; }
    public string? ProjectSchemaVersionBefore { get; init; }
    public string? ProjectSchemaVersionAfter { get; init; }
    public bool Migrated { get; init; }
}
