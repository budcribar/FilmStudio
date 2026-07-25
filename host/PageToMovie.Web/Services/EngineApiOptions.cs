namespace PageToMovie.Web.Services;

public sealed class EngineApiOptions
{
    public const string SectionName = "EngineApi";

    /// <summary>
    /// Base URL of PageToMovie.Api for browser HttpClient + SignalR.
    /// Empty (default): same origin as the Blazor WASM app (correct for unified Api host).
    /// Set only when the API is on another origin, e.g. local split ports.
    /// Env: EngineApi__BaseUrl
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Origin the browser should use for &lt;img&gt;/&lt;video&gt; media.
    /// <list type="bullet">
    /// <item>Empty (default): root-relative <c>/api/...</c> — correct for unified host.</item>
    /// <item>Set when media is served from a different origin than the UI.</item>
    /// </list>
    /// Env: EngineApi__BrowserMediaBaseUrl
    /// </summary>
    public string BrowserMediaBaseUrl { get; set; } = "";

    /// <summary>
    /// HTTP timeout for Web → API calls. Book → Fountain multi-chunk can run many minutes;
    /// keep this at or above the API chat client timeout (default 20 min).
    /// Env: EngineApi__TimeoutMinutes
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;
}
