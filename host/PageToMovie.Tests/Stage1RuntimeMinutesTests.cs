using PageToMovie.Adaptation;
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
        // Natural read-aloud (2.5 wps) × staging → ~1–2 min for a short nursery rhyme (not 8–10, not ~3).
        Assert.InRange(minutes, 1, 2);
        Assert.Equal(analysis.SuggestedTotalMinutes, minutes);
    }

    [Fact]
    public void Override_is_clamped_like_production()
    {
        Assert.Equal(1, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 1)); // MinMinutes floor is 1
        Assert.Equal(180, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 999));
        Assert.Equal(12, BookTextAnalyzer.ResolveStage1RuntimeMinutes("hello world", 12));
    }

    [Fact]
    public void Short_literary_uses_speech_staging_not_eight_minute_floor()
    {
        // ~1200 words of short prose → narration speech path, not old words/120 floor of 8.
        var minutes = BookTextAnalyzer.SuggestStage1RuntimeMinutes("short", words: 1200, pages: 20);
        Assert.InRange(minutes, 5, 20);
        Assert.NotEqual(8, minutes); // old floor
    }
}
