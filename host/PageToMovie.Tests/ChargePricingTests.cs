using PageToMovie.Core.Billing;
using Xunit;

namespace PageToMovie.Tests;

public class ChargePricingTests
{
    [Fact]
    public void ToCharge_applies_multiplier()
    {
        Assert.Equal(2.5, ChargePricing.ToCharge(1.0, 2.5));
        Assert.Equal(0, ChargePricing.ToCharge(0, 3));
    }

    [Fact]
    public void DisplayCharge_always_uses_current_multiplier()
    {
        // Even if a legacy row froze mult=2, display uses current admin mult=5 on list rate.
        var charged = ChargePricing.DisplayCharge(
            storedUsd: 2.0,
            listUsd: 1.0,
            eventMultiplier: 2.0,
            currentMultiplier: 5.0);
        Assert.Equal(5.0, charged);
    }

    [Fact]
    public void ResolveListUsd_prefers_list_usd()
    {
        Assert.Equal(1.0, ChargePricing.ResolveListUsd(9.0, listUsd: 1.0, eventMultiplier: 3.0));
    }

    [Fact]
    public void ResolveListUsd_divides_legacy_charged_row()
    {
        // usd was stored charged at 2× without list_usd
        Assert.Equal(1.0, ChargePricing.ResolveListUsd(2.0, listUsd: null, eventMultiplier: 2.0));
    }

    [Fact]
    public void DisplayCharge_list_only_row()
    {
        var charged = ChargePricing.DisplayCharge(
            storedUsd: 1.0,
            listUsd: null,
            eventMultiplier: null,
            currentMultiplier: 3.0);
        Assert.Equal(3.0, charged);
    }
}
