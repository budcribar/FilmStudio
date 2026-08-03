using System.Text.Json;
using System.Text.Json.Nodes;
using PageToMovie.Adaptation;

namespace PageToMovie.Engine;

/// <summary>
/// Natural vs user-chosen target film length for a project.
/// Natural comes from <see cref="PageToMovie.Adaptation.AdaptationDensity"/>; target drives Stage 1 / estimates.
/// </summary>
public static class FilmRuntime
{
    public const int MinMinutes = 2;
    public const int MaxMinutes = 180;

    public sealed class Snapshot
    {
        /// <summary>True when source/book_full.txt exists (natural length is meaningful).</summary>
        public bool HasBookText { get; init; }
        public int NaturalMinutes { get; init; }
        public int TargetMinutes { get; init; }
        public string Mode { get; init; } = "natural"; // natural | reduced | custom
        public int? TextWords { get; init; }
        public string? BookKind { get; init; }
        public string Source { get; init; } = ""; // config | extract_meta | density | none
    }

    public static int ClampMinutes(int minutes) =>
        Math.Clamp(minutes, MinMinutes, MaxMinutes);

    /// <summary>
    /// Resolve target minutes for screenplay generation.
    /// Order: explicit override → pipeline_config.target_runtime_minutes →
    /// extract_meta target/suggested → density natural.
    /// </summary>
    public static async Task<Snapshot> ResolveAsync(
        ProjectStore store,
        string projectId,
        string? bookText = null,
        int? overrideTargetMinutes = null,
        CancellationToken ct = default)
    {
        var dir = store.GetProjectDir(projectId);
        var metaPath = Path.Combine(dir, "source", "extract_meta.json");
        var bookPath = Path.Combine(dir, "source", "book_full.txt");

        if (bookText is null && File.Exists(bookPath))
            bookText = await File.ReadAllTextAsync(bookPath, ct).ConfigureAwait(false);

        int? metaNatural = null;
        int? metaTarget = null;
        int? metaWords = null;
        string? bookKind = null;

        if (File.Exists(metaPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false));
                var root = doc.RootElement;
                if (TryInt(root, "natural_runtime_minutes", out var n0)) metaNatural = n0;
                if (TryInt(root, "target_runtime_minutes", out var t0)) metaTarget = t0;
                if (metaTarget is null && TryInt(root, "suggested_total_minutes", out var s0))
                    metaTarget = s0;
                if (TryInt(root, "text_words", out var w0)) metaWords = w0;
                if (root.TryGetProperty("book_kind", out var bk))
                    bookKind = bk.GetString();
            }
            catch { /* ignore */ }
        }

        var hasBook = !string.IsNullOrWhiteSpace(bookText) || File.Exists(bookPath);

        int natural;
        string densitySource;
        if (metaNatural is > 0)
        {
            natural = ClampMinutes(metaNatural.Value);
            densitySource = "extract_meta";
        }
        else if (!string.IsNullOrWhiteSpace(bookText))
        {
            natural = BookTextAnalyzer.ResolveStage1RuntimeMinutes(bookText);
            densitySource = "density";
        }
        else if (metaTarget is > 0)
        {
            natural = ClampMinutes(metaTarget.Value);
            densitySource = "extract_meta";
        }
        else
        {
            natural = 0;
            densitySource = "none";
        }

        var cfg = await store.GetConfigAsync(projectId, ct).ConfigureAwait(false);
        int? configTarget = null;
        string? configMode = null;
        if (cfg.TryGetValue("target_runtime_minutes", out var tr) && tr.ValueKind == JsonValueKind.Number &&
            tr.TryGetInt32(out var ctm) && ctm > 0)
            configTarget = ctm;
        if (cfg.TryGetValue("runtime_mode", out var rm) && rm.ValueKind == JsonValueKind.String)
            configMode = rm.GetString();

        int target;
        string mode;
        string source;
        if (overrideTargetMinutes is > 0)
        {
            target = ClampMinutes(overrideTargetMinutes.Value);
            mode = target == natural ? "natural" : (target < natural ? "reduced" : "custom");
            source = "override";
        }
        else if (configTarget is > 0)
        {
            target = ClampMinutes(configTarget.Value);
            mode = string.IsNullOrWhiteSpace(configMode)
                ? (target == natural ? "natural" : target < natural ? "reduced" : "custom")
                : configMode!.Trim().ToLowerInvariant();
            source = "config";
        }
        else if (metaTarget is > 0)
        {
            target = ClampMinutes(metaTarget.Value);
            mode = target == natural ? "natural" : target < natural ? "reduced" : "custom";
            source = "extract_meta";
        }
        else if (natural > 0)
        {
            target = natural;
            mode = "natural";
            source = densitySource;
        }
        else
        {
            target = 0;
            mode = "none";
            source = "none";
        }

        return new Snapshot
        {
            HasBookText = hasBook,
            NaturalMinutes = natural,
            TargetMinutes = target,
            Mode = mode,
            TextWords = metaWords,
            BookKind = bookKind,
            Source = source,
        };
    }

    /// <summary>
    /// Persist user target (and keep natural). Updates pipeline_config + extract_meta when present.
    /// </summary>
    public static async Task<Snapshot> SetTargetAsync(
        ProjectStore store,
        string projectId,
        int targetMinutes,
        CancellationToken ct = default)
    {
        var snap = await ResolveAsync(store, projectId, ct: ct).ConfigureAwait(false);
        if (!snap.HasBookText || snap.NaturalMinutes <= 0)
            throw new InvalidOperationException(
                "Import the book first so we can measure a natural film length, then set a shorter target if you want.");
        targetMinutes = ClampMinutes(targetMinutes);
        var mode = targetMinutes == snap.NaturalMinutes
            ? "natural"
            : targetMinutes < snap.NaturalMinutes ? "reduced" : "custom";

        using var updateDoc = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["target_runtime_minutes"] = targetMinutes,
            ["natural_runtime_minutes"] = snap.NaturalMinutes,
            ["runtime_mode"] = mode,
        }));
        await store.SaveConfigAsync(projectId, updateDoc.RootElement.Clone(), ct).ConfigureAwait(false);

        var metaPath = Path.Combine(store.GetProjectDir(projectId), "source", "extract_meta.json");
        if (File.Exists(metaPath))
        {
            try
            {
                var node = JsonNode.Parse(await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false)) as JsonObject
                           ?? new JsonObject();
                node["natural_runtime_minutes"] = snap.NaturalMinutes;
                node["target_runtime_minutes"] = targetMinutes;
                node["suggested_total_minutes"] = targetMinutes; // backward compatible
                node["runtime_mode"] = mode;
                await File.WriteAllTextAsync(
                    metaPath,
                    node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
                    ct).ConfigureAwait(false);
            }
            catch { /* non-fatal */ }
        }

        return new Snapshot
        {
            HasBookText = snap.HasBookText,
            NaturalMinutes = snap.NaturalMinutes,
            TargetMinutes = targetMinutes,
            Mode = mode,
            TextWords = snap.TextWords,
            BookKind = snap.BookKind,
            Source = "config",
        };
    }

    /// <summary>Write natural (+ default target=natural) into extract_meta after book prepare.</summary>
    public static void ApplyNaturalToMetaDictionary(
        Dictionary<string, object?> meta,
        int naturalMinutes,
        int? existingTarget = null)
    {
        naturalMinutes = ClampMinutes(naturalMinutes);
        var target = existingTarget is > 0 ? ClampMinutes(existingTarget.Value) : naturalMinutes;
        meta["natural_runtime_minutes"] = naturalMinutes;
        meta["target_runtime_minutes"] = target;
        meta["suggested_total_minutes"] = target;
        meta["runtime_mode"] = target == naturalMinutes ? "natural" : "custom";
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var el)) return false;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value)) return value > 0;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out value)) return value > 0;
        return false;
    }
}
