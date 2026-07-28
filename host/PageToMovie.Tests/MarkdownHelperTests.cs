using PageToMovie.Web.Services;
using Xunit;

namespace PageToMovie.Tests;

public class MarkdownHelperTests
{
    [Fact]
    public void Render_RendersMarkdownToHtmlMarkupString()
    {
        // Arrange
        var markdown = "### Executive Overview\n- **Score**: 9/10\n- Great pacing!";

        // Act
        var markup = MarkdownHelper.Render(markdown);
        var html = markup.Value;

        // Assert
        Assert.Contains("<h3>Executive Overview</h3>", html);
        Assert.Contains("<strong>Score</strong>: 9/10", html);
        Assert.Contains("<li>Great pacing!</li>", html);
    }

    [Fact]
    public void Render_SanitizesRawLlmHtmlParagraphTags_PreventsLiteralParagraphText()
    {
        // Arrange: LLM raw payload containing literal <p> and </p> wrappers
        var rawLlmOutput = "<p>Strong spatial progression from single medium shot at table with candle.</p>";

        // Act
        var markup = MarkdownHelper.Render(rawLlmOutput);
        var html = markup.Value;

        // Assert: Should NOT escape into &lt;p&gt; text inside rendered HTML
        Assert.DoesNotContain("&lt;p&gt;", html);
        Assert.DoesNotContain("&lt;/p&gt;", html);
        Assert.Contains("Strong spatial progression from single medium shot", html);
    }

    [Fact]
    public void StripHtml_StripsAllHtmlTagsAndDecodesEntities()
    {
        // Arrange
        var htmlInput = "<p>Camera movement &amp; framing locked <strong>well</strong>.</p>";

        // Act
        var plainText = MarkdownHelper.StripHtml(htmlInput);

        // Assert
        Assert.Equal("Camera movement & framing locked well.", plainText);
    }
}
