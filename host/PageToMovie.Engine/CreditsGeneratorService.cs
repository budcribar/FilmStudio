using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// End-credits plate via video generation API (no native ffmpeg).
/// Produces a short title-card style clip; client saves <c>assets/video/credits.mp4</c>
/// the same way as scene clips (proxy URL + hash registry).
/// </summary>
public class CreditsGeneratorService
{
    public const string RelativePath = "assets/video/credits.mp4";
    public const int CreditsDurationSeconds = 6;

    private readonly ProjectStore _projects;
    private readonly PageToMovieOptions _options;
    private readonly IVideoClient _video;
    private readonly MediaProxyTicketStore _mediaProxy;
    private readonly ILogger<CreditsGeneratorService> _logger;

    public CreditsGeneratorService(
        ProjectStore projects,
        IOptions<PageToMovieOptions> options,
        IVideoClient video,
        MediaProxyTicketStore mediaProxy,
        ILogger<CreditsGeneratorService> logger)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _options = options?.Value ?? new PageToMovieOptions();
        _video = video ?? throw new ArgumentNullException(nameof(video));
        _mediaProxy = mediaProxy ?? throw new ArgumentNullException(nameof(mediaProxy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// True when every blueprint scene has at least one present clip (server file or .client.json).
    /// </summary>
    public bool AreAllScenesComplete(string projectId)
    {
        var dir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(dir, "assets", "video");
        if (!Directory.Exists(videoDir) && !HasAnyClientMarkers(videoDir))
            return false;

        var plannedScenes = _projects.GetBlueprintSceneNumbers(projectId);
        if (plannedScenes is null || plannedScenes.Count == 0)
        {
            // Any clip present
            return Directory.Exists(videoDir) &&
                   (Directory.GetFiles(videoDir, "scene_*_clip_*.mp4").Any(f => new FileInfo(f).Length >= 1024) ||
                    Directory.GetFiles(videoDir, "scene_*_clip_*.mp4.client.json").Length > 0);
        }

        foreach (var sn in plannedScenes)
        {
            if (!SceneHasPresentVideo(projectId, videoDir, sn))
                return false;
        }

        return true;
    }

    private bool SceneHasPresentVideo(string projectId, string videoDir, int sn)
    {
        var comp = _projects.ResolveCompositePath(projectId, sn);
        if (comp is not null && File.Exists(comp) && new FileInfo(comp).Length >= 1024)
            return true;
        if (!Directory.Exists(videoDir))
            return false;
        foreach (var f in Directory.GetFiles(videoDir, $"scene_{sn:D2}_clip_*.mp4"))
        {
            if (new FileInfo(f).Length >= 1024 || File.Exists(f + ".client.json"))
                return true;
        }
        return Directory.GetFiles(videoDir, $"scene_{sn:D2}_clip_*.mp4.client.json").Length > 0;
    }

    private static bool HasAnyClientMarkers(string videoDir)
    {
        try
        {
            return Directory.Exists(videoDir) &&
                   Directory.GetFiles(videoDir, "*.client.json").Length > 0;
        }
        catch
        {
            return false;
        }
    }

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
                foreach (var line in File.ReadLines(fountainPath).Take(30))
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
                _logger.LogWarning(ex, "Failed to parse title/author from screenplay for {ProjectId}", projectId);
            }
        }

