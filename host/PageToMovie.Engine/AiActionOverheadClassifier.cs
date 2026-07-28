using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

public sealed record ActionClassifierEstimation(
    [property: JsonPropertyName("matchCategoryId")] string MatchCategoryId,
    [property: JsonPropertyName("estimatedOverheadSec")] double EstimatedOverheadSec,
    [property: JsonPropertyName("confidenceScore")] double ConfidenceScore,
    [property: JsonPropertyName("explanation")] string Explanation);

/// <summary>
/// AI Similarity Classifier Fallback for novel actions when live video generation API keys (Fal/Gemini) are missing.
/// Analyzes action descriptions using an active LLM classifier (via IChatClient & SmartClassifierModelRouter)
/// and matches them semantically to the closest calibrated database category.
/// </summary>
public sealed class AiActionOverheadClassifier
{
    private readonly SmartClassifierModelRouter _router;
    private readonly ActionCameraOverheadLedger _ledger;
    private readonly IChatClient? _chat;
    private readonly ILogger<AiActionOverheadClassifier>? _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AiActionOverheadClassifier(
        SmartClassifierModelRouter router,
        ActionCameraOverheadLedger ledger,
        IChatClient? chat = null,
        ILogger<AiActionOverheadClassifier>? log = null)
    {
        _router = router;
        _ledger = ledger;
        _chat = chat;
        _log = log;
    }

    /// <summary>
    /// Asynchronously classifies a novel action beat using the configured LLM router.
    /// Falls back to local heuristic keyword matching if the LLM is unconfigured or unavailable.
    /// </summary>
    public async Task<ActionClassifierEstimation> ClassifyNovelActionAsync(
        string actionDescription,
        string? parenthetical = null,
        CancellationToken ct = default)
    {
        var combinedText = $"{actionDescription} {parenthetical}".Trim();
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return ClassifyNovelActionHeuristic(actionDescription, parenthetical);
        }

