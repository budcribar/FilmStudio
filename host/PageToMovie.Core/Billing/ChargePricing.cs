namespace PageToMovie.Core.Billing;

/// <summary>
/// Single place: vendor list rate → customer charge (admin charge multiplier).
/// Used for estimates, cost_ledger, user_api_calls, and credit debits.
/// </summary>
public static class ChargePricing
{
    /// <summary>Clamp multiplier to a sane range. Non-finite or negative → 1.0 (pass-through).</summary>
    public static double ClampMultiplier(double multiplier)
    {
        if (!double.IsFinite(multiplier) || multiplier < 0)
            return 1.0;
        return Math.Clamp(multiplier, 0, 100);
    }

    /// <summary>listUsd × multiplier, rounded to 6 dp (ledger precision).</summary>
    public static double ToCharge(double listUsd, double multiplier)
    {
        var list = Math.Max(0, listUsd);
        return Math.Round(list * ClampMultiplier(multiplier), 6);
    }

    /// <summary>Money display round (2 dp).</summary>
    public static double RoundMoney(double usd) => Math.Round(usd, 2);
}
