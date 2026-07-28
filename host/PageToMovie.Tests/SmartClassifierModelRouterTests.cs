using PageToMovie.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class SmartClassifierModelRouterTests
{
    [Fact]
    public void ResolveOptimalModelForTask_HonorsUserExplicitOverride()
    {
        var router = new SmartClassifierModelRouter(NullLogger<SmartClassifierModelRouter>.Instance);
        var chosen = router.ResolveOptimalModelForTask("beat_pacing", userConfiguredModel: "claude-sonnet-5");

        Assert.Equal("claude-sonnet-5", chosen);
    }

    [Fact]
    public void ResolveOptimalModelForTask_ReturnsRankedCandidateWhenKeysPresentOrFallback()
    {
        var router = new SmartClassifierModelRouter(NullLogger<SmartClassifierModelRouter>.Instance);
        var chosen = router.ResolveOptimalModelForTask("beat_pacing", userConfiguredModel: "auto");

        Assert.NotNull(chosen);
        Assert.NotEmpty(chosen);
    }
}