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
    public void ResolveChargeUsd_uses_write_time_multiplier_when_present()
    {
        // list 1.0 × 2.0 at write = 2.0; current mult 5 should not reprice
        var charged = ChargePricing.ResolveChargeUsd(
            storedUsd: 2.0,
            listUsd: 1.0,
            eventMultiplier: 2.0,
            currentMultiplier: 5.0);
        Assert.Equal(2.0, charged);
    }

    [Fact]
    public void ResolveChargeUsd_legacy_list_only_uses_current_multiplier()
    {
        var charged = ChargePricing.ResolveChargeUsd(
            storedUsd: 1.0,
            listUsd: null,
            eventMultiplier: null,
            currentMultiplier: 3.0);
        Assert.Equal(3.0, charged);
    }

    [Fact]
    public void ResolveChargeUsd_legacy_with_list_usd_uses_current_multiplier()
    {
        var charged = ChargePricing.ResolveChargeUsd(
            storedUsd: 1.0,
            listUsd: 1.0,
            eventMultiplier: null,
            currentMultiplier: 2.5);
        Assert.Equal(2.5, charged);
    }
}
