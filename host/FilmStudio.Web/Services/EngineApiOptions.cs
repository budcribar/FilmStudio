namespace FilmStudio.Web.Services;

public sealed class EngineApiOptions
{
    public const string SectionName = "EngineApi";

    /// <summary>Base URL of FilmStudio.Api (REST + SignalR hub).</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:5088";
}
