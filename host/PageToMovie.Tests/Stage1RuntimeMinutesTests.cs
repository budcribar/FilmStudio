using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class Stage1RuntimeMinutesTests
{
    [Fact]
    public void Mary_nursery_rhyme_uses_analyzer_not_fixed_ten()
    {
        var book = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "MaryHadALittleLamb.txt"));
        var minutes = BookTextAnalyzer.ResolveStage1RuntimeMinutes(book);
        Assert.InRange(minutes, 3, 180);
        // Short text floors at 8 for "short" kind — not the old benchmark default of 10.
        Assert.Equal(BookTextAnalyzer.Analyze(book).SuggestedTotalMinutes, minutes);
        Assert.Equal(Math.Clamp(BookTextAnalyzer.Analyze(book).SuggestedTotalMinutes, 3, 180), minutes);
    }

    [Fact]
    public void Override_is_clamped_like_production()
    {
        Assert.Equal(3, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 1));
        Assert.Equal(180, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 999));
        Assert.Equal(12, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 12));
    }
}
