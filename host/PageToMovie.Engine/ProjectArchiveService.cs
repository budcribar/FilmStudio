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

    private readonly ProjectStore _projects;
    private readonly ILogger<ProjectArchiveService> _log;

    public ProjectArchiveService(ProjectStore projects, ILogger<ProjectArchiveService>? log = null)
    {
        _projects = projects;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectArchiveService>.Instance;
    }

    /// <summary>
    /// Build a zip of the entire project directory. Caller must dispose the stream
    /// (FileStream with DeleteOnClose).
    /// </summary>
    public async Task<ProjectExportResult> ExportAsync(string projectId, CancellationToken ct = default)
    {
        var id = (projectId ?? "").Trim();
        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("Project id required");

        var projectDir = _projects.GetProjectDir(id);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"PageToMovie_{id}_{stamp}.zip";
        var tempPath = Path.Combine(Path.GetTempPath(), $"ptm-export-{Guid.NewGuid():N}.zip");

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // Manifest for importers
                    var metaEntry = zip.CreateEntry($"{id}/_export_meta.json", CompressionLevel.Fastest);
                    using (var w = new StreamWriter(metaEntry.Open(), Encoding.UTF8))
                    {
                        w.Write(JsonSerializer.Serialize(new
                        {
                            schema = "PageToMovie.project_export.v1",
                            projectId = id,
                            exportedAtUtc = DateTime.UtcNow.ToString("o"),
                            note = "Full project folder for local debug. Unzip or use Admin → Import project.",
                        }, JsonOpts));
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
                await zipStream.CopyToAsync(fs, ct).ConfigureAwait(false);
            }

            Directory.CreateDirectory(tempExtract);
            ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

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

            var id = ProjectStore.SanitizeProjectIdPublic(rawId);
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

            // Ensure project.json id matches folder
            await EnsureProjectJsonIdAsync(dest, id, ct).ConfigureAwait(false);

            _projects.InvalidateReadCaches(null);
            var info = await _projects.ActivateAsync(id, ct).ConfigureAwait(false);

            _log.LogInformation("Imported project {ProjectId} from zip (overwrite={Overwrite})", id, overwrite);

            return new ProjectImportResult
            {
                Ok = true,
                ProjectId = id,
                Project = info,
                Message = overwrite
                    ? $"Imported and replaced project “{id}”"
                    : $"Imported project “{id}”",
            };
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, recursive: true); } catch { /* ignore */ }
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

    private static async Task EnsureProjectJsonIdAsync(string dest, string id, CancellationToken ct)
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
}
