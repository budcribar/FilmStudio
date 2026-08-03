using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class Stage1RuntimeMinutesTests
{
    [Fact]
    public void Mary_nursery_rhyme_tracks_slow_read_aloud_not_eight_or_ten_minutes()
    {
        var book = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "MaryHadALittleLamb.txt"));
        var analysis = BookTextAnalyzer.Analyze(book);
        var minutes = BookTextAnalyzer.ResolveStage1RuntimeMinutes(book);

        Assert.InRange(analysis.TextWords, 80, 200);
        // Slow read of this verse is ~2 minutes; do not force 8–10 min short-film padding.
        Assert.InRange(minutes, 2, 3);
        Assert.Equal(analysis.SuggestedTotalMinutes, minutes);
    }

    [Fact]
    public void Very_short_word_count_suggests_two_minutes()
    {
        // ~140 words → 140/70 ≈ 2
        Assert.Equal(2, BookTextAnalyzer.SuggestStage1RuntimeMinutes("short", words: 140, pages: 1));
        Assert.Equal(2, BookTextAnalyzer.SuggestStage1RuntimeMinutes("picture_book", words: 100, pages: 1));
    }

    [Fact]
    public void Override_is_clamped_like_production()
    {
        Assert.Equal(2, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 1));
        Assert.Equal(180, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 999));
        Assert.Equal(12, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 12));
    }

    [Fact]
    public void Short_prose_no_longer_floors_at_eight()
    {
        // 1200 words / 120 = 10; mid short-story band
        Assert.Equal(10, BookTextAnalyzer.SuggestStage1RuntimeMinutes("short", words: 1200, pages: 20));
        // 600 words would have been floored at 8 under the old rule (600/120=5 → clamp 8)
        Assert.Equal(5, BookTextAnalyzer.SuggestStage1RuntimeMinutes("short", words: 600, pages: 20));
    }
}
