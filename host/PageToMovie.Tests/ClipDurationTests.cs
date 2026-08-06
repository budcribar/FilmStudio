using System.Text.Json;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// ClipDuration is the single source of truth for a clip's duration. The tolerant number read
/// (TryReadNumericSeconds) backs the pacing estimator and the prompt builder; the effective-duration
/// composition (Resolve: numeric → mm:ss timestamp span → default) backs cost reporting. Every site
/// must resolve identically or a clip's runtime — and its cost — diverges from what actually renders.
/// </summary>
public class ClipDurationTests
{
    private static JsonElement El(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ReadNumeric_accepts_positive_int()
    {
        Assert.True(ClipDuration.TryReadNumericSeconds(El("""{ "duration_seconds": 7 }"""), out var s));
        Assert.Equal(7, s);
    }

    [Fact]
    public void ReadNumeric_accepts_positive_double()
    {
        Assert.True(ClipDuration.TryReadNumericSeconds(El("""{ "duration_seconds": 8.5 }"""), out var s));
        Assert.Equal(8.5, s);
    }

    [Fact]
    public void ReadNumeric_accepts_numeric_string()
    {
        Assert.True(ClipDuration.TryReadNumericSeconds(El("""{ "duration_seconds": "6" }"""), out var s));
        Assert.Equal(6, s);
    }

    [Theory]
    [InlineData("""{ "duration_seconds": 0 }""")]
    [InlineData("""{ "duration_seconds": -3 }""")]
    [InlineData("""{ "duration_seconds": "abc" }""")]
    [InlineData("""{ "timestamp": "00:00 - 00:05" }""")]
    [InlineData("""{ "visual_prompt": "x" }""")]
    public void ReadNumeric_false_when_no_positive_number(string json)
    {
        Assert.False(ClipDuration.TryReadNumericSeconds(El(json), out var s));
        Assert.Equal(0, s);
    }

    [Fact]
    public void Resolve_numeric_wins_over_timestamp()
    {
        // duration_seconds present → used even when a (longer) timestamp span also exists.
        var dur = ClipDuration.Resolve(El("""{ "duration_seconds": 6, "timestamp": "00:00 - 00:30" }"""), 8);
        Assert.Equal(6, dur);
    }

    [Fact]
    public void Resolve_falls_back_to_timestamp_span()
    {
        var dur = ClipDuration.Resolve(El("""{ "timestamp": "01:10 - 01:18" }"""), 8);
        Assert.Equal(8, dur); // (70..78) → 8 seconds
    }

    [Fact]
    public void Resolve_ignores_non_increasing_or_malformed_timestamp()
    {
        Assert.Equal(8, ClipDuration.Resolve(El("""{ "timestamp": "00:10 - 00:10" }"""), 8)); // b == a → default
        Assert.Equal(8, ClipDuration.Resolve(El("""{ "timestamp": "garbage" }"""), 8));       // no match → default
    }

    [Fact]
    public void Resolve_uses_default_when_nothing_present()
    {
        Assert.Equal(8, ClipDuration.Resolve(El("""{ "visual_prompt": "x" }"""), 8));
    }
}
