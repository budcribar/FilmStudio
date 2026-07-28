using Markdig;
using Microsoft.AspNetCore.Components;

namespace PageToMovie.Web.Services;

public static class MarkdownHelper
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseListExtras()
        .Build();

    public static MarkupString Render(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkupString("");

        var html = Markdown.ToHtml(markdown, Pipeline);
        return new MarkupString(html);
    }
}
