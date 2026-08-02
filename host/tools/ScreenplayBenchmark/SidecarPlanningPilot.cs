using System.Text.Json;
using System.Text.Json.Nodes;
using PageToMovie.Engine.Abstractions;

namespace ScreenplayBenchmark;

/// <summary>
/// Benchmark-only proof of a source-grounded planning package. It writes proposed sidecars for
/// review and never changes an operator project or generates any image/video media.
/// </summary>
internal static class SidecarPlanningPilot
{
    public static async Task<int> RunAsync(string workspaceRoot, string bookPath, string modelId, IChatClient chat, CancellationToken ct = default)
    {
        if (!File.Exists(bookPath)) throw new FileNotFoundException("Book file was not found.", bookPath);
        if (!chat.IsConfigured) throw new InvalidOperationException("No configured chat provider is available.");

        var book = await File.ReadAllTextAsync(bookPath, ct);
        var slug = Path.GetFileNameWithoutExtension(bookPath).ToLowerInvariant();
        var outputDir = Path.Combine(workspaceRoot, "evals", "sidecar_pilots", $"sidecar_pilot_{DateTime.UtcNow:yyyyMMdd_HHmmss}", slug, "source");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"📖 Sidecar pilot source: {bookPath}");
        Console.WriteLine($"🤖 Planning model: {modelId}");
        var raw = await chat.CompleteAsync(
            systemPrompt: "You are a source-faithful film pre-production planner. Return only one valid JSON object. Never invent source facts. If evidence is absent, state unknown or leave the item out.",
            userPrompt: BuildRequest(book),
            model: modelId,
            temperature: 0,
            ct: ct,
            mode: ChatCallModes.SidecarPlanning);

        var root = JsonNode.Parse(StripFences(raw))?.AsObject()
            ?? throw new InvalidOperationException("The sidecar planner did not return a JSON object.");

        await WriteRequiredAsync(root, "adaptation_plan", "adaptation_plan.json", outputDir, ct);
        await WriteRequiredAsync(root, "cast_seeds", "cast_seeds.json", outputDir, ct);
        await WriteRequiredAsync(root, "location_bible", "location_bible.json", outputDir, ct);
        await WriteRequiredAsync(root, "audio_plan", "audio_plan.json", outputDir, ct);
        await WriteRequiredAsync(root, "edit_decision_list", "edit_decision_list.json", outputDir, ct);
        await WriteRequiredAsync(root, "delivery_manifest", "delivery_manifest.json", outputDir, ct);

        var fountain = root["screenplay_fountain"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fountain)) throw new InvalidOperationException("The sidecar planner did not return screenplay_fountain.");
        await File.WriteAllTextAsync(Path.Combine(outputDir, "screenplay.fountain"), fountain.Trim() + Environment.NewLine, ct);

        var validation = SidecarArtifactValidator.Validate(root, fountain);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "validation_report.json"), validation.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);

        await File.WriteAllTextAsync(Path.Combine(outputDir, "pilot_manifest.json"), new JsonObject
        {
            ["schema_version"] = "sidecar_pilot.v1",
            ["book_path"] = bookPath,
            ["model"] = modelId,
            ["requested_temperature"] = 0,
            ["generated_at_utc"] = DateTime.UtcNow.ToString("O"),
            ["media_generated"] = false
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);

        Console.WriteLine($"🧪 Validation: {validation["status"]} ({validation["summary"]?["failure_count"]} repair target(s))");
        Console.WriteLine($"✅ Sidecar pilot saved: {outputDir}");
        return 0;
    }

    private static async Task WriteRequiredAsync(JsonObject root, string key, string fileName, string outputDir, CancellationToken ct)
    {
        var node = root[key] ?? throw new InvalidOperationException($"The sidecar planner did not return {key}.");
        await File.WriteAllTextAsync(Path.Combine(outputDir, fileName), node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
    }

    private static string StripFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence].Trim()
            : trimmed;
    }

    private static string BuildRequest(string book) => $"""
        Create a reviewable, source-grounded pre-production package for the book below.
        Return one JSON object with exactly these keys:
        - adaptation_plan: schema_version, logline, source_grounded_timeline, withheld_name_rules,
          essential_beats, scene_outline. Every character/location/beat must include a short source_evidence quote.
        - cast_seeds: schema_version "cast_seeds.v1", render_style_lock, character_seed_tokens. character_seed_tokens
          MUST be an object keyed by stable cast key (for example "CHAR_PROTAGONIST"), never an array. Each token needs
          description, canonical_given_name, display_name_policy, voice_label, voice_profile, visual_lock,
          age_variants, wardrobe_variants, source_evidence. Describe images only; do not generate images.
        - location_bible: schema_version, render_style_lock, locations. Each location needs a stable key,
          description, layout_anchors, lighting_states, persistent_props, image_briefs, source_evidence,
          and scene_assignments. One heading/scene must use one canonical location.
        - audio_plan: schema_version, project_music_style, scenes. Each scene needs score_intent, timing,
          diegetic_sound, silence_guidance, exclusions, and source_evidence. Describe audio only; do not generate it.
        - edit_decision_list: schema_version, scenes. Each scene needs its location_key, cast_variant_keys,
          wardrobe_variant_keys, purpose, estimated_seconds, and clip_beats.
        - delivery_manifest: schema_version, target_aspect_ratio, target_resolution, caption_requirement,
          credits_requirement, review_gates. Use "operator_choice_required" when the book cannot establish a value.
        - screenplay_fountain: a complete Fountain screenplay only, with no markdown fences.

        Rules: preserve withheld names/twists; do not invent named speakers or quoted dialogue; use stable cast and
        location keys; put image, wardrobe, casting, and production metadata in the JSON sidecars, not Fountain.
        Keep the package concise enough for a short-film adaptation.

        BOOK:
        {book}
        """;
}
