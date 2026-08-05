using System.Collections.Concurrent;
using System.Diagnostics;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Fakes;

/// <summary>
/// Fake video client: delay + real MP4 fixtures (1s / 5s / 10s).
/// Extensions optionally concat previous clip + new segment when ffmpeg is on PATH.
/// </summary>
public sealed class FakeGrokVideoClient : IVideoClient
{
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<FakeGrokVideoClient> _log;
    private readonly ConcurrentDictionary<string, string> _pending = new();
    private int _submitCount;
    private int _clipRoundRobin;

    public FakeGrokVideoClient(IOptions<PageToMovieOptions> opts, ILogger<FakeGrokVideoClient> log)
    {
        _opts = opts.Value;
        _log = log;
    }

    public bool IsConfigured => true;

    public async Task<string> SubmitGenerationAsync(
        string prompt,
        int durationSeconds,
        string resolution,
        string model,
        CancellationToken ct,
        IReadOnlyList<string>? referenceImagePaths = null,
        string? startFrameImagePath = null,
        string? continueFromVideoPath = null)
    {
        var n = Interlocked.Increment(ref _submitCount);
        var fakes = _opts.Fakes ?? new FakesOptions();
        if (fakes.RateLimitEveryN > 0 && n % fakes.RateLimitEveryN == 0)
            throw new InvalidOperationException("Fake 429: rate limit (RateLimitEveryN)");

        await Task.Delay(Math.Max(0, fakes.VideoDelayMs), ct);

        if (fakes.FailRate > 0 && Random.Shared.NextDouble() < fakes.FailRate)
            throw new InvalidOperationException("Fake video generation failed (FailRate)");

        var id = "fake-" + Guid.NewGuid().ToString("N")[..12];
        var fixture = ResolveFixturePath(fakes.VideoMode, durationSeconds, Interlocked.Increment(ref _clipRoundRobin));
        // Mark extensions so Download can concat prev + new segment when possible
        if (!string.IsNullOrWhiteSpace(continueFromVideoPath) && File.Exists(continueFromVideoPath))
            _pending[id] = "extend:" + continueFromVideoPath + "|" + fixture;
        else
            _pending[id] = fixture;
        _log.LogInformation(
            "Fake video submit {Id} duration={Dur}s fixture={Fixture} refs={Refs} startFrame={Start} continue={Cont} promptLen={Len}",
            id, durationSeconds, Path.GetFileName(fixture),
            referenceImagePaths?.Count ?? 0,
            startFrameImagePath is null ? "-" : Path.GetFileName(startFrameImagePath),
            continueFromVideoPath is null ? "-" : Path.GetFileName(continueFromVideoPath),
            prompt?.Length ?? 0);
        return id;
    }

    public async Task<string> PollForVideoUrlAsync(
        string requestId,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        onProgress?.Invoke("status=pending (fake)");
        await Task.Delay(Math.Max(50, (_opts.Fakes?.VideoDelayMs ?? 0) / 4), ct);
        onProgress?.Invoke("status=done (fake)");
        if (!_pending.TryGetValue(requestId, out var fixture))
            throw new InvalidOperationException($"Unknown fake request_id {requestId}");
        return "fake-fixture:" + fixture;
    }

