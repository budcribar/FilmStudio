using Markdig;
using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace PageToMovie.Web.Services;

public static class MarkdownHelper
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseListExtras()
        .Build();

    /// <summary>
    /// Render Markdown or AI text payload to MarkupString.
    /// Automatically strips raw HTML paragraph tags (<p>, </p>, <br>) emitted by LLMs so they render as clean HTML.
    /// </summary>
    public static MarkupString Render(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new MarkupString("");

        var text = input.Trim();

        // If the AI model returned raw HTML tags (e.g. <p>...</p> or <br/>), clean raw paragraph tags
        // so Markdig doesn't double-escape them into literal &lt;p&gt; text.
        text = Regex.Replace(text, @"^<(p|div)>\s*", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*</(p|div)>$", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(p|div)>\s*", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*</(p|div)>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        var html = Markdown.ToHtml(text, Pipeline);
        return new MarkupString(html);
    }

    /// <summary>
    /// Strip all HTML tags for plain-text contexts (e.g. collapsed headers, tooltips, title tags).
    /// </summary>
    public static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var text = Regex.Replace(input, @"<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
