using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Engine;

/// <summary>
/// AI: approved Fountain (+ optional book) → <c>source/cast_seeds.json</c>.
/// Cast identity for Characters UI / plates — not parsed solely from dialogue cues.
/// </summary>
public sealed class CastFromScreenplayService
{
    public const string PromptRelativePath = "prompts/fountain_to_cast.txt";

    private readonly ProjectStore _projects;
    private readonly IGrokChatClient _chat;
    private readonly CastVisualLiteralizeService _literalize;
    private readonly ILogger<CastFromScreenplayService> _log;

    public CastFromScreenplayService(
        ProjectStore projects,
        IGrokChatClient chat,
        CastVisualLiteralizeService literalize,
        ILogger<CastFromScreenplayService> log)
    {
        _projects = projects;
        _chat = chat;
        _literalize = literalize;
        _log = log;
    }

    public sealed class ExtractResult
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public string? OutPath { get; init; }
        public int CharacterCount { get; init; }
        public List<string> CharacterKeys { get; init; } = new();
        public string? MovieTitle { get; init; }
        public string? RawPath { get; init; }
    }

    /// <summary>
    /// Build cast_seeds.json from screenplay.fountain (+ book_full.txt when present).
    /// </summary>
    public async Task<ExtractResult> ExtractAsync(
        string projectId,
        string model = "grok-4.5",
        bool force = false,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (!_chat.IsConfigured)
            throw new InvalidOperationException("Connect service (API key) to build cast from the screenplay.");

        ScreenplayService.EnsureCanonicalDraft(_projects, projectId);
        var draftPath = ScreenplayService.GetDraftPath(_projects, projectId);
        if (!File.Exists(draftPath))
            return new ExtractResult { Ok = false, Error = "No screenplay draft. Import/approve a Fountain first." };

        var fountain = await File.ReadAllTextAsync(draftPath, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fountain))
            return new ExtractResult { Ok = false, Error = "Screenplay draft is empty." };

        var outPath = ScreenplayService.GetCastSeedsPath(_projects, projectId);
        if (!force && File.Exists(outPath))
        {
            try
            {
                using var existing = JsonDocument.Parse(await File.ReadAllTextAsync(outPath, ct).ConfigureAwait(false));
                var seeds = GetSeedsElement(existing.RootElement);
                if (seeds.ValueKind == JsonValueKind.Object && seeds.EnumerateObject().Count() > 0)
                {
                    onProgress?.Invoke("Cast file already present — use force to rebuild.");
                    var existingKeys = seeds.EnumerateObject().Select(p => p.Name).ToList();
                    return new ExtractResult
                    {
                        Ok = true,
                        OutPath = outPath,
                        CharacterCount = existingKeys.Count,
                        CharacterKeys = existingKeys,
                        MovieTitle = existing.RootElement.TryGetProperty("movie_title", out var mt)
                            ? mt.GetString()
                            : null,
                    };
                }
            }
            catch { /* rebuild */ }
        }

        var bookPath = Path.Combine(_projects.GetProjectDir(projectId), "source", "book_full.txt");
        string? book = null;
        if (File.Exists(bookPath))
            book = await File.ReadAllTextAsync(bookPath, ct).ConfigureAwait(false);

        onProgress?.Invoke("Loading cast prompt…");
        var system = await LoadSystemPromptAsync(_projects.WorkspaceRoot, ct).ConfigureAwait(false);
        var user = BuildUserPrompt(fountain, book);

        onProgress?.Invoke("Calling Grok for closed cast…");
        var raw = await _chat.CompleteAsync(system, user, model, temperature: 0.2, ct)
            .ConfigureAwait(false);
        raw = StripFences(raw);

        Dictionary<string, object?> parsed;
        try
        {
            parsed = GrokChatClient.ParseJsonObject(raw);
        }
        catch (Exception ex)
        {
            var dump = Path.Combine(
                _projects.GetProjectDir(projectId),
                "source",
                $"cast_raw_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dump)!);
                await File.WriteAllTextAsync(dump, raw, ct).ConfigureAwait(false);
            }
            catch { /* ignore */ }

            _log.LogWarning(ex, "Cast JSON parse failed for {Project}", projectId);
            return new ExtractResult
            {
                Ok = false,
                Error = $"Could not parse cast JSON: {ex.Message}",
                RawPath = dump,
            };
        }

        var normalized = NormalizeCastDoc(parsed, projectId);
        var seedsObj = GetSeedsDict(normalized);
        if (seedsObj.Count == 0)
            return new ExtractResult { Ok = false, Error = "Model returned no character_seed_tokens." };

        // Second AI pass: figurative / idiomatic visual language → literal filmable prose
        // (no never-ending regex nickname lists)
        var literalSeeds = await _literalize.LiteralizeSeedsAsync(
            seedsObj, model, onProgress, ct).ConfigureAwait(false);
        normalized["character_seed_tokens"] = literalSeeds;
        seedsObj = literalSeeds;

        onProgress?.Invoke($"Writing {seedsObj.Count} character seed(s)…");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        if (File.Exists(outPath))
        {
            try
            {
                File.Copy(outPath, outPath + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}", overwrite: true);
            }
            catch { /* ignore */ }
        }

        var json = JsonSerializer.Serialize(normalized, JsonDefaults.Indented);
        await File.WriteAllTextAsync(outPath, json + "\n", ct).ConfigureAwait(false);

        var keys = seedsObj.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        onProgress?.Invoke($"Cast ready · {keys.Count} character(s)");
        return new ExtractResult
        {
            Ok = true,
            OutPath = outPath,
            CharacterCount = keys.Count,
            CharacterKeys = keys,
            MovieTitle = normalized.TryGetValue("movie_title", out var t) ? t?.ToString() : null,
        };
    }

    public static async Task<string> LoadSystemPromptAsync(string workspaceRoot, CancellationToken ct = default)
    {
        var path = Path.Combine(
            workspaceRoot,
            PromptRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new InvalidOperationException($"Cast prompt not found: {path}");
        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    }

    private static string BuildUserPrompt(string fountain, string? book)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Extract the closed cast for production pinning.");
        sb.AppendLine("Include silent on-screen characters named only in action (e.g. BUSTER the dog).");
        sb.AppendLine("Return JSON only (schema_version cast_seeds.v1).");
        sb.AppendLine();
        sb.AppendLine("--- BEGIN FOUNTAIN ---");
        sb.AppendLine(TrimForPrompt(fountain, 40_000));
        sb.AppendLine("--- END FOUNTAIN ---");
        if (!string.IsNullOrWhiteSpace(book))
        {
            sb.AppendLine();
            sb.AppendLine("--- BEGIN BOOK (optional likeness / pages) ---");
            sb.AppendLine(TrimForPrompt(book, 20_000));
            sb.AppendLine("--- END BOOK ---");
        }
        return sb.ToString();
    }

    private static string TrimForPrompt(string text, int max)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (text.Length <= max) return text;
        return text[..max] + "\n\n[[truncated for length]]\n";
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

    private static Dictionary<string, object?> NormalizeCastDoc(
        Dictionary<string, object?> parsed,
        string projectId)
    {
        var outDoc = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["schema_version"] = "cast_seeds.v1",
            ["generation"] = new Dictionary<string, object?>
            {
                ["method"] = "CastFromScreenplayService",
                ["ts"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            },
        };

        if (parsed.TryGetValue("movie_title", out var mt) && mt is not null)
            outDoc["movie_title"] = mt.ToString();
        else
            outDoc["movie_title"] = projectId;

        if (parsed.TryGetValue("render_style_lock", out var rsl) && rsl is not null)
            outDoc["render_style_lock"] = rsl.ToString();

        var seedsIn = GetSeedsDict(parsed);
        var seedsOut = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, val) in seedsIn)
        {
            if (val is not Dictionary<string, object?> seed) continue;
            var k = key.StartsWith("Character_", StringComparison.OrdinalIgnoreCase)
                ? key
                : "Character_" + Regex.Replace(key, @"[^A-Za-z0-9]+", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(k) || k == "Character_") continue;

            var name = seed.TryGetValue("canonical_given_name", out var cn) && cn is not null
                ? cn.ToString()!
                : k.Replace("Character_", "").Replace('_', ' ');

            var off = string.Equals(
                seed.TryGetValue("display_name_policy", out var pol) ? pol?.ToString() : null,
                "never_on_screen",
                StringComparison.OrdinalIgnoreCase);

            // Visual fields are cleaned by CastVisualLiteralizeService (AI), not regex lists.
            var desc = CoerceString(seed, "description")
                ?? (off ? $"{name} (voice only)." : $"{name}, as in the screenplay.");
            var clean = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["description"] = desc,
                ["canonical_given_name"] = name,
                ["display_name_policy"] = off ? "never_on_screen" : "ok_anytime",
                ["voice_label"] = CoerceString(seed, "voice_label") ?? name.Replace(' ', '_'),
                ["voice_profile"] = CoerceString(seed, "voice_profile")
                    ?? "Consistent character voice every scene.",
                ["reference_image_placeholder"] = CoerceString(seed, "reference_image_placeholder")
                    ?? ProjectStore.CharacterRefFileName(k),
            };

            var vlock = CoerceString(seed, "visual_lock");
            if (!off && !string.IsNullOrWhiteSpace(vlock))
                clean["visual_lock"] = vlock;
            else if (!off)
                clean["visual_lock"] = $"Match {name} consistently across scenes.";

            if (seed.TryGetValue("wardrobe_always", out var wa) && wa is List<object?> list)
                clean["wardrobe_always"] = list;
            if (seed.TryGetValue("source_image_pages", out var sip) && sip is List<object?> pages)
                clean["source_image_pages"] = pages;

            seedsOut[k] = clean;
        }

        outDoc["character_seed_tokens"] = seedsOut;
        return outDoc;
    }

    private static Dictionary<string, object?> GetSeedsDict(Dictionary<string, object?> doc)
    {
        if (doc.TryGetValue("character_seed_tokens", out var s) && s is Dictionary<string, object?> d)
            return d;
        if (doc.TryGetValue("global_production_variables", out var g) &&
            g is Dictionary<string, object?> gpv &&
            gpv.TryGetValue("character_seed_tokens", out var s2) &&
            s2 is Dictionary<string, object?> d2)
            return d2;
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement GetSeedsElement(JsonElement root)
    {
        if (root.TryGetProperty("character_seed_tokens", out var s) && s.ValueKind == JsonValueKind.Object)
            return s;
        if (root.TryGetProperty("global_production_variables", out var g) &&
            g.TryGetProperty("character_seed_tokens", out var s2) &&
            s2.ValueKind == JsonValueKind.Object)
            return s2;
        return default;
    }

    private static string? CoerceString(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) ? v?.ToString()?.Trim() : null;
}
