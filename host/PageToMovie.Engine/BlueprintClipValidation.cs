using System.Collections.Generic;
using System.Text.Json;

namespace PageToMovie.Engine;

/// <summary>
/// Shared guard against the "duplicate clip_number" data fault that doubles a scene downstream (the
/// scene stitch concatenates one file per <c>veo_clips</c> entry, so a repeated clip_number plays the
/// scene twice). Used two ways: the read path deduplicates + flags it so existing movies keep working
/// and the problem is visible; the shot-plan WRITE path throws on it so the bug is caught at its
/// source, before any video spend, without ever touching a saved movie.
/// </summary>
public static class BlueprintClipValidation
{
    /// <summary>
    /// Every (scene, clip_number) that appears more than once within its scene's <c>veo_clips</c>.
    /// Empty ⇒ clean.
    /// </summary>
    public static List<(int Scene, int ClipNumber)> FindDuplicateClipNumbers(JsonElement root)
    {
        var dups = new List<(int, int)>();
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("scenes", out var scenes) || scenes.ValueKind != JsonValueKind.Array)
            return dups;

        foreach (var s in scenes.EnumerateArray())
        {
            var sn = s.ValueKind == JsonValueKind.Object &&
                     s.TryGetProperty("scene_number", out var snEl) && snEl.TryGetInt32(out var n) ? n : 0;
            if (!s.TryGetProperty("veo_clips", out var vc) || vc.ValueKind != JsonValueKind.Array)
                continue;

            var seen = new HashSet<int>();
            foreach (var c in vc.EnumerateArray())
            {
                var cn = c.ValueKind == JsonValueKind.Object &&
                         c.TryGetProperty("clip_number", out var cnEl) && cnEl.TryGetInt32(out var m) ? m : 0;
                if (cn <= 0) continue;
                if (!seen.Add(cn)) dups.Add((sn, cn));
            }
        }
        return dups;
    }

    /// <summary>Human-readable summary of duplicates (e.g. "scene 4 clip 2, scene 7 clip 3"), or null if clean.</summary>
    public static string? DescribeDuplicates(JsonElement root)
    {
        var dups = FindDuplicateClipNumbers(root);
        return dups.Count == 0 ? null : string.Join(", ", dups.ConvertAll(d => $"scene {d.Scene} clip {d.ClipNumber}"));
    }
}
