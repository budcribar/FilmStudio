using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Engine;

/// <summary>
/// Attach book page plates to character seeds as design_reference_images.
/// Flexible: Stage 1 source_image_pages when present, else heuristics (hero/early pages).
/// Does not lock portraits — only candidate seed paths for Grok / UI multi-select.
/// </summary>
public sealed class CharacterBookPlateService
{
    private readonly ProjectStore _projects;
    private readonly ILogger<CharacterBookPlateService> _log;

    public CharacterBookPlateService(ProjectStore projects, ILogger<CharacterBookPlateService> log)
    {
        _projects = projects;
        _log = log;
    }

    public FilmStudio.Core.Models.AttachCharacterPlatesResult Attach(
        string projectId,
        bool force = true,
        bool copyIntoAssets = true,
        string? onlyCharKey = null)
    {
        var projectDir = _projects.GetProjectDir(projectId);
        var scenesPath = _projects.ResolveScenesJsonPath(projectId);
        var result = new FilmStudio.Core.Models.AttachCharacterPlatesResult();

        if (!File.Exists(scenesPath))
        {
            result.Reason = $"no_stage1:{scenesPath}";
            return result;
        }

        var inventory = LoadBookImageInventory(projectDir);
        if (inventory.Count == 0)
        {
            result.Reason = "no_book_images";
            return result;
        }

        JsonNode root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(scenesPath))
                   ?? throw new InvalidOperationException("empty scenes.json");
        }
        catch (Exception ex)
        {
            result.Reason = $"bad_scenes:{ex.Message}";
            return result;
        }

        var gpv = root["global_production_variables"] as JsonObject
                  ?? new JsonObject();
        root["global_production_variables"] = gpv;
        var seeds = gpv["character_seed_tokens"] as JsonObject;
        if (seeds is null || seeds.Count == 0)
        {
            result.Reason = "no_character_seeds";
            return result;
        }

        var charsDir = Path.Combine(projectDir, "assets", "characters");
        if (copyIntoAssets)
            Directory.CreateDirectory(charsDir);

        var index = 0;
        foreach (var (key, seedNode) in seeds.ToList())
        {
            if (onlyCharKey is { Length: > 0 } &&
                !string.Equals(key, onlyCharKey, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (seedNode is not JsonObject seed)
            {
                index++;
                continue;
            }

            if (IsVoiceOnly(key, seed))
            {
                seed.Remove("design_reference_images");
                seed.Remove("book_reference_images");
                result.CharactersSkipped++;
                result.AttachedByCharacter[key] = new List<string> { "(voice_only)" };
                index++;
                continue;
            }

            if (!force &&
                seed["design_reference_images"] is JsonArray existing &&
                existing.Count > 0)
            {
                result.CharactersSkipped++;
                result.AttachedByCharacter[key] = existing
                    .Select(x => x?.GetValue<string>() ?? "")
                    .Where(s => s.Length > 0)
                    .ToList();
                index++;
                continue;
            }

            var pages = PagesForSeed(seed);
            List<BookImageRow> picks;
            string method;
            if (pages.Count > 0)
            {
                picks = RowsForPages(inventory, pages);
                method = "source_image_pages";
            }
            else
            {
                picks = HeuristicPicks(inventory, key, seed, index);
                method = "heuristic";
                seed["source_image_pages"] = new JsonArray(
                    picks.Where(p => p.Page > 0).Select(p => (JsonNode)p.Page).ToArray());
            }

            var relPaths = new List<string>();
            for (var j = 0; j < Math.Min(3, picks.Count); j++)
            {
                var row = picks[j];
                if (copyIntoAssets && File.Exists(row.AbsPath))
                {
                    var ext = Path.GetExtension(row.AbsPath).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext)) ext = ".png";
                    var destName = $"{key.ToLowerInvariant()}_bookref_{j + 1}{ext}";
                    var dest = Path.Combine(charsDir, destName);
                    try
                    {
                        File.Copy(row.AbsPath, dest, overwrite: true);
                        relPaths.Add(
                            Path.GetRelativePath(projectDir, dest).Replace('\\', '/'));
                    }
                    catch
                    {
                        relPaths.Add(row.PathRel);
                    }
                }
                else
                {
                    relPaths.Add(row.PathRel);
                }
            }

            if (relPaths.Count == 0)
            {
                result.CharactersSkipped++;
                result.AttachedByCharacter[key] = new List<string> { $"(none via {method})" };
            }
            else
            {
                seed["design_reference_images"] = new JsonArray(
                    relPaths.Select(r => (JsonNode)r).ToArray());
                seed["book_reference_images"] = new JsonArray(
                    relPaths.Select(r => (JsonNode)r).ToArray());
                result.CharactersUpdated++;
                result.AttachedByCharacter[key] = relPaths;
                _log.LogInformation(
                    "Attached {Count} book plate(s) to {Key} via {Method}",
                    relPaths.Count, key, method);
            }

            index++;
        }

        // Backup + write Stage 1
        try
        {
            var bak = scenesPath + $".bak_attach_plates_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(scenesPath, bak, overwrite: true);
        }
        catch { /* ignore */ }

        File.WriteAllText(
            scenesPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        // Mirror into Stage 2 blueprint seeds when present
        TryMirrorBlueprint(projectDir, seeds);

        result.Ok = result.CharactersUpdated > 0 || result.CharactersSkipped > 0;
        if (!result.Ok)
            result.Reason ??= "nothing_attached";
        return result;
    }

    private void TryMirrorBlueprint(string projectDir, JsonObject stage1Seeds)
    {
        try
        {
            var projectId = Path.GetFileName(
                projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var bp = _projects.FindBlueprintPath(projectId);
            if (bp is null || !File.Exists(bp)) return;

            var root = JsonNode.Parse(File.ReadAllText(bp)) as JsonObject;
            if (root is null) return;
            var gpv = root["global_production_variables"] as JsonObject ?? new JsonObject();
            root["global_production_variables"] = gpv;
            var bpSeeds = gpv["character_seed_tokens"] as JsonObject ?? new JsonObject();
            gpv["character_seed_tokens"] = bpSeeds;

            foreach (var (key, seedNode) in stage1Seeds)
            {
                if (seedNode is not JsonObject src) continue;
                if (bpSeeds[key] is not JsonObject dest)
                {
                    bpSeeds[key] = src.DeepClone();
                    continue;
                }
                if (src["design_reference_images"] is JsonArray arr)
                {
                    dest["design_reference_images"] = arr.DeepClone();
                    dest["book_reference_images"] = arr.DeepClone();
                }
                if (src["source_image_pages"] is JsonArray pages)
                    dest["source_image_pages"] = pages.DeepClone();
            }

            File.WriteAllText(bp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not mirror book plates into blueprint");
        }
    }

    private static bool IsVoiceOnly(string key, JsonObject seed)
    {
        var pol = (seed["display_name_policy"]?.GetValue<string>() ?? "").ToLowerInvariant();
        return pol.Contains("never")
               || key.EndsWith("_Narrator", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Character_Narrator", StringComparison.OrdinalIgnoreCase)
               || key.Contains("narrator", StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> PagesForSeed(JsonObject seed)
    {
        var outList = new List<int>();
        var raw = seed["source_image_pages"] ?? seed["image_pages"];
        if (raw is JsonValue jv)
        {
            if (jv.TryGetValue<int>(out var one))
                outList.Add(one);
            else if (jv.TryGetValue<string>(out var s))
            {
                foreach (Match m in Regex.Matches(s, @"\d+"))
                    if (int.TryParse(m.Value, out var n)) outList.Add(n);
            }
        }
        else if (raw is JsonArray arr)
        {
            foreach (var x in arr)
            {
                if (x is null) continue;
                if (x is JsonValue v && v.TryGetValue<int>(out var n))
                    outList.Add(n);
                else if (int.TryParse(x.ToString(), out var n2))
                    outList.Add(n2);
            }
        }
        return outList;
    }

    private static List<BookImageRow> RowsForPages(List<BookImageRow> inventory, List<int> pages)
    {
        var byPage = inventory.GroupBy(r => r.Page).ToDictionary(g => g.Key, g => g.ToList());
        var picks = new List<BookImageRow>();
        foreach (var pg in pages)
        {
            if (!byPage.TryGetValue(pg, out var cands) || cands.Count == 0) continue;
            var best = cands
                .OrderBy(r => r.Kind == "rendered_page" || r.Name.StartsWith("page_", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(r => r.Kind == "embedded" ? 0 : 1)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .First();
            picks.Add(best);
        }
        return picks;
    }

    private static List<BookImageRow> HeuristicPicks(
        List<BookImageRow> inventory,
        string key,
        JsonObject seed,
        int index)
    {
        var ordered = inventory
            .OrderBy(r => r.Name.Contains("cover", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Kind == "rendered_page" || r.Name.StartsWith("page_", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Kind == "embedded" ? 0 : 2)
            .ThenBy(r => r.Page > 0 ? r.Page : 99)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var early = ordered.Where(r => r.Page is > 0 and <= 8 ||
                                       r.Name.Contains("cover", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (early.Count == 0)
            early = ordered.Take(6).ToList();

        var token = key.Replace("Character_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var given = (seed["canonical_given_name"]?.GetValue<string>() ?? "").ToLowerInvariant();
        var nameHits = inventory.Where(r =>
            r.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
            (given.Length > 0 && r.Name.Contains(given, StringComparison.OrdinalIgnoreCase))).ToList();

        var desc = (seed["description"]?.GetValue<string>() ?? "").ToLowerInvariant();
        var isHero = index == 0 || desc.Contains("dog") || token.Contains("buster");

        if (nameHits.Count > 0) return nameHits.Take(3).ToList();
        if (isHero) return early.Take(3).ToList();
        return early.Take(2).ToList();
    }

    private static List<BookImageRow> LoadBookImageInventory(string projectDir)
    {
        var rows = new List<BookImageRow>();
        var source = Path.Combine(projectDir, "source");
        var imgDir = Path.Combine(source, "book_images");
        var man = Path.Combine(imgDir, "manifest.json");

        if (File.Exists(man))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(man));
                if (doc.RootElement.TryGetProperty("images", out var imgs) &&
                    imgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var im in imgs.EnumerateArray())
                    {
                        var rel = im.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                        rel = rel.Replace('\\', '/');
                        var abs = Path.IsPathRooted(rel)
                            ? rel
                            : Path.Combine(source, rel.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(abs))
                            abs = Path.Combine(imgDir, Path.GetFileName(rel));
                        if (!File.Exists(abs)) continue;
                        var page = im.TryGetProperty("page", out var pg) && pg.TryGetInt32(out var pn) ? pn : 0;
                        var kind = im.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
                        var pathRel = Path.GetRelativePath(projectDir, abs).Replace('\\', '/');
                        rows.Add(new BookImageRow(pathRel, abs, page, kind, Path.GetFileName(abs).ToLowerInvariant()));
                    }
                }
            }
            catch { /* fall through */ }
        }

        if (rows.Count == 0 && Directory.Exists(imgDir))
        {
            foreach (var f in Directory.EnumerateFiles(imgDir)
                         .Where(f => Regex.IsMatch(Path.GetExtension(f), @"\.(png|jpe?g|webp)$", RegexOptions.IgnoreCase))
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(f);
                var m = Regex.Match(name, @"(?:page|p|embedded_p)0*(\d+)", RegexOptions.IgnoreCase);
                var page = m.Success && int.TryParse(m.Groups[1].Value, out var pn) ? pn : 0;
                var pathRel = Path.GetRelativePath(projectDir, f).Replace('\\', '/');
                rows.Add(new BookImageRow(pathRel, f, page, "file", name.ToLowerInvariant()));
            }
        }

        return rows;
    }

    private sealed record BookImageRow(string PathRel, string AbsPath, int Page, string Kind, string Name);
}
