using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class AdaptationDensityTests
{
    [Fact]
    public void Mary_natural_is_about_two_to_three_minutes_high_density()
    {
        var book = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "MaryHadALittleLamb.txt"));
        var e = AdaptationDensity.EstimateNatural(book);

        Assert.InRange(e.NaturalFilmMinutes, 2, 3);
        Assert.Equal("short_speech_x_staging", e.Method);
        // High minutes-per-1k-words (short verse filmed near full speech length)
        Assert.True(e.MinutesPerThousandWords > 8, $"δ={e.MinutesPerThousandWords}");
        Assert.True(e.TemporalCompressionRatio > 0.8, $"τ={e.TemporalCompressionRatio}");
        Assert.Null(AdaptationDensity.SuggestReducedBenchmarkMinutes(e));
    }

    [Fact]
    public void Nick_scale_novel_lands_feature_band_not_audiobook()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "books", "Nick_and_Me.txt"));
        if (!File.Exists(path))
        {
            // Fallback: synthetic ~50k-word novel mass
            var synth = string.Join(' ', Enumerable.Repeat("Nick walked home and thought about the day.", 6000));
            var eSynth = AdaptationDensity.EstimateNatural(synth, bookKind: "novel");
            Assert.InRange(eSynth.NaturalFilmMinutes, 40, 180);
            Assert.True(eSynth.TemporalCompressionRatio < 0.6);
            return;
        }

        var e = AdaptationDensity.EstimateNatural(File.ReadAllText(path));
        Assert.Equal("novel", e.BookKind);
        // Feature / limited-series band — not 340+ min full-prose speech
        Assert.InRange(e.NaturalFilmMinutes, 80, 180);
        Assert.True(e.AudiobookMinutes > 300, "Nick should be multi-hour as audiobook");
        Assert.True(e.TemporalCompressionRatio < 0.5, $"τ={e.TemporalCompressionRatio} should show heavy compression");
        Assert.InRange(e.MinutesPerThousandWords, 1.2, 3.5);

        var reduced = AdaptationDensity.SuggestReducedBenchmarkMinutes(e);
        Assert.NotNull(reduced);
        Assert.True(reduced < e.NaturalFilmMinutes);
        Assert.InRange(reduced!.Value, 20, e.NaturalFilmMinutes - 5);
    }

    [Fact]
    public void Density_definition_is_minutes_per_thousand_words()
    {
        var e = AdaptationDensity.EstimateNatural(
            string.Join(' ', Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 200)),
            bookKind: "short");
        var expected = e.NaturalFilmMinutes / (e.SourceWords / 1000.0);
        Assert.Equal(Math.Round(expected, 2), e.MinutesPerThousandWords);
    }
}
