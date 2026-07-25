using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PageToMovie.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Generates an end-credits video clip (credits.mp4) for a project and appends it
/// when all scenes in the screenplay/blueprint are completed.
/// Credits include story title, author, filmmaking software name (PageToMovie),
/// software author (Nick), repository link, and fair use notice.
/// </summary>
public class CreditsGeneratorService
{
    private readonly ProjectStore _projects;
    private readonly PageToMovieOptions _options;
    private readonly ILogger<CreditsGeneratorService> _logger;

    public CreditsGeneratorService(
        ProjectStore projects,
        IOptions<PageToMovieOptions> options,
        ILogger<CreditsGeneratorService> logger)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _options = options?.Value ?? new PageToMovieOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check whether all planned blueprint scenes for a project have completed video files on disk.
    /// </summary>
    public bool AreAllScenesComplete(string projectId)
    {
        var dir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(dir, "assets", "video");
        if (!Directory.Exists(videoDir)) return false;

        var plannedScenes = _projects.GetBlueprintSceneNumbers(projectId);
        if (plannedScenes is null || plannedScenes.Count == 0)
        {
            // Fallback: check if at least one scene_01.mp4 exists
            return File.Exists(Path.Combine(videoDir, "scene_01.mp4"));
        }

        foreach (var sn in plannedScenes)
        {
            var compPath = _projects.ResolveCompositePath(projectId, sn);
            if (compPath is null || !File.Exists(compPath) || new FileInfo(compPath).Length < 1024)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extract story title and author from screenplay.fountain or project metadata.
    /// </summary>
    public (string Title, string Author) ExtractStoryTitleAndAuthor(string projectId)
    {
        var dir = _projects.GetProjectDir(projectId);
        string title = projectId;
        string author = "Public Domain / Source Material";

        var fountainPath = Path.Combine(dir, "source", "screenplay.fountain");
        if (File.Exists(fountainPath))
        {
            try
            {
                var lines = File.ReadLines(fountainPath).Take(30).ToList();
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = trimmed.Substring(6).Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                            title = val;
                    }
                    else if (trimmed.StartsWith("Author:", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.StartsWith("Authors:", StringComparison.OrdinalIgnoreCase))
                    {
                        var idx = trimmed.IndexOf(':');
                        var val = trimmed.Substring(idx + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                            author = val;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse title/author from screenplay.fountain for project {ProjectId}", projectId);
            }
        }

        return (title, author);
    }

    /// <summary>
    /// Formats the multiline text displayed on the credits title card.
    /// </summary>
    public string FormatCreditsText(string title, string author, CreditsOptions? opts = null)
    {
        opts ??= _options.Credits;
        var t = string.IsNullOrWhiteSpace(title) ? "MOTION PICTURE" : title.Trim().ToUpperInvariant();
        var a = string.IsNullOrWhiteSpace(author) ? "Public Domain / Adapted Work" : author.Trim();
        var softName = string.IsNullOrWhiteSpace(opts.SoftwareName) ? "PageToMovie" : opts.SoftwareName.Trim();
        var softAuthor = string.IsNullOrWhiteSpace(opts.SoftwareAuthor) ? "Bud Cribar" : opts.SoftwareAuthor.Trim();
        var repo = string.IsNullOrWhiteSpace(opts.RepositoryUrl) ? "https://github.com/budcribar/PageToMovie" : opts.RepositoryUrl.Trim();
        var fairUse = string.IsNullOrWhiteSpace(opts.FairUseNotice)
            ? "Produced under Fair Use and Public Domain for Non-Commercial Creative Purposes."
            : opts.FairUseNotice.Trim();

        return $"{t}\n" +
               $"Written by {a}\n\n" +
               $"Filmmaking Software: {softName}\n" +
               $"Software Author: {softAuthor}\n" +
               $"Repository: {repo}\n\n" +
               $"{fairUse}";
    }

    /// <summary>
    /// Ensures credits.mp4 is generated and up-to-date in assets/video/credits.mp4.
    /// </summary>
    public async Task<string?> EnsureCreditsClipAsync(
        string projectId,
        string ffmpegExePath,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var projectDir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        var creditsMoviePath = Path.Combine(videoDir, "credits.mp4");
        var (title, author) = ExtractStoryTitleAndAuthor(projectId);
        var textContent = FormatCreditsText(title, author, _options.Credits);

        var textFilePath = Path.Combine(videoDir, "_credits_text.txt");
        await File.WriteAllTextAsync(textFilePath, textContent, ct).ConfigureAwait(false);

        if (File.Exists(creditsMoviePath) && new FileInfo(creditsMoviePath).Length < 1024)
        {
            try { File.Delete(creditsMoviePath); } catch { }
        }

        // If credits.mp4 already exists and is newer than text file, reuse it
        if (File.Exists(creditsMoviePath) &&
            new FileInfo(creditsMoviePath).Length >= 1024 &&
            new FileInfo(creditsMoviePath).LastWriteTimeUtc >= new FileInfo(textFilePath).LastWriteTimeUtc.AddSeconds(-2))
        {
            return creditsMoviePath;
        }

        _ = ffmpegExePath;
        onProgress?.Invoke("Skipping end credits clip (no native ffmpeg — browser stitch only).");
        _logger.LogInformation("Credits clip skipped for {ProjectId} (native ffmpeg removed)", projectId);
        return null;
    }

    /// <summary>
    /// Finds a valid system font for FFmpeg drawtext rendering across Windows, Linux, and macOS.
    /// </summary>
    public static string? ResolveSystemFontPath()
    {
        var fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var candidates = new[]
        {
            Path.Combine(fontsFolder, "arial.ttf"),
            Path.Combine(fontsFolder, "segoeui.ttf"),
            Path.Combine(fontsFolder, "tahoma.ttf"),
            Path.Combine(fontsFolder, "calibri.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "/System/Library/Fonts/Helvetica.ttc",
        };

        foreach (var font in candidates)
        {
            if (!string.IsNullOrWhiteSpace(font) && File.Exists(font))
                return font;
        }

        return null;
    }
}