        return (title, author);
    }

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

    /// <summary>Video-gen prompt for a cinematic end-credits title card (readable text on screen).</summary>
    public string BuildCreditsVideoPrompt(string projectId)
    {
        var (title, author) = ExtractStoryTitleAndAuthor(projectId);
        var opts = _options.Credits ?? new CreditsOptions();
        var softName = string.IsNullOrWhiteSpace(opts.SoftwareName) ? "PageToMovie" : opts.SoftwareName.Trim();
        var softAuthor = string.IsNullOrWhiteSpace(opts.SoftwareAuthor) ? "Bud Cribar" : opts.SoftwareAuthor.Trim();
        var fairUse = string.IsNullOrWhiteSpace(opts.FairUseNotice)
            ? "Produced under Fair Use and Public Domain for Non-Commercial Creative Purposes."
            : opts.FairUseNotice.Trim();

        // Keep text sparse — models struggle with dense paragraphs on screen.
        var titleLine = SanitizeOnScreenText(title.ToUpperInvariant());
        var authorLine = SanitizeOnScreenText(author);
        var softLine = SanitizeOnScreenText($"{softName} · {softAuthor}");

        return
            "Cinematic end-credits title card, locked-off camera, no people, no faces, no logos of other brands. " +
            "Solid deep black background with subtle film grain. " +
            "Centered white sans-serif end-credit typography, high contrast, perfectly sharp and fully legible, " +
            "classic theatrical end-title card, slow gentle fade-in of text, gentle hold, soft fade. " +
            "On-screen text only (exact wording, line breaks as separate centered lines):\n" +
            $"{titleLine}\n" +
            $"Written by {authorLine}\n" +
            $"{softLine}\n" +
            $"{SanitizeOnScreenText(fairUse)}\n" +
            "No other text, no watermarks, no UI, no subtitles, 16:9 landscape, photoreal film look.";
    }

    private static string SanitizeOnScreenText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // Strip characters that confuse video prompts
        s = Regex.Replace(s.Trim(), @"\s+", " ");
        if (s.Length > 80) s = s[..77] + "…";
        return s;
    }

    /// <summary>
    /// Generate credits plate via video API. Returns same-origin proxy URL + relative path
    /// for client media folder (does not write MP4 on server).
    /// </summary>
    public async Task<CreditsGenClientHandoff?> GenerateCreditsForClientAsync(
        string projectId,
        string? resolution = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (!_video.IsConfigured)
            throw new InvalidOperationException("Video service not configured (API key).");

        if (!_options.Credits.AutoAppendCredits)
        {
            onProgress?.Invoke("Credits auto-append disabled in config.");
            return null;
        }

        var projectDir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        var (title, author) = ExtractStoryTitleAndAuthor(projectId);
        var textContent = FormatCreditsText(title, author, _options.Credits);
        var textFilePath = Path.Combine(videoDir, "_credits_text.txt");
        await File.WriteAllTextAsync(textFilePath, textContent, ct).ConfigureAwait(false);

        // Reuse if client already registered and text unchanged
        var marker = Path.Combine(videoDir, "credits.mp4.client.json");
        if (File.Exists(marker) &&
            File.GetLastWriteTimeUtc(marker) >= File.GetLastWriteTimeUtc(textFilePath).AddSeconds(-2))
        {
            onProgress?.Invoke("Credits plate already registered for this project.");
            return null;
        }

        var prompt = BuildCreditsVideoPrompt(projectId);
        var res = string.IsNullOrWhiteSpace(resolution) ? _options.DefaultResolution : resolution.Trim();
        if (string.IsNullOrWhiteSpace(res)) res = "480p";
        var model = string.IsNullOrWhiteSpace(_options.DefaultModel) ? "grok-imagine-video" : _options.DefaultModel;

        onProgress?.Invoke("Generating end-credits plate (video API)…");
        _logger.LogInformation("Credits video gen for {ProjectId}: {Title}", projectId, title);

        var requestId = await _video.SubmitGenerationAsync(
            prompt,
            CreditsDurationSeconds,
            res,
            model,
            ct,
            referenceImagePaths: null,
            startFrameImagePath: null,
            continueFromVideoPath: null).ConfigureAwait(false);

        onProgress?.Invoke($"Credits job {requestId}…");
        var url = await _video.PollForVideoUrlAsync(
            requestId,
            msg => onProgress?.Invoke($"Credits: {msg}"),
            ct).ConfigureAwait(false);

        var ticket = _mediaProxy.Issue(url, TimeSpan.FromMinutes(45));
        var clientUrl = $"/api/media/proxy/{ticket}";
        onProgress?.Invoke("Credits plate ready — save to media folder.");

        return new CreditsGenClientHandoff
        {
            ClientMediaUrl = clientUrl,
            ClientRelativePath = RelativePath,
            PromptPreview = prompt.Length > 200 ? prompt[..200] + "…" : prompt,
        };
    }

    /// <summary>Legacy entry: video-gen handoff only (no server file path).</summary>
    public async Task<string?> EnsureCreditsClipAsync(
        string projectId,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var handoff = await GenerateCreditsForClientAsync(projectId, null, onProgress, ct)
            .ConfigureAwait(false);
        // No server path — client must save. Return null so callers don't treat as local file.
        if (handoff is null) return null;
        _logger.LogInformation(
            "Credits handoff for {Project}: {Url}",
            projectId, handoff.ClientMediaUrl);
        return null;
    }
}

public sealed class CreditsGenClientHandoff
{
    public string ClientMediaUrl { get; set; } = "";
    public string ClientRelativePath { get; set; } = CreditsGeneratorService.RelativePath;
    public string? PromptPreview { get; set; }
}
