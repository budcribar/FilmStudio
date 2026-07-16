using System.Diagnostics;
using System.Text;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

/// <summary>
/// FFmpeg scene remux + WIP movie rebuild.
/// Resolves ffmpeg from: config path → NuGet-shipped Resources/ffmpeg.exe
/// (Soenneker.Libraries.FFmpeg) → PATH.
/// </summary>
public sealed class FfmpegRemuxService
{
    private readonly ProjectStore _projects;
    private readonly FilmStudioOptions _opts;
    private readonly ILogger<FfmpegRemuxService> _log;
    private string? _resolvedPath;
    private readonly object _resolveLock = new();

    public FfmpegRemuxService(
        ProjectStore projects,
        IOptions<FilmStudioOptions> opts,
        ILogger<FfmpegRemuxService> log)
    {
        _projects = projects;
        _opts = opts.Value;
        _log = log;
    }

    /// <summary>Resolved ffmpeg executable path (absolute when possible).</summary>
    public string FfmpegPath => ResolveFfmpegPath();

    public bool IsAvailable()
    {
        try
        {
            var path = ResolveFfmpegPath();
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(8000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Concat clips for a scene into assets/video/scene_XX.mp4.</summary>
    public async Task<string?> RemuxSceneAsync(
        string projectId,
        int sceneNum,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        EnsureAvailable(onProgress);

        var projectDir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        var clips = ListSceneClipFiles(videoDir, sceneNum);
        if (clips.Count == 0)
            throw new InvalidOperationException($"No clip files for scene {sceneNum} under {videoDir}");

        onProgress?.Invoke($"Remux S{sceneNum:D2}: {clips.Count} clip(s) via {Path.GetFileName(FfmpegPath)}…");
        var listFile = Path.Combine(videoDir, $"_concat_s{sceneNum:D2}.txt");
        var sb = new StringBuilder();
        foreach (var c in clips)
        {
            var escaped = c.Replace("\\", "/").Replace("'", "'\\''");
            sb.AppendLine($"file '{escaped}'");
        }
        await File.WriteAllTextAsync(listFile, sb.ToString(), ct);

        var outPath = Path.Combine(videoDir, $"scene_{sceneNum:D2}.mp4");
        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outPath}\"";
        var (exit, log) = await RunFfmpegAsync(args, projectDir, ct);
        try { File.Delete(listFile); } catch { /* ignore */ }

        if (exit != 0)
        {
            onProgress?.Invoke("Concat copy failed — re-encoding…");
            await File.WriteAllTextAsync(listFile, sb.ToString(), ct);
            args =
                $"-y -f concat -safe 0 -i \"{listFile}\" -c:v libx264 -preset veryfast -crf 20 " +
                $"-c:a aac -b:a 160k \"{outPath}\"";
            (exit, log) = await RunFfmpegAsync(args, projectDir, ct);
            try { File.Delete(listFile); } catch { /* ignore */ }
        }

        if (exit != 0 || !File.Exists(outPath) || new FileInfo(outPath).Length < 1024)
            throw new InvalidOperationException($"FFmpeg remux failed for scene {sceneNum}: {TrimLog(log)}");

        onProgress?.Invoke($"Remuxed → {Path.GetFileName(outPath)}");
        return outPath;
    }

    /// <summary>Concat scene/clip files into WIP movie path from config.</summary>
    public async Task<string?> RebuildWipAsync(
        string projectId,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        EnsureAvailable(onProgress);

        var projectDir = _projects.GetProjectDir(projectId);
        var cfg = LoadConfig(projectDir);
        var wipRel = cfg.TryGetValue("wip_movie_path", out var w)
            ? w?.ToString() ?? "assets/movie_wip.mp4"
            : "assets/movie_wip.mp4";
        var wipPath = Path.IsPathRooted(wipRel)
            ? wipRel
            : Path.Combine(projectDir, wipRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(wipPath)!);

        var videoDir = Path.Combine(projectDir, "assets", "video");
        var sceneFiles = Directory.Exists(videoDir)
            ? Directory.GetFiles(videoDir, "scene_*.mp4")
                .Where(f => RegexSceneOnly(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        if (sceneFiles.Count == 0)
        {
            sceneFiles = Directory.Exists(videoDir)
                ? Directory.GetFiles(videoDir, "scene_*_clip_*.mp4")
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
        }

        if (sceneFiles.Count == 0)
            throw new InvalidOperationException("No scene or clip videos found to build WIP.");

        onProgress?.Invoke($"WIP rebuild from {sceneFiles.Count} file(s) via {Path.GetFileName(FfmpegPath)}…");
        var listFile = Path.Combine(videoDir, "_concat_wip.txt");
        var sb = new StringBuilder();
        foreach (var c in sceneFiles)
        {
            var escaped = c.Replace("\\", "/").Replace("'", "'\\''");
            sb.AppendLine($"file '{escaped}'");
        }
        await File.WriteAllTextAsync(listFile, sb.ToString(), ct);

        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{wipPath}\"";
        var (exit, log) = await RunFfmpegAsync(args, projectDir, ct);
        if (exit != 0)
        {
            args =
                $"-y -f concat -safe 0 -i \"{listFile}\" -c:v libx264 -preset veryfast -crf 20 " +
                $"-c:a aac -b:a 160k \"{wipPath}\"";
            (exit, log) = await RunFfmpegAsync(args, projectDir, ct);
        }
        try { File.Delete(listFile); } catch { /* ignore */ }

        if (exit != 0 || !File.Exists(wipPath))
            throw new InvalidOperationException($"FFmpeg WIP rebuild failed: {TrimLog(log)}");

        onProgress?.Invoke($"WIP → {wipPath}");
        return wipPath;
    }

    private void EnsureAvailable(Action<string>? onProgress)
    {
        if (IsAvailable())
        {
            onProgress?.Invoke($"ffmpeg: {FfmpegPath}");
            return;
        }

        throw new InvalidOperationException(
            "ffmpeg not found. Expected NuGet-shipped Resources/ffmpeg.exe " +
            "(Soenneker.Libraries.FFmpeg), FilmStudio:FfmpegPath, or ffmpeg on PATH.");
    }

    /// <summary>
    /// Resolution order:
    /// 1) FilmStudio:FfmpegPath when set to an existing file (or usable name)
    /// 2) App output Resources/ffmpeg.exe (Soenneker package content)
    /// 3) Same folder as the host assembly
    /// 4) Engine assembly Resources/
    /// 5) Bare "ffmpeg" (PATH)
    /// </summary>
    private string ResolveFfmpegPath()
    {
        if (_resolvedPath is not null)
            return _resolvedPath;

        lock (_resolveLock)
        {
            if (_resolvedPath is not null)
                return _resolvedPath;

            var candidates = new List<string>();

            // 1) Explicit config
            if (!string.IsNullOrWhiteSpace(_opts.FfmpegPath))
            {
                var configured = _opts.FfmpegPath.Trim();
                if (File.Exists(configured))
                    candidates.Add(Path.GetFullPath(configured));
                else if (!string.Equals(configured, "ffmpeg", StringComparison.OrdinalIgnoreCase))
                {
                    // Relative to base dir
                    var rel = Path.Combine(AppContext.BaseDirectory, configured);
                    if (File.Exists(rel))
                        candidates.Add(Path.GetFullPath(rel));
                }
            }

            // 2–4) Bundled Soenneker content + common layouts
            var bases = new[]
            {
                AppContext.BaseDirectory,
                Path.GetDirectoryName(typeof(FfmpegRemuxService).Assembly.Location) ?? "",
                Directory.GetCurrentDirectory(),
            }.Where(b => !string.IsNullOrWhiteSpace(b)).Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in bases)
            {
                candidates.Add(Path.Combine(root, "Resources", "ffmpeg.exe"));
                candidates.Add(Path.Combine(root, "ffmpeg.exe"));
                candidates.Add(Path.Combine(root, "bin", "ffmpeg.exe"));
                candidates.Add(Path.Combine(root, "ffmpeg", "ffmpeg.exe"));
            }

            // NuGet package cache (Soenneker.Libraries.FFmpeg) — works even if content
            // was not copied into the host OutDir (e.g. restore/copy glitch).
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var pkgRoot = Path.Combine(userProfile, ".nuget", "packages", "soenneker.libraries.ffmpeg");
                if (Directory.Exists(pkgRoot))
                {
                    foreach (var verDir in Directory.GetDirectories(pkgRoot)
                                 .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
                    {
                        candidates.Add(Path.Combine(verDir, "content", "Resources", "ffmpeg.exe"));
                        candidates.Add(Path.Combine(verDir, "contentFiles", "any", "net9.0", "Resources", "ffmpeg.exe"));
                    }
                }
            }
            catch { /* ignore */ }

            foreach (var c in candidates)
            {
                try
                {
                    if (File.Exists(c) && new FileInfo(c).Length > 100_000)
                    {
                        _resolvedPath = Path.GetFullPath(c);
                        _log.LogInformation("Using bundled/local ffmpeg: {Path}", _resolvedPath);
                        return _resolvedPath;
                    }
                }
                catch { /* ignore */ }
            }

            // 5) PATH
            _resolvedPath = string.IsNullOrWhiteSpace(_opts.FfmpegPath) ? "ffmpeg" : _opts.FfmpegPath.Trim();
            return _resolvedPath;
        }
    }

    private static bool RegexSceneOnly(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^scene_\d{2}\.mp4$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static List<string> ListSceneClipFiles(string videoDir, int sceneNum)
    {
        if (!Directory.Exists(videoDir)) return new();
        var prefix = $"scene_{sceneNum:D2}_clip_";
        return Directory.GetFiles(videoDir, $"{prefix}*.mp4")
            .Where(f => new FileInfo(f).Length >= 1024)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object?> LoadConfig(string projectDir)
    {
        var path = Path.Combine(projectDir, "pipeline_config.json");
        if (!File.Exists(path)) return new();
        try
        {
            return GrokChatClient.ParseJsonObject(File.ReadAllText(path));
        }
        catch { return new(); }
    }

    private async Task<(int Exit, string Log)> RunFfmpegAsync(
        string arguments,
        string workingDir,
        CancellationToken ct)
    {
        var exe = ResolveFfmpegPath();
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {exe}");

        // Read stdout+stderr in parallel — sequential ReadToEnd can deadlock when buffers fill.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var log = (stdout + "\n" + stderr).Trim();
        if (proc.ExitCode != 0)
            _log.LogWarning("ffmpeg exit {Code}: {Log}", proc.ExitCode, TrimLog(log));
        return (proc.ExitCode, log);
    }

    private static string TrimLog(string log) =>
        log.Length <= 600 ? log : log[^600..];
}
