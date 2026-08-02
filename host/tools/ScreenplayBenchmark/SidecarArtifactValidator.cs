using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScreenplayBenchmark;

/// <summary>
/// Local, provider-independent checks for a generated pre-production package.
/// These checks deliberately report precise repair targets instead of attempting
/// to silently rewrite creative work.
/// </summary>
internal static class SidecarArtifactValidator
{
    public static async Task<JsonObject> ValidateDirectoryAsync(string sourceDirectory, CancellationToken ct = default)
    {
        var fileByKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["adaptation_plan"] = "adaptation_plan.json",
            ["cast_seeds"] = "cast_seeds.json",
            ["location_bible"] = "location_bible.json",
            ["audio_plan"] = "audio_plan.json",
            ["edit_decision_list"] = "edit_decision_list.json",
            ["delivery_manifest"] = "delivery_manifest.json"
        };
        var package = new JsonObject();
        foreach (var (key, fileName) in fileByKey)
        {
            var filePath = Path.Combine(sourceDirectory, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Required sidecar is missing: {fileName}", filePath);
            package[key] = JsonNode.Parse(await File.ReadAllTextAsync(filePath, ct))
                ?? throw new InvalidOperationException($"{fileName} is not valid JSON.");
        }

        var fountainPath = Path.Combine(sourceDirectory, "screenplay.fountain");
        if (!File.Exists(fountainPath))
            throw new FileNotFoundException("Required screenplay is missing: screenplay.fountain", fountainPath);
        var validation = Validate(package, await File.ReadAllTextAsync(fountainPath, ct));
        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, "validation_report.json"),
            validation.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ct);
        return validation;
    }

    public static JsonObject Validate(JsonObject package, string fountain)
    {
        var failures = new JsonArray();
        var warnings = new JsonArray();
        var edlScenes = GetObjectArray(package["edit_decision_list"]?["scenes"]);
        var audioScenes = GetObjectArray(package["audio_plan"]?["scenes"]);
        var locations = GetObjectCollection(package["location_bible"]?["locations"]);
        var castTokens = package["cast_seeds"]?["character_seed_tokens"];

        var sceneIds = edlScenes
            .Select(scene => GetString(scene, "scene_id"))
            .Where(id => id is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        if (sceneIds.Count == 0)
            Fail(failures, "EDIT_SCENES_MISSING", "edit_decision_list.scenes must contain at least one scene.");

        var audioByScene = audioScenes
            .Select(scene => GetString(scene, "scene_id"))
            .Where(id => id is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var sceneId in sceneIds.Except(audioByScene, StringComparer.Ordinal))
            Fail(failures, "AUDIO_SCENE_MISSING", $"audio_plan is missing scene {sceneId}.", sceneId);
        foreach (var sceneId in audioByScene.Except(sceneIds, StringComparer.Ordinal))
            Warn(warnings, "AUDIO_SCENE_ORPHANED", $"audio_plan contains {sceneId}, which is not in the edit decision list.", sceneId);

        var locationsByKey = locations
            .Select(location => (Key: GetString(location, "location_key") ?? GetString(location, "key"), Node: location))
            .Where(item => item.Key is not null)
            .ToDictionary(item => item.Key!, item => item.Node, StringComparer.Ordinal);
        foreach (var scene in edlScenes)
        {
            var sceneId = GetString(scene, "scene_id") ?? "(unknown)";
            var locationKey = GetString(scene, "location_key");
            if (locationKey is null || !locationsByKey.TryGetValue(locationKey, out var location))
            {
                Fail(failures, "LOCATION_REFERENCE_MISSING", $"{sceneId} references missing location '{locationKey ?? "(blank)"}'.", sceneId);
                continue;
            }

            var assignments = GetStringArray(location["scene_assignments"]);
            if (!assignments.Contains(sceneId, StringComparer.Ordinal))
                Fail(failures, "LOCATION_SCENE_UNASSIGNED", $"{sceneId} uses {locationKey}, but that location does not assign the scene.", sceneId);
        }

        // The product's cast sidecar is keyed by stable cast id. An array may be good model output,
        // but it is not loadable by the existing product contract and must be repaired or normalized.
        if (castTokens is not JsonObject castByKey)
        {
            Fail(failures, "CAST_SIDE_CAR_SHAPE", "cast_seeds.character_seed_tokens must be an object keyed by stable cast key, not an array.");
        }
        else
        {
            var knownCast = castByKey.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
            var knownCastVariants = castByKey
                .SelectMany(item => GetKeys(item.Value?["age_variants"]))
                .ToHashSet(StringComparer.Ordinal);
            var knownWardrobes = castByKey
                .SelectMany(item => GetKeys(item.Value?["wardrobe_variants"]))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var scene in edlScenes)
            {
                var sceneId = GetString(scene, "scene_id") ?? "(unknown)";
                foreach (var variant in GetStringArray(scene["cast_variant_keys"]))
                {
                    var castKey = variant.Split(new[] { '.', '|' }, 2)[0];
                    if (!knownCast.Contains(castKey) && !knownCastVariants.Contains(variant))
                        Fail(failures, "CAST_VARIANT_UNKNOWN", $"{sceneId} references unknown cast variant '{variant}'.", sceneId);
                }
                foreach (var wardrobe in GetStringArray(scene["wardrobe_variant_keys"]))
                {
                    if (!knownWardrobes.Contains(wardrobe))
                        Fail(failures, "WARDROBE_VARIANT_UNKNOWN", $"{sceneId} references unknown wardrobe '{wardrobe}'.", sceneId);
                }
            }
        }

        var syntax = DeterministicSyntaxScorer.Evaluate(fountain);
        if (syntax.TotalSceneHeadings == 0)
            Fail(failures, "FOUNTAIN_SCENE_HEADINGS_MISSING", "screenplay.fountain has no parser-recognized INT./EXT. scene headings and cannot drive a shot plan.");
        else if (syntax.OverallSyntaxScore < 100)
            Warn(warnings, "FOUNTAIN_AUDIT", "The screenplay needs Fountain-format review.");

        return new JsonObject
        {
            ["schema_version"] = "sidecar_validation.v1",
            ["status"] = failures.Count == 0 ? "passed" : "repair_required",
            ["summary"] = new JsonObject
            {
                ["scene_count"] = sceneIds.Count,
                ["audio_scene_count"] = audioByScene.Count,
                ["location_count"] = locationsByKey.Count,
                ["failure_count"] = failures.Count,
                ["warning_count"] = warnings.Count
            },
            ["failures"] = failures,
            ["warnings"] = warnings,
            ["fountain_syntax"] = JsonSerializer.SerializeToNode(syntax)
        };
    }

    private static IReadOnlyList<JsonObject> GetObjectArray(JsonNode? node) => node is JsonArray array
        ? array.OfType<JsonObject>().ToList()
        : Array.Empty<JsonObject>();

    // Planning models may express keyed registries either as an array of records or an object keyed by id.
    // Both are valid as long as each record itself carries the stable key used by cross-file references.
    private static IReadOnlyList<JsonObject> GetObjectCollection(JsonNode? node) => node switch
    {
        JsonArray array => array.OfType<JsonObject>().ToList(),
        JsonObject map => map.Select(item => item.Value).OfType<JsonObject>().ToList(),
        _ => Array.Empty<JsonObject>()
    };

    private static IReadOnlyList<string> GetStringArray(JsonNode? node) => node is JsonArray array
        ? array.OfType<JsonValue>()
            .Select(item => item.TryGetValue<string>(out var value) ? value : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList()
        : Array.Empty<string>();

    private static IReadOnlyList<string> GetKeys(JsonNode? node) => node switch
    {
        JsonObject map => map.Select(item => item.Key).ToList(),
        JsonArray array => array.Select(item => item switch
            {
                JsonValue value when value.TryGetValue<string>(out var key) => key,
                JsonObject record => GetString(record, "variant_key") ?? GetString(record, "key"),
                _ => null
            })
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToList(),
        _ => Array.Empty<string>()
    };

    private static string? GetString(JsonObject node, string key) => node[key]?.GetValue<string>();

    private static void Fail(JsonArray failures, string code, string message, string? sceneId = null) =>
        failures.Add(Issue(code, message, sceneId));

    private static void Warn(JsonArray warnings, string code, string message, string? sceneId = null) =>
        warnings.Add(Issue(code, message, sceneId));

    private static JsonObject Issue(string code, string message, string? sceneId) => new()
    {
        ["code"] = code,
        ["message"] = message,
        ["scene_id"] = sceneId
    };
}
