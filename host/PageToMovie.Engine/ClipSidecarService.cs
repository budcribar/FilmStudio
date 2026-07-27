using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;

namespace PageToMovie.Engine;

/// <summary>
/// Writes and parses structured <c>.clip.json</c> sidecar manifests alongside <c>.mp4</c> files.
/// Includes script dialogue/action text, visual prompt, model, resolution, duration, SHA-256 hash,
/// and UTC generation timestamp for timezone-immune versioning.
/// </summary>
public sealed class ClipSidecarService
{
    private static readonly JsonSerializerOptions JsonOpts = JsonDefaults.IndentedCaseInsensitive;

    private readonly ProjectStore _projects;
    private readonly ILogger<ClipSidecarService> _log;

    public ClipSidecarService(ProjectStore projects, ILogger<ClipSidecarService>? log = null)
    {
        _projects = projects;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClipSidecarService>.Instance;
    }

    public static string GetSidecarPathForMp4(string mp4Path) =>
        Path.ChangeExtension(mp4Path, ".clip.json");

    /// <summary>
    /// Write a .clip.json sidecar alongside an MP4 video file.
    /// </summary>
    public async Task<string> WriteSidecarAsync(
        string projectDir,
        int scene,
        int clip,
        string prompt,
        string scriptText,
        string model,
        string resolution,
        double durationSeconds,
        string sha256,
        long sizeBytes,
        string? mp4FileName = null,
        CancellationToken ct = default)
    {
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        var fileName = string.IsNullOrWhiteSpace(mp4FileName)
            ? $"scene_{scene:D2}_clip_{clip:D2}.mp4"
            : mp4FileName.Trim();

        var mp4Path = Path.Combine(videoDir, fileName);
        var sidecarPath = GetSidecarPathForMp4(mp4Path);

        var projectId = Path.GetFileName(projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var sidecar = new Dictionary<string, object?>
        {
            ["schema_version"] = "clip_sidecar.v1",
            ["project_id"] = projectId,
            ["scene"] = scene,
            ["clip"] = clip,
            ["script_text"] = scriptText ?? "",
            ["visual_prompt"] = prompt ?? "",
            ["model"] = model ?? "",
            ["resolution"] = resolution ?? "",
            ["duration_seconds"] = Math.Round(durationSeconds, 2),
            ["sha256"] = MediaRegistryService.NormalizeSha256(sha256),
            ["size_bytes"] = sizeBytes,
            ["created_at_utc"] = DateTime.UtcNow.ToString("o"),
        };

        var json = JsonSerializer.Serialize(sidecar, JsonOpts);
        await File.WriteAllTextAsync(sidecarPath, json + "\n", ct).ConfigureAwait(false);
        _log.LogInformation("Written clip sidecar manifest → {Path}", sidecarPath);
        return sidecarPath;
    }

    /// <summary>
    /// Ensure all MP4 files under <c>assets/video/</c> have a corresponding <c>.clip.json</c> sidecar.
    /// Uses prompt text files and blueprint metadata to backfill missing sidecars during Export.
    /// </summary>
    public async Task<int> EnsureAllSidecarsExistAsync(string projectDir, CancellationToken ct = default)
    {
        var videoDir = Path.Combine(projectDir, "assets", "video");
        if (!Directory.Exists(videoDir))
            return 0;

        var videoFiles = Directory.EnumerateFiles(videoDir, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mp4.client.json", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (videoFiles.Count == 0)
            return 0;

        var createdCount = 0;
        var projectId = Path.GetFileName(projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // Load blueprint if available for script text
        var blueprintPath = Path.Combine(projectDir, "blueprint.clips.grok.json");
        JsonDocument? blueprintDoc = null;
        if (File.Exists(blueprintPath))
        {
            try { blueprintDoc = JsonDocument.Parse(await File.ReadAllTextAsync(blueprintPath, ct).ConfigureAwait(false)); }
            catch { /* ignore */ }
        }

        using (blueprintDoc)
        {
            foreach (var file in videoFiles)
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file);

                // Strip .client.json marker suffix if present
                if (name.EndsWith(".client.json", StringComparison.OrdinalIgnoreCase))
                    name = name[..^12];

                var mp4Path = Path.Combine(Path.GetDirectoryName(file)!, name);
                var sidecarPath = GetSidecarPathForMp4(mp4Path);
                if (File.Exists(sidecarPath))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(
                    name, @"scene_?(\d+)(?:_clip_?(\d+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var scene = match.Success && int.TryParse(match.Groups[1].Value, out var s) ? s : 1;
                var clip = match.Success && match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var c) ? c : 1;

                // Load prompt text if exists
                var promptPath = Path.Combine(videoDir, "prompts", $"S{scene:D2}C{clip:D2}.txt");
                var promptText = "";
                if (File.Exists(promptPath))
                {
                    try { promptText = await File.ReadAllTextAsync(promptPath, ct).ConfigureAwait(false); }
                    catch { /* ignore */ }
                }

                // Compute sha256 and size if local file exists
                var fi = new FileInfo(file);
                var sha256 = File.Exists(mp4Path) ? await MediaRegistryService.HashFileAsync(mp4Path, ct).ConfigureAwait(false) : "";

                await WriteSidecarAsync(
                    projectDir,
                    scene,
                    clip,
                    promptText,
                    scriptText: "",
                    model: "grok-imagine-video",
                    resolution: "480p",
                    durationSeconds: 6.0,
                    sha256: sha256,
                    sizeBytes: fi.Length,
                    mp4FileName: name,
                    ct: ct).ConfigureAwait(false);

                createdCount++;
            }
        }

        return createdCount;
    }
}
