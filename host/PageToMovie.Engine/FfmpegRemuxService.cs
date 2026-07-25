using System.Diagnostics;
using System.Text.RegularExpressions;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Resolves an optional native ffmpeg binary and shared clip/WIP path helpers.
/// Does <b>not</b> remux scenes or WIP — play/stitch is client-side (<c>ffmpeg.wasm</c>).
/// </summary>
public sealed class FfmpegRemuxService : IFfmpegRemux
{
    /// <summary>Strict: scene_01_clip_02.mp4 only — not .native sidecars.</summary>
    private static readonly Regex ExactClipNameRe = new(
        @"^scene_(\d{2})_clip_(\d{2})\.mp4$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SceneSourcesManifestPath(string compositePath) =>
        compositePath + ".sources.json";

    public static string WipSourcesManifestPath(string wipPath) =>
        wipPath + ".sources.json";

    public static bool IsExactClipFileName(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && ExactClipNameRe.IsMatch(fileName);

    private static bool RegexSceneOnly(string name) =>
        Regex.IsMatch(name, @"^scene_\d{2}\.mp4$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Ordered inputs for freshness checks: scene composites first, else exact clip files.
    /// </summary>
    public static List<string> ListWipSourceFiles(string videoDir)
    {
        if (!Directory.Exists(videoDir))
            return new List<string>();

        var sceneFiles = new DirectoryInfo(videoDir).GetFiles("scene_*.mp4")
            .Where(f => RegexSceneOnly(f.Name))
            .Where(f =>
            {
                try { return f.Length >= 1024; }
                catch { return false; }
            })
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => f.FullName)
            .ToList();

        if (sceneFiles.Count > 0)
            return sceneFiles;

        return new DirectoryInfo(videoDir).GetFiles("scene_*_clip_*.mp4")
            .Where(f => IsExactClipFileName(f.Name))
            .Where(f =>
            {
                try { return f.Length >= 1024; }
                catch { return false; }
            })
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => f.FullName)
            .ToList();
    }

    private readonly PageToMovieOptions _opts;
    private readonly ILogger<FfmpegRemuxService> _log;
    private string? _resolvedPath;
    private readonly object _resolveLock = new();

    public FfmpegRemuxService(
        IOptions<PageToMovieOptions> opts,
        ILogger<FfmpegRemuxService> log)
    {
        _opts = opts.Value;
        _log = log;
    }

    // Back-compat ctor for older tests that pass unused services
    public FfmpegRemuxService(
        ProjectStore projects,
        EditLogService editLogs,
        ProjectTelemetryService telemetry,
        IOptions<PageToMovieOptions> opts,
        ILogger<FfmpegRemuxService> log,
        CreditsGeneratorService? creditsGenerator = null)
        : this(opts, log)
    {
        _ = projects;
        _ = editLogs;
        _ = telemetry;
        _ = creditsGenerator;
    }

    public string FfmpegPath => _opts.UseNativeFfmpeg ? ResolveFfmpegPath() : "";

    public bool IsAvailable()
    {
        if (!_opts.UseNativeFfmpeg)
            return false;
        try
        {
            var path = ResolveFfmpegPath();
            if (string.IsNullOrWhiteSpace(path))
                return false;
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
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Native ffmpeg not available");
            return false;
        }
    }

    private string ResolveFfmpegPath()
    {
        lock (_resolveLock)
        {
            if (_resolvedPath is not null)
                return _resolvedPath;

            if (!string.IsNullOrWhiteSpace(_opts.FfmpegPath))
            {
                var configured = _opts.FfmpegPath.Trim();
                if (File.Exists(configured))
                {
                    _resolvedPath = Path.GetFullPath(configured);
                    return _resolvedPath;
                }
                // bare name "ffmpeg"
                if (configured.IndexOfAny(new[] { '/', '\\', ':' }) < 0)
                {
                    _resolvedPath = configured;
                    return _resolvedPath;
                }
            }

            var candidates = new List<string>();
            foreach (var root in new[]
                     {
                         AppContext.BaseDirectory,
                         Path.GetDirectoryName(typeof(FfmpegRemuxService).Assembly.Location) ?? "",
                     }.Where(r => r.Length > 0))
            {
                candidates.Add(Path.Combine(root, "Resources", "ffmpeg.exe"));
                candidates.Add(Path.Combine(root, "ffmpeg.exe"));
                candidates.Add(Path.Combine(root, "bin", "ffmpeg.exe"));
            }

            foreach (var c in candidates)
            {
                try
                {
                    if (File.Exists(c) && new FileInfo(c).Length > 100_000)
                    {
                        _resolvedPath = Path.GetFullPath(c);
                        return _resolvedPath;
                    }
                }
                catch { /* ignore */ }
            }

            _resolvedPath = "ffmpeg";
            return _resolvedPath;
        }
    }
}