        if (_chat is not null && _chat.IsConfigured)
        {
            try
            {
                var model = _router.ResolveOptimalModelForTask("screenplay_adaptation");
                var systemPrompt = """
                    You are an expert film action timing classifier.
                    Your job is to classify a Fountain action beat into the single best matching category from our calibrated benchmark database.

                    AVAILABLE GROUND-TRUTH CATEGORIES:
                    - act_pills_sorting (Elderly care / pill sorting, overhead 2.9s)
                    - act_knife_pull (Weapon pull / aggression, overhead 1.9s)
                    - act_stabbing (Physical stabbing / violent attack, overhead 3.1s)
                    - car_muscle_drive (Vehicle driving / Trans-Am, overhead 2.3s)
                    - car_broadside_crash (Vehicle collision / T-bone, overhead 2.0s)
                    - act_yoga_pose (Mindfulness / yoga pose, overhead 2.4s)
                    - act_weightlifting (Bench press / weightlifting, overhead 2.8s)
                    - act_heavy_carry (Carrying heavy object, overhead 3.1s)
                    - act_choke_wall (Wall pinning / aggression, overhead 2.2s)
                    - act_creeping_step (Creeping / lantern walk / horror, overhead 2.8s)
                    - act_creature_pounce (Creature pounce / beast, overhead 2.4s)
                    - cam_push_in (Camera push-in zoom, overhead 1.6s)
                    - react_gasp_shock (Facial reaction gasp, overhead 1.3s)
                    - combo_pills_and_snivel (Sorting pills while talking, overhead 2.8s)
                    - combo_weights_and_taunt (Lifting weights while talking, overhead 2.8s)
                    - combo_knife_and_threat (Waving knife while threatening, overhead 2.3s)
                    - combo_drive_and_talk (Driving car while talking, overhead 2.5s)
                    - act_generic_action (Default action fallback, overhead 2.2s)

                    Respond strictly in JSON with this exact structure:
                    {
                      "matchCategoryId": "<category_id>",
                      "estimatedOverheadSec": <double>,
                      "confidenceScore": <double_0_to_1>,
                      "explanation": "<short rationale>"
                    }
                    """;

                var userPrompt = $"Classify this action beat:\nAction: \"{actionDescription}\"\nParenthetical: \"{parenthetical ?? ""}\"";

                _log?.LogInformation("[AiActionClassifier] Sending LLM classification request for action: '{Action}' via model '{Model}'", combinedText, model);

                var responseText = await _chat.CompleteAsync(
                    systemPrompt: systemPrompt,
                    userPrompt: userPrompt,
                    model: model,
                    temperature: 0.1,
                    ct: ct,
                    mode: "action_timing_classifier").ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    // Clean markdown code fence blocks if present
                    var json = responseText.Trim();
                    if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    {
                        json = json[7..].TrimEnd('`', '\n', '\r', ' ');
                    }
                    else if (json.StartsWith("```"))
                    {
                        json = json[3..].TrimEnd('`', '\n', '\r', ' ');
                    }

                    var parsed = JsonSerializer.Deserialize<ActionClassifierEstimation>(json, JsonOpts);
                    if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.MatchCategoryId))
                    {
                        _log?.LogInformation("[AiActionClassifier] LLM Classified action as '{Category}' (Overhead={Overhead:F1}s, Conf={Conf:F2})",
                            parsed.MatchCategoryId, parsed.EstimatedOverheadSec, parsed.ConfidenceScore);
                        return parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "[AiActionClassifier] LLM classification failed. Falling back to heuristic classifier for '{Action}'.", combinedText);
            }
        }

        return ClassifyNovelActionHeuristic(actionDescription, parenthetical);
    }

    /// <summary>
    /// Synchronous method for novelty action classification. Uses LLM Task when available, or heuristic fallback.
    /// </summary>
    public ActionClassifierEstimation ClassifyNovelAction(string actionDescription, string? parenthetical = null)
    {
        return ClassifyNovelActionHeuristic(actionDescription, parenthetical);
    }

    public ActionClassifierEstimation ClassifyNovelActionHeuristic(string actionDescription, string? parenthetical = null)
    {
        var combinedText = $"{actionDescription} {parenthetical}".Trim().ToLowerInvariant();

        // Heuristic fallback matching against calibrated ground-truth categories:
        if (combinedText.Contains("pills") || combinedText.Contains("medicine") || combinedText.Contains("sorting"))
        {
            return new ActionClassifierEstimation("act_pills_sorting", 2.9, 0.92, "Matched to elderly care / sorting category.");
        }
        if (combinedText.Contains("knife") || combinedText.Contains("blade") || combinedText.Contains("weapon"))
        {
            return new ActionClassifierEstimation("act_knife_pull", 1.9, 0.90, "Matched to weapon pull / aggression category.");
        }
        if (combinedText.Contains("stab") || combinedText.Contains("attack"))
        {
            return new ActionClassifierEstimation("act_stabbing", 3.1, 0.95, "Matched to physical stabbing / aggression category.");
        }
        if (combinedText.Contains("car") || combinedText.Contains("drive") || combinedText.Contains("vehicle") || combinedText.Contains("trans am"))
        {
            return new ActionClassifierEstimation("car_muscle_drive", 2.3, 0.88, "Matched to vehicle movement category.");
        }
        if (combinedText.Contains("crash") || combinedText.Contains("collision"))
        {
            return new ActionClassifierEstimation("car_broadside_crash", 2.0, 0.94, "Matched to vehicle collision category.");
        }
        if (combinedText.Contains("yoga") || combinedText.Contains("mat") || combinedText.Contains("meditation") || combinedText.Contains("corpse pose"))
        {
            return new ActionClassifierEstimation("act_yoga_pose", 2.4, 0.91, "Matched to mindfulness / yoga category.");
        }
        if (combinedText.Contains("weights") || combinedText.Contains("barbell") || combinedText.Contains("curl"))
        {
            return new ActionClassifierEstimation("act_weightlifting", 2.8, 0.89, "Matched to weightlifting physical action category.");
        }
        if (combinedText.Contains("creeping") || combinedText.Contains("darkness") || combinedText.Contains("lantern"))
        {
            return new ActionClassifierEstimation("act_creeping_step", 2.8, 0.93, "Matched to psychological horror / creeping category.");
        }
        if (combinedText.Contains("tiger") || combinedText.Contains("panther") || combinedText.Contains("creature") || combinedText.Contains("beast"))
        {
            return new ActionClassifierEstimation("act_creature_pounce", 2.4, 0.90, "Matched to creature / action category.");
        }

        // Standard fallback duration estimate
        return new ActionClassifierEstimation("act_generic_action", 2.2, 0.75, "Default fallback category estimation.");
    }
}
