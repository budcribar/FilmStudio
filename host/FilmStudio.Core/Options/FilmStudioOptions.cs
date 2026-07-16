namespace FilmStudio.Core.Options;

public sealed class FilmStudioOptions
{
    public const string SectionName = "FilmStudio";

    /// <summary>Repo / workspace root containing projects/ and prompts/.</summary>
    public string WorkspaceRoot { get; set; } = "";

    /// <summary>Native = C# Grok client; PythonBridge = call host/python_engine_api worker path via process (optional).</summary>
    public string EngineMode { get; set; } = "Native";

    public string DefaultModel { get; set; } = "grok-imagine-video";
    public string DefaultImageModel { get; set; } = "grok-imagine-image-quality";
    public string DefaultResolution { get; set; } = "480p";
    public int DefaultDurationSeconds { get; set; } = 6;
    public int GrokPollSeconds { get; set; } = 5;
    public int GrokTimeoutSeconds { get; set; } = 900;

    /// <summary>Python executable for Stage 1 / other bridge scripts.</summary>
    public string PythonExecutable { get; set; } = "python";
}
