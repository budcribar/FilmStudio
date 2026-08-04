using PageToMovie.Adaptation;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class FilmRuntimeTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(10, 10)]
    [InlineData(200, 180)]
    public void ClampMinutes_bounds(int input, int expected)
        => Assert.Equal(expected, FilmRuntime.ClampMinutes(input));

    [Fact]
    public void ClampMinutes_delegates_to_Adaptation_NaturalRuntime()
    {
        Assert.Equal(NaturalRuntime.MinMinutes, FilmRuntime.MinMinutes);
        Assert.Equal(NaturalRuntime.MaxMinutes, FilmRuntime.MaxMinutes);
        Assert.Equal(NaturalRuntime.ClampMinutes(1), FilmRuntime.ClampMinutes(1));
        Assert.Equal(NaturalRuntime.ClampMinutes(999), FilmRuntime.ClampMinutes(999));
    }

    [Theory]
    [InlineData(10, 10, "natural")]
    [InlineData(10, 5, "reduced")]
    [InlineData(10, 15, "custom")]
    [InlineData(0, 10, "none")]
    public void NaturalRuntime_ResolveMode(int natural, int target, string expected)
        => Assert.Equal(expected, NaturalRuntime.ResolveMode(natural, target));

    [Fact]
    public void ApplyNaturalToMetaDictionary_fills_storage_keys()
    {
        var meta = new Dictionary<string, object?>();
        FilmRuntime.ApplyNaturalToMetaDictionary(meta, naturalMinutes: 12);
        Assert.Equal(12, meta["natural_runtime_minutes"]);
        Assert.Equal(12, meta["target_runtime_minutes"]);
        Assert.Equal(12, meta["suggested_total_minutes"]);
        Assert.Equal("natural", meta["runtime_mode"]);
    }
}
