using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

/// <summary>
/// FFmpeg scene remux + WIP movie rebuild.
/// Resolves ffmpeg from: config path → NuGet-shipped Resources/ffmpeg.exe
/// (Soenneker.Libraries.FFmpeg) → PATH.
/// Streams stderr/stdout progress to <paramref name="onProgress"/> (SignalR job log).
/// </summary>
public sealed class FfmpegRemuxService
{
    private static readonly Regex DurationRe = new(
        @"Duration:\s*(\d{1,2}):(\d{2}):(\d{2}(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TimeEqualsRe = new(
        @"time=\s*(\d{1,2}):(\d{2}):(\d{2}(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FrameRe = new(
        @"frame=\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SpeedRe = new(
        @"speed=\s*([\d.]+x?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        onProgress?.Invoke("Probing clip durations…");
        var totalSec = await EstimateTotalDurationAsync(clips, onProgress, ct);
        if (totalSec is > 0)
            onProgress?.Invoke($"Estimated total duration ~{FormatHms(totalSec.Value)}");

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
        var (exit, log) = await RunFfmpegAsync(
            args, projectDir, ct, onProgress, totalSec, label: $"S{sceneNum:D2} copy");
        try { File.Delete(listFile); } catch { /* ignore */ }

        if (exit != 0)
        {
            onProgress?.Invoke("Concat copy failed — re-encoding…");
            await File.WriteAllTextAsync(listFile, sb.ToString(), ct);
            args =
                $"-y -f concat -safe 0 -i \"{listFile}\" -c:v libx264 -preset veryfast -crf 20 " +
                $"-c:a aac -b:a 160k \"{outPath}\"";
            (exit, log) = await RunFfmpegAsync(
                args, projectDir, ct, onProgress, totalSec, label: $"S{sceneNum:D2} encode");
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
        onProgress?.Invoke("Probing input durations…");
        var totalSec = await EstimateTotalDurationAsync(sceneFiles, onProgress, ct);
        if (totalSec is > 0)
            onProgress?.Invoke($"Estimated total duration ~{FormatHms(totalSec.Value)}");

        var listFile = Path.Combine(videoDir, "_concat_wip.txt");
        var sb = new StringBuilder();
        foreach (var c in sceneFiles)
        {
            var escaped = c.Replace("\\", "/").Replace("'", "'\\''");
            sb.AppendLine($"file '{escaped}'");
        }
        await File.WriteAllTextAsync(listFile, sb.ToString(), ct);

        var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{wipPath}\"";
        var (exit, log) = await RunFfmpegAsync(
            args, projectDir, ct, onProgress, totalSec, label: "WIP copy");
        if (exit != 0)
        {
            onProgress?.Invoke("WIP stream-copy failed — re-encoding…");
            args =
                $"-y -f concat -safe 0 -i \"{listFile}\" -c:v libx264 -preset veryfast -crf 20 " +
                $"-c:a aac -b:a 160k \"{wipPath}\"";
            (exit, log) = await RunFfmpegAsync(
                args, projectDir, ct, onProgress, totalSec, label: "WIP encode");
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

            if (!string.IsNullOrWhiteSpace(_opts.FfmpegPath))
            {
                var configured = _opts.FfmpegPath.Trim();
                if (File.Exists(configured))
                    candidates.Add(Path.GetFullPath(configured));
                else if (!string.Equals(configured, "ffmpeg", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = Path.Combine(AppContext.BaseDirectory, configured);
                    if (File.Exists(rel))
                        candidates.Add(Path.GetFullPath(rel));
                }
            }

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

            _resolvedPath = string.IsNullOrWhiteSpace(_opts.FfmpegPath) ? "ffmpeg" : _opts.FfmpegPath.Trim();
            return _resolvedPath;
        }
    }

    private static bool RegexSceneOnly(string name) =>
        Regex.IsMatch(name, @"^scene_\d{2}\.mp4$", RegexOptions.IgnoreCase);

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

    /// <summary>
    /// Sum per-file durations via <c>ffmpeg -i</c> Duration lines (no ffprobe required).
    /// </summary>
    private async Task<double?> EstimateTotalDurationAsync(
        IReadOnlyList<string> files,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        if (files.Count == 0) return null;
        double total = 0;
        var got = 0;
        // Cap probe cost for huge films
        var toProbe = files.Count <= 40 ? files : files.Take(40).ToList();
        for (var i = 0; i < toProbe.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var d = await ProbeDurationSecondsAsync(toProbe[i], ct);
            if (d is > 0)
            {
                total += d.Value;
                got++;
            }
            if (i == 0 || (i + 1) % 5 == 0 || i + 1 == toProbe.Count)
                onProgress?.Invoke($"  probe {i + 1}/{toProbe.Count}…");
        }
        if (got == 0) return null;
        if (toProbe.Count < files.Count && got > 0)
        {
            // Scale average for unprobed tail
            var avg = total / got;
            total += avg * (files.Count - toProbe.Count);
        }
        return total;
    }

    private async Task<double?> ProbeDurationSecondsAsync(string mediaPath, CancellationToken ct)
    {
        try
        {
            var exe = ResolveFfmpegPath();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                // ffmpeg prints metadata to stderr and exits non-zero without an output
                Arguments = $"-hide_banner -i \"{mediaPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var text = await stderrTask + "\n" + await stdoutTask;
            var m = DurationRe.Match(text);
            if (!m.Success) return null;
            return ParseHms(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Run ffmpeg with <c>-progress pipe:1</c>, stream lines to onProgress for SignalR.
    /// </summary>
    private async Task<(int Exit, string Log)> RunFfmpegAsync(
        string arguments,
        string workingDir,
        CancellationToken ct,
        Action<string>? onProgress = null,
        double? totalDurationSec = null,
        string label = "ffmpeg")
    {
        var exe = ResolveFfmpegPath();
        // -progress pipe:1 → key=value on stdout; -nostats quiet classic bar; keep errors on stderr
        var fullArgs = $"-hide_banner -loglevel info -progress pipe:1 -nostats {arguments}";
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = fullArgs,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {exe}");

        var log = new StringBuilder();
        var state = new ProgressState { TotalSec = totalDurationSec };
        var lastEmit = DateTime.UtcNow.AddSeconds(-1);

        void HandleLine(string? line, bool isStderr)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (log)
            {
                log.AppendLine(line);
                if (log.Length > 32_000)
                    log.Remove(0, log.Length - 24_000);
            }

            // Capture Duration from stderr if we didn't probe
            if (isStderr && state.TotalSec is null or <= 0)
            {
                var dm = DurationRe.Match(line);
                if (dm.Success)
                {
                    var d = ParseHms(dm.Groups[1].Value, dm.Groups[2].Value, dm.Groups[3].Value);
                    if (d is > 0)
                        state.TotalSec = (state.TotalSec ?? 0) + d.Value;
                }
            }

            var updated = ApplyProgressLine(line, state);
            if (!updated && !IsInterestingLogLine(line))
                return;

            // Throttle UI: at most ~4 updates/sec unless done
            var now = DateTime.UtcNow;
            var isEnd = line.Contains("progress=end", StringComparison.OrdinalIgnoreCase);
            if (!isEnd && (now - lastEmit).TotalMilliseconds < 250)
                return;
            lastEmit = now;

            var msg = FormatProgressMessage(label, state);
            if (!string.IsNullOrEmpty(msg))
                onProgress?.Invoke(msg);
            else if (IsInterestingLogLine(line))
                onProgress?.Invoke($"[{label}] {TrimOneLine(line, 160)}");
        }

        var stdoutTask = Task.Run(async () =>
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line is null) break;
                HandleLine(line, isStderr: false);
            }
        }, ct);

        var stderrTask = Task.Run(async () =>
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (line is null) break;
                HandleLine(line, isStderr: true);
            }
        }, ct);

