namespace PageToMovie.Adaptation;

/// <summary>
/// Pure film-length math — density natural minutes, clamp, and mode.
/// No ProjectStore, paths, or pipeline_config. Engine <c>FilmRuntime</c> is
/// storage/orchestration only and must call this for natural minutes.
/// </summary>
public static class NaturalRuntime
{
    /// <summary>
    /// Floor for target/natural minutes. 1 allows true micro films (nursery rhymes ~0.5–2 min)
    /// without forcing a fake 2–3 min floor that inflates cost and pads Stage‑1.
    /// </summary>
    public const int MinMinutes = 1;
    public const int MaxMinutes = 180;

    public static int ClampMinutes(int minutes) =>
        Math.Clamp(minutes, MinMinutes, MaxMinutes);

    /// <summary>
    /// <c>natural</c> | <c>reduced</c> | <c>custom</c> | <c>none</c>.
    /// </summary>
    public static string ResolveMode(int naturalMinutes, int targetMinutes)
    {
        if (naturalMinutes <= 0 || targetMinutes <= 0)
            return "none";
        if (targetMinutes == naturalMinutes) return "natural";
        if (targetMinutes < naturalMinutes) return "reduced";
        return "custom";
    }

    /// <summary>
    /// Density natural film minutes from book text (clamped 2–180). Returns 0 when empty.
    /// </summary>
    public static int EstimateNaturalMinutes(string? bookText)
    {
        if (string.IsNullOrWhiteSpace(bookText))
            return 0;
        return ClampMinutes(AdaptationDensity.EstimateNatural(bookText).NaturalFilmMinutes);
    }

    /// <summary>
    /// Natural from density + optional override target. Pure — no store.
    /// </summary>
    public static (int Natural, int Target, string Mode) Resolve(
        string? bookText,
        int? overrideMinutes = null)
    {
        var natural = EstimateNaturalMinutes(bookText);
        if (overrideMinutes is > 0)
        {
            var target = ClampMinutes(overrideMinutes.Value);
            return (natural, target, ResolveMode(natural, target));
        }

        if (natural > 0)
            return (natural, natural, "natural");
        return (0, 0, "none");
    }
}
