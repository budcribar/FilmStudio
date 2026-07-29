using System.Text.RegularExpressions;

namespace PageToMovie.Core.Utils;

/// <summary>
/// Singleton catalog of pre-compiled Regex patterns shared across services.
/// Eliminates redundant compilations of common patterns (whitespace collapse, dot collapse, HTML tags).
/// </summary>
public static class CommonRegex
{
    /// <summary>Matches consecutive whitespace characters (\s+).</summary>
    public static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Matches consecutive dots or dots with surrounding spaces (\s*\.\s*\.+).</summary>
    public static readonly Regex DotCollapse = new(@"\s*\.\s*\.+", RegexOptions.Compiled);

    /// <summary>Matches standard HTML tags (&lt;[^&gt;]+&gt;).</summary>
    public static readonly Regex HtmlTags = new(@"<[^>]+>", RegexOptions.Compiled);
}
