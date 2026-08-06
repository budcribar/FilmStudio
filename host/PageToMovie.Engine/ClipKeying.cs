using System.Text.Json;
using System.Text.Json.Nodes;

namespace PageToMovie.Engine;

/// <summary>
/// Single source of truth for reading a clip's number from a blueprint <c>veo_clips[*]</c> node.
///
/// Older shot plans — and some auto-inserted end-credits scenes — key the clip on the legacy
/// <c>clip_index</c> field with no <c>clip_number</c>. Every generation, cost, and read site MUST
/// resolve the number the same way, or such a clip becomes invisible where the fallback is missing:
/// the batch-gen finder logs "S0xC1: not in blueprint — skip" (never generates it) and the cost
/// report counts it as $0. Newly planned clips carry both fields, so the hazard is entirely
/// already-persisted legacy clips.
///
/// Two overloads because the codebase reads clips in two forms — read-only <see cref="JsonElement"/>
/// (gen/cost blueprint scans, sentinel 0) and mutable <see cref="JsonNode"/> (edit paths, sentinel
/// -1). Each preserves its side's existing "absent" sentinel so call-site guards (<c>cn &lt;= 0</c>,
/// <c>== clip</c>) keep working unchanged.
/// </summary>
internal static class ClipKeying
{
    /// <summary>
    /// Canonical clip number from a read-only clip element: <c>clip_number</c>, else legacy
    /// <c>clip_index</c>, else 0 (invalid — callers skip on <c>cn &lt;= 0</c>).
    /// </summary>
    public static int ClipNumber(JsonElement clip)
    {
        if (clip.ValueKind != JsonValueKind.Object) return 0;
        if (clip.TryGetProperty("clip_number", out var cn) && cn.TryGetInt32(out var n)) return n;
        if (clip.TryGetProperty("clip_index", out var ci) && ci.TryGetInt32(out var i)) return i;
        return 0;
    }

    /// <summary>
    /// Canonical clip number from a mutable clip node: <c>clip_number</c> when present (even if
    /// malformed → -1), else legacy <c>clip_index</c>, else -1 (absent). Mirrors the prior
    /// <c>ReadJsonNodeInt(c["clip_number"])</c> behavior but adds the <c>clip_index</c> fallback so a
    /// legacy clip can still be found/keyed/removed rather than colliding on a null key.
    /// </summary>
    public static int ClipNumber(JsonNode? clip)
    {
        if (clip is not JsonObject o) return -1;
        if (o["clip_number"] is { } cnNode) return NodeInt(cnNode);
        return o["clip_index"] is { } ciNode ? NodeInt(ciNode) : -1;
    }

    private static int NodeInt(JsonNode node)
    {
        try { return node.GetValue<int>(); }
        catch { return int.TryParse(node.ToString(), out var n) ? n : -1; }
    }
}
