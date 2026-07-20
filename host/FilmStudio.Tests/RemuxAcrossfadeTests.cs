using FilmStudio.Engine;
using Xunit;

namespace FilmStudio.Tests;

public class RemuxAcrossfadeTests
{
    [Fact]
    public void BuildAcrossfadeFilterComplex_two_inputs()
    {
        var fc = FfmpegRemuxService.BuildAcrossfadeFilterComplex(2, 0.1);
        Assert.Contains("concat=n=2:v=1:a=0[v]", fc);
        Assert.Contains("[0:a][1:a]acrossfade", fc);
        Assert.Contains("[a]", fc);
        Assert.DoesNotContain("ax1", fc);
    }

    [Fact]
    public void BuildAcrossfadeFilterComplex_three_inputs_chains()
    {
        var fc = FfmpegRemuxService.BuildAcrossfadeFilterComplex(3, 0.1);
        Assert.Contains("concat=n=3:v=1:a=0[v]", fc);
        Assert.Contains("[0:a][1:a]acrossfade", fc);
        Assert.Contains("[ax1][2:a]acrossfade", fc);
        Assert.EndsWith("[a]", fc);
    }
}
