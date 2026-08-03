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

    /// <summary>
    /// Resolve customer charge for a ledger/API row.
    /// New rows store list + write-time multiplier + charged <paramref name="storedUsd"/>.
    /// Legacy rows only store list in <paramref name="storedUsd"/> / <paramref name="listUsd"/> —
    /// reprice with <paramref name="currentMultiplier"/> so actuals match estimate markup.
    /// </summary>
    public static double ResolveChargeUsd(
        double storedUsd,
        double? listUsd,
        double? eventMultiplier,
        double currentMultiplier)
    {
        var current = ClampMultiplier(currentMultiplier);
        if (eventMultiplier is double em && em > 0 && double.IsFinite(em))
        {
            // Write-time charge: prefer list × event mult when list is known; else trust stored usd.
            if (listUsd is double lu && lu >= 0 && double.IsFinite(lu))
                return ToCharge(lu, em);
            return Math.Round(Math.Max(0, storedUsd), 6);
        }

        // Legacy: amounts were list rates only.
        var list = listUsd is double l && l >= 0 && double.IsFinite(l)
            ? l
            : Math.Max(0, storedUsd);
        return ToCharge(list, current);
    }
}
