using PageToMovie.Adaptation.Conversion;
using Xunit;

namespace PageToMovie.Tests;

public sealed class AdaptationPromptRuntimeTokensTests
{
    [Fact]
    public void ApplyRuntimeTokens_null_means_unlimited()
    {
        var body = """
            1. Cover the book’s essential story in roughly {{TOTAL_RUNTIME_MINUTES}} minutes
               of finished film. Keep the adaptation tight; do not pad.
            Target about {{TOTAL_RUNTIME_MINUTES}} minutes of finished film.
            """;
        var outText = AdaptationPromptPack.ApplyRuntimeTokens(body, null);
        Assert.DoesNotContain("{{TOTAL_RUNTIME_MINUTES}}", outText);
        Assert.Contains("natural length", outText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roughly 10 minutes", outText);
        Assert.Contains(AdaptationPromptPack.UnlimitedRuntimeDirective.Split('—')[0].Trim(), outText);
    }

    [Fact]
    public void ApplyRuntimeTokens_positive_injects_number()
    {
        var body = "Target about {{TOTAL_RUNTIME_MINUTES}} minutes of finished film.";
        var outText = AdaptationPromptPack.ApplyRuntimeTokens(body, 12);
        Assert.Contains("12", outText);
        Assert.DoesNotContain("unlimited", outText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyRuntimeTokens_v4_directive_token()
    {
        var body = "RUNTIME\n{{RUNTIME_TARGET_DIRECTIVE}}\n";
        var unlimited = AdaptationPromptPack.ApplyRuntimeTokens(body, null);
        Assert.Contains("unlimited", unlimited, StringComparison.OrdinalIgnoreCase);
        var limited = AdaptationPromptPack.ApplyRuntimeTokens(body, 8);
        Assert.Contains("8 minutes", limited);
    }
}
