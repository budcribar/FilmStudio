namespace PageToMovie.Engine.ModelBacked;

/// <summary>
/// Shared telemetry fields for the coverage-retry classifier run results (<see cref="AmbientSfxClassifyResult"/>,
/// <see cref="SilentBeatClassifyResult"/>). Each concrete result adds its own item-count property and
/// <c>ToMetaDict()</c> serialization — those intentionally differ (key names, included fields) and stay
/// per-class.
/// </summary>
public abstract class ClassifierRunResultBase
{
    public bool Enabled { get; set; }
    public string PromptVersion { get; set; } = "";
    public string Model { get; set; } = "";
    public double Temperature { get; set; }
    public int AiCount { get; set; }
    public int FallbackCount { get; set; }
    public int Attempts { get; set; }
    public int ChatCalls { get; set; }
    public string Note { get; set; } = "";
    public string? LastError { get; set; }
}
