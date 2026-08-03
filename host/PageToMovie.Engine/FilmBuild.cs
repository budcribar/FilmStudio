using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PageToMovie.Engine;

/// <summary>
/// Studio cut EDL + hash for one stitched WIP (<c>assets/movie_wip.film.json</c>).
/// Media bytes stay on the client; this JSON is project-git text provenance.
/// </summary>
public sealed class FilmBuildDocument
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = FilmBuildService.SchemaVersion;

    [JsonPropertyName("film_id")]
    public string FilmId { get; set; } = "";

    [JsonPropertyName("created_at_utc")]
    public string CreatedAtUtc { get; set; } = "";

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("studio")]
    public FilmBuildStudio Studio { get; set; } = new();

    [JsonPropertyName("timeline")]
    public FilmBuildTimeline Timeline { get; set; } = new();

    [JsonPropertyName("assembly")]
    public FilmBuildAssembly Assembly { get; set; } = new();

    [JsonPropertyName("provenance")]
    public FilmBuildProvenance Provenance { get; set; } = new();
}

public sealed class FilmBuildStudio
{
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = "assets/movie_wip.mp4";

    [JsonPropertyName("byte_length")]
    public long? ByteLength { get; set; }
}

public sealed class FilmBuildTimeline
{
    [JsonPropertyName("total_seconds")]
    public double TotalSeconds { get; set; }

    [JsonPropertyName("segments")]
    public List<FilmBuildSegment> Segments { get; set; } = new();
}

public sealed class FilmBuildSegment
{
    [JsonPropertyName("i")]
    public int Index { get; set; }

    [JsonPropertyName("scene")]
    public int? Scene { get; set; }

    [JsonPropertyName("clip")]
    public int? Clip { get; set; }

    [JsonPropertyName("take")]
    public int? Take { get; set; }

    [JsonPropertyName("t_start")]
    public double TStart { get; set; }

    [JsonPropertyName("t_end")]
    public double TEnd { get; set; }

    [JsonPropertyName("src")]
    public string Src { get; set; } = "";

    [JsonPropertyName("src_sha256")]
    public string? SrcSha256 { get; set; }

    [JsonPropertyName("sidecar")]
    public string? Sidecar { get; set; }
}

public sealed class FilmBuildAssembly
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "ffmpeg";

    [JsonPropertyName("where")]
    public string Where { get; set; } = "client";
}

public sealed class FilmBuildProvenance
{
    [JsonPropertyName("app_repo")]
    public string AppRepo { get; set; } = "budcribar/PageToMovie";

    [JsonPropertyName("adaptation_version")]
    public string? AdaptationVersion { get; set; }

    [JsonPropertyName("prompt_content_sha256")]
    public string? PromptContentSha256 { get; set; }

    [JsonPropertyName("runtime_mode")]
    public string? RuntimeMode { get; set; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    [JsonPropertyName("stage1_manifest")]
    public string Stage1ManifestPath { get; set; } = ProjectStage1ConvertManifest.RelativePath;
}

/// <summary>Create / persist / load film builds.</summary>
public static class FilmBuildService
{
    public const string SchemaVersion = "film_build.v1";
    public const string RelativePath = "assets/movie_wip.film.json";

    public static string GetPath(string projectDir) =>
        Path.Combine(projectDir, "assets", "movie_wip.film.json");

    public static string NewFilmId(string projectId)
    {
        var slug = (projectId ?? "project").Replace('/', '_').Replace('\\', '_');
        if (slug.Length > 40) slug = slug[..40];
        var shortId = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"film_{slug}_{DateTime.UtcNow:yyyyMMddHHmmss}_{shortId}";
    }

    public static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static FilmBuildDocument Create(
        string projectId,
        string studioSha256,
        double durationSeconds,
        IReadOnlyList<FilmBuildSegment>? segments = null,
        long? byteLength = null,
        string assemblyWhere = "client",
        string studioPath = "assets/movie_wip.mp4")
    {
        var segs = segments?.ToList() ?? new List<FilmBuildSegment>();
        var total = durationSeconds;
        if (total <= 0 && segs.Count > 0)
            total = segs.Max(s => s.TEnd);

        var doc = new FilmBuildDocument
        {
            FilmId = NewFilmId(projectId),
            CreatedAtUtc = DateTime.UtcNow.ToString("o"),
            ProjectId = projectId,
            Studio = new FilmBuildStudio
            {
                Sha256 = studioSha256 ?? "",
                DurationSeconds = total,
                Path = studioPath,
                ByteLength = byteLength,
            },
            Timeline = new FilmBuildTimeline
            {
                TotalSeconds = total,
                Segments = segs,
            },
            Assembly = new FilmBuildAssembly
            {
                Tool = "ffmpeg",
                Where = assemblyWhere,
            },
        };
        return doc;
    }

    /// <summary>Attach Stage‑1 pins from convert manifest when present.</summary>
    public static void AttachStage1Provenance(string projectDir, FilmBuildDocument doc)
    {
        var m = ProjectStage1ConvertManifest.TryRead(projectDir);
        if (m is null) return;
        doc.Provenance.AdaptationVersion = m.AdaptationVersion;
        doc.Provenance.PromptContentSha256 = m.PromptContentSha256;
        doc.Provenance.RuntimeMode = m.RuntimeMode;
        doc.Provenance.ModelId = m.ModelId;
    }

    public static void Write(string projectDir, FilmBuildDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var path = GetPath(projectDir);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(doc, JsonDefaults.Indented);
        File.WriteAllText(path, json + "\n");
    }

    public static FilmBuildDocument? TryRead(string projectDir)
    {
        var path = GetPath(projectDir);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<FilmBuildDocument>(
                File.ReadAllText(path),
                JsonDefaults.IndentedCaseInsensitive);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Register a studio cut: write film build, auto-commit trajectory.
    /// </summary>
    public static FilmBuildDocument Register(
        ProjectStore store,
        string projectId,
        string studioSha256,
        double durationSeconds,
        IReadOnlyList<FilmBuildSegment>? segments = null,
        long? byteLength = null,
        string assemblyWhere = "client")
    {
        var projectDir = store.GetProjectDir(projectId);
        var doc = Create(projectId, studioSha256, durationSeconds, segments, byteLength, assemblyWhere);
        AttachStage1Provenance(projectDir, doc);
        Write(projectDir, doc);
        try
        {
            store.TriggerAutoGitCommit(projectId, ProjectStageCommits.FilmStitched(doc.FilmId));
        }
        catch
        {
            /* non-fatal */
        }
        return doc;
    }

    /// <summary>Hash on-disk WIP bytes and register a minimal film build (no timeline).</summary>
    public static FilmBuildDocument? RegisterFromWipFile(
        ProjectStore store,
        string projectId,
        string? wipRelativePath = null)
    {
        var projectDir = store.GetProjectDir(projectId);
        var rel = string.IsNullOrWhiteSpace(wipRelativePath) ? "assets/movie_wip.mp4" : wipRelativePath!;
        var full = Path.Combine(projectDir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return null;
        var bytes = File.ReadAllBytes(full);
        if (bytes.Length == 0) return null;
        return Register(
            store,
            projectId,
            HashBytes(bytes),
            durationSeconds: 0,
            segments: null,
            byteLength: bytes.Length,
            assemblyWhere: "server");
    }
}