        await using var reg = ct.Register(() =>
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch { /* ignore */ }
        });

        try
        {
            await proc.WaitForExitAsync(ct);
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var fullLog = log.ToString().Trim();
        if (proc.ExitCode != 0)
            _log.LogWarning("ffmpeg exit {Code}: {Log}", proc.ExitCode, TrimLog(fullLog));
        else
            onProgress?.Invoke($"[{label}] complete");

        return (proc.ExitCode, fullLog);
    }

    private static bool ApplyProgressLine(string line, ProgressState state)
    {
        var updated = false;
        // -progress pipe:1 key=value
        if (line.StartsWith("out_time_ms=", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(line.AsSpan("out_time_ms=".Length), out var ms) && ms >= 0)
        {
            state.OutSec = ms / 1_000_000.0;
            updated = true;
        }
        else if (line.StartsWith("out_time=", StringComparison.OrdinalIgnoreCase))
        {
            var t = line["out_time=".Length..].Trim();
            // HH:MM:SS.microseconds
            var parts = t.Split(':');
            if (parts.Length == 3 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
            {
                state.OutSec = h * 3600 + m * 60 + s;
                updated = true;
            }
        }
        else if (line.StartsWith("frame=", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(line.AsSpan("frame=".Length), out var fr))
        {
            state.Frame = fr;
            updated = true;
        }
        else if (line.StartsWith("fps=", StringComparison.OrdinalIgnoreCase) &&
                 double.TryParse(line.AsSpan("fps=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var fps))
        {
            state.Fps = fps;
            updated = true;
        }
        else if (line.StartsWith("speed=", StringComparison.OrdinalIgnoreCase))
        {
            state.Speed = line["speed=".Length..].Trim();
            updated = true;
        }
        else if (line.StartsWith("progress=", StringComparison.OrdinalIgnoreCase))
        {
            state.Phase = line["progress=".Length..].Trim();
            updated = true;
        }
        else
        {
            // Classic stats on one line: frame=  42 fps=... time=00:00:01.23 speed=1.2x
            var tm = TimeEqualsRe.Match(line);
            if (tm.Success)
            {
                state.OutSec = ParseHms(tm.Groups[1].Value, tm.Groups[2].Value, tm.Groups[3].Value);
                updated = true;
            }
            var fm = FrameRe.Match(line);
            if (fm.Success && int.TryParse(fm.Groups[1].Value, out var f2))
            {
                state.Frame = f2;
                updated = true;
            }
            var sm = SpeedRe.Match(line);
            if (sm.Success)
            {
                state.Speed = sm.Groups[1].Value;
                updated = true;
            }
        }
        return updated;
    }

    private static string FormatProgressMessage(string label, ProgressState state)
    {
        if (state.OutSec is null && state.Frame is null && string.IsNullOrEmpty(state.Speed))
            return "";

        var parts = new List<string> { $"[{label}]" };
        if (state.OutSec is double outSec)
        {
            var timePart = FormatHms(outSec);
            if (state.TotalSec is > 0.5)
            {
                var pct = Math.Clamp(outSec / state.TotalSec.Value * 100.0, 0, 100);
                parts.Add($"{timePart} / {FormatHms(state.TotalSec.Value)} ({pct:0.0}%)");
            }
            else
            {
                parts.Add($"time {timePart}");
            }
        }
        if (state.Frame is int fr)
            parts.Add($"frame {fr}");
        if (!string.IsNullOrEmpty(state.Speed))
            parts.Add($"speed {state.Speed}");
        if (state.Fps is > 0)
            parts.Add($"{state.Fps:0.#} fps");
        if (string.Equals(state.Phase, "end", StringComparison.OrdinalIgnoreCase))
            parts.Add("done");
        return string.Join(" · ", parts);
    }

    private static bool IsInterestingLogLine(string line)
    {
        if (line.Length < 4) return false;
        // Skip pure progress key=value noise for raw log (already summarized)
        if (line.Contains('=', StringComparison.Ordinal) &&
            (line.StartsWith("out_time", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("frame=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("fps=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("speed=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("bitrate=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("total_size=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("dup_frames=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("drop_frames=", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("stream_", StringComparison.OrdinalIgnoreCase) ||
             line.StartsWith("progress=", StringComparison.OrdinalIgnoreCase)))
            return false;

        return line.Contains("error", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Error", StringComparison.Ordinal)
               || line.Contains("warning", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Opening", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Output #", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Stream mapping", StringComparison.OrdinalIgnoreCase)
               || line.StartsWith("Input #", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Duration:", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ParseHms(string h, string m, string s)
    {
        if (!double.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out var hh)) return null;
        if (!double.TryParse(m, NumberStyles.Float, CultureInfo.InvariantCulture, out var mm)) return null;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var ss)) return null;
        return hh * 3600 + mm * 60 + ss;
    }

    private static string FormatHms(double sec)
    {
        if (sec < 0) sec = 0;
        var ts = TimeSpan.FromSeconds(sec);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
    }

    private static string TrimOneLine(string s, int n)
    {
        s = s.Trim();
        return s.Length <= n ? s : s[..n] + "…";
    }

    private static string TrimLog(string log) =>
        log.Length <= 600 ? log : log[^600..];

    private sealed class ProgressState
    {
        public double? TotalSec { get; set; }
        public double? OutSec { get; set; }
        public int? Frame { get; set; }
        public double? Fps { get; set; }
        public string? Speed { get; set; }
        public string? Phase { get; set; }
    }
}
