namespace FilmStudio.Core.Options;

public sealed class FilmStudioOptions
{
    public const string SectionName = "FilmStudio";

    /// <summary>Repo / workspace root containing projects/ and prompts/.</summary>
    public string WorkspaceRoot { get; set; } = "";

    public string DefaultModel { get; set; } = "grok-imagine-video";
    public string DefaultImageModel { get; set; } = "grok-imagine-image-quality";
    /// <summary>
    /// Image backend for character portraits: grok | gemini.
    /// Also inferred from DefaultImageModel / project image_model_name when empty.
    /// </summary>
    public string ImageProvider { get; set; } = "grok";
    public string DefaultResolution { get; set; } = "480p";
    public int DefaultDurationSeconds { get; set; } = 6;
    public int GrokPollSeconds { get; set; } = 5;
    public int GrokTimeoutSeconds { get; set; } = 900;

    /// <summary>
    /// ffmpeg executable for scene remux / WIP.
    /// Empty → auto: NuGet Soenneker Resources/ffmpeg.exe, then PATH.
    /// Can be an absolute path or path relative to the API output directory.
    /// </summary>
    public string FfmpegPath { get; set; } = "";

    /// <summary>When true, DI registers fake Grok clients (no xAI spend).</summary>
    public bool UseFakes { get; set; }

    public CapacityOptions Capacity { get; set; } = new();
    public FakesOptions Fakes { get; set; } = new();
}

/// <summary>Server-side concurrency caps (Phase A+; multi-worker in later phases).</summary>
public sealed class CapacityOptions
{
    /// <summary>Max concurrent API video jobs (Phase A still runs 1 at a time if gate is 1).</summary>
    public int MaxVideoInFlight { get; set; } = 1;
    public int MaxVideoInFlightPerUser { get; set; } = 1;
    public int MaxFfmpegInFlight { get; set; } = 2;
    public int MaxQueuePerUser { get; set; } = 5;
}

/// <summary>Fake client knobs when <see cref="FilmStudioOptions.UseFakes"/> is true.</summary>
public sealed class FakesOptions
{
    /// <summary>MergeRealistic | LoadLight</summary>
    public string VideoMode { get; set; } = "MergeRealistic";
    public int VideoDelayMs { get; set; } = 200;
    /// <summary>0–1 probability of synthetic failure after delay.</summary>
    public double FailRate { get; set; }
    /// <summary>Throw rate-limit every N submits (0 = never).</summary>
    public int RateLimitEveryN { get; set; }
}
