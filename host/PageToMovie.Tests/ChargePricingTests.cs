using PageToMovie.Core.Billing;
using Xunit;

namespace PageToMovie.Tests;

public class ChargePricingTests
{
    [Theory]
    [InlineData(1.0, 10.0, 10.0)]
    [InlineData(1.5, 10.0, 15.0)]
    [InlineData(2.0, 0.12, 0.24)]
    [InlineData(0, 5.0, 0)]
    public void ToCharge_applies_multiplier(double mult, double list, double expected)
        => Assert.Equal(expected, ChargePricing.ToCharge(list, mult));

    [Fact]
    public void ClampMultiplier_rejects_nan()
        => Assert.Equal(1.0, ChargePricing.ClampMultiplier(double.NaN));
}
