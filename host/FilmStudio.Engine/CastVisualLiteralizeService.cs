using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Engine;

/// <summary>
/// AI pass: rewrite figurative / idiomatic visual prose into literal filmable descriptions.
/// Avoids never-ending regex nickname lists — the model judges phrase risk.
/// Prompt: <c>prompts/cast_visual_literalize.txt</c>.
/// </summary>
public sealed class CastVisualLiteralizeService
{
    public const string PromptRelativePath = "prompts/cast_visual_literalize.txt";

    private readonly ProjectStore _projects;
    private readonly IGrokChatClient _chat;
    private readonly ILogger<CastVisualLiteralizeService> _log;

    public CastVisualLiteralizeService(
        ProjectStore projects,
        IGrokChatClient chat,
        ILogger<CastVisualLiteralizeService> log)
    {
        _projects = projects;
        _chat = chat;
        _log = log;
    }

    /// <summary>
    /// Literalize description / visual_lock / wardrobe_always on each seed in-place (dict).
    /// Non-fatal: returns input seeds if chat fails.
    /// </summary>
    public async Task<Dictionary<string, object?>> LiteralizeSeedsAsync(
        Dictionary<string, object?> seeds,
        string model = "grok-4.5",
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (seeds.Count == 0 || !_chat.IsConfigured)
            return seeds;

        onProgress?.Invoke("Literalizing visual descriptions (AI)…");
        try
        {
            var system = await LoadSystemPromptAsync(_projects.WorkspaceRoot, ct).ConfigureAwait(false);
            var payload = new Dictionary<string, object?>
            {
                ["character_seed_tokens"] = BuildVisualPayload(seeds),
            };
            var user =
                "Literalize figurative/idiomatic visual language in these character seeds.\n" +
                "Return JSON only with character_seed_tokens.\n\n" +
                JsonSerializer.Serialize(payload, JsonDefaults.Indented);

            var raw = await _chat.CompleteAsync(system, user, model, temperature: 0.15, ct)
                .ConfigureAwait(false);
            var parsed = GrokChatClient.ParseJsonObject(StripFences(raw));
            return MergeLiteralized(seeds, parsed);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cast visual literalize failed — keeping pre-literalize seeds");
            onProgress?.Invoke("Literalize pass skipped (non-fatal).");
            return seeds;
        }
    }

    public static async Task<string> LoadSystemPromptAsync(string workspaceRoot, CancellationToken ct = default)
    {
        var path = Path.Combine(
            workspaceRoot,
            PromptRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new InvalidOperationException($"Visual literalize prompt not found: {path}");
        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BuildVisualPayload(Dictionary<string, object?> seeds)
    {
        var outSeeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, val) in seeds)
        {
            if (val is not Dictionary<string, object?> seed) continue;
            var slim = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (seed.TryGetValue("description", out var d)) slim["description"] = d;
            if (seed.TryGetValue("visual_lock", out var v)) slim["visual_lock"] = v;
            if (seed.TryGetValue("wardrobe_always", out var w)) slim["wardrobe_always"] = w;
            if (seed.TryGetValue("display_name_policy", out var p)) slim["display_name_policy"] = p;
            if (seed.TryGetValue("canonical_given_name", out var n)) slim["canonical_given_name"] = n;
            outSeeds[key] = slim;
        }
        return outSeeds;
    }

    private static Dictionary<string, object?> MergeLiteralized(
        Dictionary<string, object?> original,
        Dictionary<string, object?> parsed)
    {
        Dictionary<string, object?>? cleanedSeeds = null;
        if (parsed.TryGetValue("character_seed_tokens", out var s) && s is Dictionary<string, object?> d)
            cleanedSeeds = d;
        else if (parsed.TryGetValue("global_production_variables", out var g) &&
                 g is Dictionary<string, object?> gpv &&
                 gpv.TryGetValue("character_seed_tokens", out var s2) &&
                 s2 is Dictionary<string, object?> d2)
            cleanedSeeds = d2;

        if (cleanedSeeds is null || cleanedSeeds.Count == 0)
            return original;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, val) in original)
        {
            if (val is not Dictionary<string, object?> seed)
            {
                result[key] = val;
                continue;
            }

            var copy = new Dictionary<string, object?>(seed, StringComparer.OrdinalIgnoreCase);
            if (cleanedSeeds.TryGetValue(key, out var cval) && cval is Dictionary<string, object?> clean)
            {
                if (clean.TryGetValue("description", out var desc) && desc is not null)
                    copy["description"] = desc.ToString()?.Trim();
                if (clean.TryGetValue("visual_lock", out var vl) && vl is not null)
                    copy["visual_lock"] = vl.ToString()?.Trim();
                if (clean.TryGetValue("wardrobe_always", out var wa) && wa is List<object?> list)
                    copy["wardrobe_always"] = list;
            }
            result[key] = copy;
        }

        // Add any unexpected keys from model (shouldn't, but keep closed)
        foreach (var (key, val) in cleanedSeeds)
        {
            if (!result.ContainsKey(key) && val is Dictionary<string, object?>)
                result[key] = val;
        }

        return result;
    }

    private static string StripFences(string text)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = Regex.Replace(text, @"^```(?:json|text)?\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\s*```\s*$", "");
        }
        return text.Trim();
    }
}
