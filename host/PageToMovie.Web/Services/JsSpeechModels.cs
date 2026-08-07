namespace PageToMovie.Web.Services;

/// <summary>
/// Shared JS interop DTOs for <c>PageToMovieFfmpeg.detectSpeechSegmentsAsync</c>. The browser side
/// returns the whole-clip duration plus the detected speech windows; consumers filter/align them.
/// </summary>
internal sealed class JsSpeechDetectResult
{
    public bool Success { get; set; }
    public double TotalSec { get; set; }
    public List<JsSpeechWindow>? Segments { get; set; }
    public string? Error { get; set; }
}

/// <summary>One detected speech window's time span (seconds) as reported by the browser detector.</summary>
internal sealed class JsSpeechWindow
{
    public double StartSec { get; set; }
    public double EndSec { get; set; }
}
