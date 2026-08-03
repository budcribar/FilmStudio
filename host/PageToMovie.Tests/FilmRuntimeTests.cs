using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class FilmRuntimeTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(10, 10)]
    [InlineData(200, 180)]
    public void ClampMinutes_bounds(int input, int expected)
        => Assert.Equal(expected, FilmRuntime.ClampMinutes(input));
}
