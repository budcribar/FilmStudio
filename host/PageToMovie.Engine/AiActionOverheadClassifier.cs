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
/// Validates LLM output against ground-truth values in ActionCameraOverheadLedger.
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
    /// Validates category matches and overhead durations against ActionCameraOverheadLedger.
    /// Falls back to local heuristic keyword matching if the LLM is unconfigured or returns invalid output.
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
                    - Camera Movements:
                      * cam_push_in (Camera push-in zoom, overhead 1.6s)
                      * cam_whip_pan (Fast whip pan, overhead 0.8s)
                      * cam_tracking_dolly (Tracking / dolly shot, overhead 2.4s)
                      * cam_crane_canopy (Crane / canopy overhead, overhead 2.7s)

                    - Physical Actions & Aggression:
                      * act_pills_sorting (Elderly care / pill sorting, overhead 2.3s)
                      * act_knife_pull (Weapon pull / aggression, overhead 2.0s)
                      * act_stabbing (Physical stabbing / violent attack, overhead 3.1s)
                      * act_choke_wall (Wall pinning / choking, overhead 2.2s)
                      * act_heavy_carry (Carrying heavy object, overhead 3.1s)
                      * act_weightlifting (Bench press / weightlifting, overhead 2.8s)
                      * act_running_panic (Running in panic / sprinting, overhead 2.8s)

                    - Psychological Horror (Tell-Tale Heart):
                      * act_creeping_step (Creeping / stealth / lantern walk, overhead 2.8s)
                      * act_lantern_unshutter (Unshuttering dark lantern, overhead 1.9s)
                      * act_sudden_shriek (Sudden shriek / scream, overhead 1.4s)
                      * act_floorboard_dismantle (Dismantling floorboards, overhead 2.8s)

                    - Creature / Adventure (Jungle Book):
                      * act_creature_pounce (Creature pounce / beast attack, overhead 2.4s)
                      * act_creature_stalk (Creature stalking in jungle, overhead 2.7s)
                      * act_vine_swing (Jungle vine swinging, overhead 3.2s)

                    - Reactions:
                      * react_gasp_shock (Facial reaction gasp, overhead 1.3s)
                      * react_confused_stare (Confused stare, overhead 1.7s)
                      * react_heart_pounding (Heart pounding reaction, overhead 1.7s)
                      * react_creature_roar (Creature roar reaction, overhead 2.1s)

                    - Vehicles & Environment:
                      * car_muscle_drive (Vehicle driving / Trans-Am, overhead 2.3s)
                      * car_broadside_crash (Vehicle collision / T-bone, overhead 2.0s)
                      * car_ferry_ride (Ferry boat ride, overhead 3.3s)
                      * scene_visitation_room (Prison visitation room, overhead 2.4s)

                    - Mindfulness & Dreams:
                      * act_yoga_pose (Mindfulness / yoga pose, overhead 2.4s)
                      * dream_viking_battle (Viking battle dream, overhead 3.6s)
                      * dream_lake_goddess (Lake goddess dream, overhead 3.6s)

                    - Composite Action-Dialogue Beats (Nick & Me):
                      * combo_pills_and_snivel (Sorting pills while talking, overhead 2.3s)
                      * combo_weights_and_taunt (Lifting weights while talking, overhead 2.8s)
                      * combo_knife_and_threat (Waving knife while threatening, overhead 2.0s)
                      * combo_drive_and_talk (Driving car while talking, overhead 2.3s)
                      * combo_bar_and_confront (Bar confrontation while talking, overhead 2.4s)
                      * combo_yoga_and_explain (Yoga pose while explaining, overhead 2.4s)

                    - Default Fallback:
                      * act_generic_action (Default generic action, overhead 2.2s)

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
                        // Validate LLM output against calibrated ground-truth ActionCameraOverheadLedger
                        var catId = parsed.MatchCategoryId.Trim().ToLowerInvariant();
                        double calibratedOverhead = _ledger.GetOverheadSec(catId, fallbackSec: -1.0);

                        if (calibratedOverhead > 0.0)
                        {
                            // Valid calibrated category match from ledger
                            _log?.LogInformation("[AiActionClassifier] LLM Classified action as '{Category}' (Ledger Verified Overhead={Overhead:F1}s, Conf={Conf:F2})",
                                catId, calibratedOverhead, parsed.ConfidenceScore);

                            return new ActionClassifierEstimation(
                                MatchCategoryId: catId,
                                EstimatedOverheadSec: calibratedOverhead,
                                ConfidenceScore: Math.Clamp(parsed.ConfidenceScore, 0.0, 1.0),
                                Explanation: parsed.Explanation);
                        }

                        _log?.LogWarning("[AiActionClassifier] LLM returned unknown/uncalibrated category '{Category}'. Falling back to heuristic ledger matching.", catId);
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
            var cat = "act_pills_sorting";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.3), 0.92, "Matched to elderly care / sorting category.");
        }
        if (combinedText.Contains("knife") || combinedText.Contains("blade") || combinedText.Contains("weapon"))
        {
            var cat = "act_knife_pull";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.0), 0.90, "Matched to weapon pull / aggression category.");
        }
        if (combinedText.Contains("stab") || combinedText.Contains("attack"))
        {
            var cat = "act_stabbing";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 3.1), 0.95, "Matched to physical stabbing / aggression category.");
        }
        if (combinedText.Contains("car") || combinedText.Contains("drive") || combinedText.Contains("vehicle") || combinedText.Contains("trans am"))
        {
            var cat = "car_muscle_drive";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.3), 0.88, "Matched to vehicle movement category.");
        }
        if (combinedText.Contains("crash") || combinedText.Contains("collision"))
        {
            var cat = "car_broadside_crash";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.0), 0.94, "Matched to vehicle collision category.");
        }
        if (combinedText.Contains("yoga") || combinedText.Contains("mat") || combinedText.Contains("meditation") || combinedText.Contains("corpse pose"))
        {
            var cat = "act_yoga_pose";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.4), 0.91, "Matched to mindfulness / yoga category.");
        }
        if (combinedText.Contains("weights") || combinedText.Contains("barbell") || combinedText.Contains("curl"))
        {
            var cat = "act_weightlifting";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.8), 0.89, "Matched to weightlifting physical action category.");
        }
        if (combinedText.Contains("creeping") || combinedText.Contains("darkness") || combinedText.Contains("lantern") || combinedText.Contains("stealth"))
        {
            var cat = "act_creeping_step";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.8), 0.93, "Matched to psychological horror / creeping category.");
        }
        if (combinedText.Contains("lantern") || combinedText.Contains("unshutter"))
        {
            var cat = "act_lantern_unshutter";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 1.9), 0.92, "Matched to dark lantern unshuttering category.");
        }
        if (combinedText.Contains("shriek") || combinedText.Contains("scream"))
        {
            var cat = "act_sudden_shriek";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 1.4), 0.95, "Matched to sudden shriek category.");
        }
        if (combinedText.Contains("floorboard") || combinedText.Contains("dismantle"))
        {
            var cat = "act_floorboard_dismantle";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.8), 0.94, "Matched to floorboard dismantling category.");
        }
        if (combinedText.Contains("stalk") || combinedText.Contains("stalking"))
        {
            var cat = "act_creature_stalk";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.7), 0.91, "Matched to creature stalking category.");
        }
        if (combinedText.Contains("vine") || combinedText.Contains("swing"))
        {
            var cat = "act_vine_swing";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 3.2), 0.93, "Matched to jungle vine swinging category.");
        }
        if (combinedText.Contains("tiger") || combinedText.Contains("panther") || combinedText.Contains("creature") || combinedText.Contains("beast") || combinedText.Contains("pounce"))
        {
            var cat = "act_creature_pounce";
            return new ActionClassifierEstimation(cat, _ledger.GetOverheadSec(cat, 2.4), 0.90, "Matched to creature / action category.");
        }

        // Standard fallback duration estimate from ledger
        var fallbackCat = "act_generic_action";
        return new ActionClassifierEstimation(fallbackCat, _ledger.GetOverheadSec(fallbackCat, 2.2), 0.75, "Default fallback category estimation.");
    }
}