    public async Task DownloadToFileAsync(string url, string destPath, CancellationToken ct)
    {
        await Task.Yield();
        string token;
        if (url.StartsWith("fake-fixture:", StringComparison.OrdinalIgnoreCase))
            token = url["fake-fixture:".Length..];
        else if (_pending.Values.FirstOrDefault() is { } f)
            token = f;
        else
            token = ResolveFixturePath(_opts.Fakes?.VideoMode, 10, 0);

        string? prevPath = null;
        string fixture = token;
        if (token.StartsWith("extend:", StringComparison.OrdinalIgnoreCase))
        {
            var pipe = token.IndexOf('|');
            if (pipe > 0)
            {
                prevPath = token["extend:".Length..pipe];
                fixture = token[(pipe + 1)..];
            }
            else
            {
                fixture = token["extend:".Length..];
            }
        }

        if (!File.Exists(fixture))
            throw new FileNotFoundException(
                "Fake video fixture missing. Expected real MP4 under PageToMovie.Fakes/Fixtures/ (clip_tiny_1s.mp4, clip_5s.mp4, clip_merge_10s.mp4).",
                fixture);

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        double seconds;
        if (!string.IsNullOrWhiteSpace(prevPath) && File.Exists(prevPath) && TryConcat(prevPath, fixture, destPath))
        {
            seconds = ProbeDurationSeconds(destPath)
                      ?? (ProbeDurationSeconds(prevPath) ?? 0) + (ProbeDurationSeconds(fixture) ?? 0);
            _log.LogInformation("Fake extend concat {Prev} + {Fixture} → {Path}", Path.GetFileName(prevPath), Path.GetFileName(fixture), destPath);
        }
        else
        {
            File.Copy(fixture, destPath, overwrite: true);
            seconds = ProbeDurationSeconds(destPath) ?? GuessDurationFromName(fixture);
        }

        try
        {
            await MediaDurationProbe.WriteDurationSidecarAsync(destPath, seconds, ct);
        }
        catch { /* ignore */ }

        _log.LogInformation("Fake download {Bytes} bytes ({Sec:0.##}s) → {Path}", new FileInfo(destPath).Length, seconds, destPath);
    }

    /// <summary>Pick fixture by duration band; rotate scene-colored clips when available.</summary>
    public static string ResolveFixturePath(string? videoMode, int durationSeconds, int roundRobin = 0)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var light = Path.Combine(fixtures, "clip_tiny_1s.mp4");
        var mid = Path.Combine(fixtures, "clip_5s.mp4");
        var merge = Path.Combine(fixtures, "clip_merge_10s.mp4");

        if (string.Equals(videoMode, "LoadLight", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(light)) return light;
            if (File.Exists(mid)) return mid;
            if (File.Exists(merge)) return merge;
        }

        // Prefer scene-varied 3s clips for multi-shot soaks
        if (Directory.Exists(fixtures) && durationSeconds > 0 && durationSeconds <= 4)
        {
            var scenes = Directory.GetFiles(fixtures, "clip_scene_*_3s.mp4");
            if (scenes.Length > 0)
                return scenes[Math.Abs(roundRobin) % scenes.Length];
        }

        string preferred;
        if (durationSeconds <= 2)
            preferred = light;
        else if (durationSeconds <= 6)
            preferred = File.Exists(mid) ? mid : merge;
        else
            preferred = merge;

        if (File.Exists(preferred)) return preferred;
        if (File.Exists(merge)) return merge;
        if (File.Exists(mid)) return mid;
        if (File.Exists(light)) return light;

        if (Directory.Exists(fixtures))
        {
            var any = Directory.GetFiles(fixtures, "*.mp4").FirstOrDefault();
            if (any is not null) return any;
        }

        return preferred;
    }

    static double GuessDurationFromName(string path)
    {
        var n = Path.GetFileName(path).ToLowerInvariant();
        if (n.Contains("10s") || n.Contains("merge")) return 10;
        if (n.Contains("5s")) return 5;
        if (n.Contains("3s")) return 3;
        if (n.Contains("1s") || n.Contains("tiny")) return 1;
        return 5;
    }

    static double? ProbeDurationSeconds(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            if (p.ExitCode == 0 && double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var sec))
                return sec;
        }
        catch { /* optional */ }
        return null;
    }

    static bool TryConcat(string prev, string next, string dest)
    {
        try
        {
            var list = Path.Combine(Path.GetTempPath(), "ptm-fake-concat-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
            var prevEsc = Path.GetFullPath(prev).Replace("'", "'\\''");
            var nextEsc = Path.GetFullPath(next).Replace("'", "'\\''");
            File.WriteAllText(list, $"file '{prevEsc}'\nfile '{nextEsc}'\n");
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -f concat -safe 0 -i \"{list}\" -c copy \"{dest}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(30000);
            try { File.Delete(list); } catch { /* ignore */ }
            return p.ExitCode == 0 && File.Exists(dest) && new FileInfo(dest).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
