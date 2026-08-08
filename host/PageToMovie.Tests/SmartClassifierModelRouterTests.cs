using PageToMovie.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

// Reads SupportedModelCatalog.TaskRankings indirectly (via task-name ranking lookups) — must not
// run concurrently with tests that swap in a reduced synthetic catalog. See CatalogSerialCollection.
[Collection("catalog-serial")]
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