using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

public sealed record MeasuredTimingEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("estimatedDurationSec")] double EstimatedDurationSec,
    [property: JsonPropertyName("actualDurationSec")] double ActualDurationSec,
    [property: JsonPropertyName("deltaSec")] double DeltaSec);

/// <summary>
/// Empirical ledger of action and camera duration overheads measured from ground-truth video benchmarks.
/// Covers Modern Urban Drama (Nick & Me), Psychological Horror (Tell-Tale Heart), and Action/Adventure (Jungle Book).
/// Calculates the Effective Speech Window: Total Clip Duration - Camera Overhead - Action Overhead.
/// Predictively splits shots when speech capacity is exceeded.
/// </summary>
public sealed class ActionCameraOverheadLedger
{
    private static readonly Dictionary<string, double> EmpiricalOverheads = new(StringComparer.OrdinalIgnoreCase)
    {
        // Camera Movements
        ["cam_push_in"] = 1.6,
        ["cam_whip_pan"] = 0.8,
        ["cam_tracking_dolly"] = 2.4,
        ["cam_crane_canopy"] = 2.7,

        // Reactions
        ["react_gasp_shock"] = 1.3,
        ["react_confused_stare"] = 1.7,
        ["react_heart_pounding"] = 1.7,
        ["react_creature_roar"] = 2.1,

        // Physical Actions & Aggression
        ["act_heavy_carry"] = 3.1,
        ["act_weightlifting"] = 2.8,
        ["act_knife_pull"] = 1.9,
        ["act_choke_wall"] = 2.2,
        ["act_stabbing"] = 3.1,
        ["act_running_panic"] = 2.8,
        ["act_pills_sorting"] = 2.9,

        // Psychological Horror (Tell-Tale Heart)
        ["act_creeping_step"] = 2.8,
        ["act_lantern_unshutter"] = 1.9,
        ["act_sudden_shriek"] = 1.4,
        ["act_floorboard_dismantle"] = 2.8,

        // Creature / Adventure (Jungle Book)
        ["act_creature_pounce"] = 2.4,
        ["act_creature_stalk"] = 2.7,
        ["act_vine_swing"] = 3.2,

        // Vehicles & Environment
        ["car_broadside_crash"] = 2.0,
        ["car_muscle_drive"] = 2.3,
        ["car_ferry_ride"] = 3.3,
        ["scene_visitation_room"] = 2.4,

        // Mindfulness & Dreams
        ["act_yoga_pose"] = 2.4,
        ["dream_viking_battle"] = 3.6,
        ["dream_lake_goddess"] = 3.6,
    };

    private readonly ILogger<ActionCameraOverheadLedger>? _log;

    public ActionCameraOverheadLedger(ILogger<ActionCameraOverheadLedger>? log = null)
    {
        _log = log;
    }

    public double GetOverheadSec(string categoryId, double fallbackSec = 1.8)
    {
        if (EmpiricalOverheads.TryGetValue(categoryId, out var overhead))
            return overhead;
        return fallbackSec;
    }

    /// <summary>
    /// Calculates remaining seconds for speech in a clip.
    /// Effective Speech Window = Total Clip Duration - Camera Overhead - Action Overhead.
    /// </summary>
    public double CalculateEffectiveSpeechWindowSec(
        double totalClipDurationSec,
        string? cameraCategoryId = null,
        string? actionCategoryId = null)
    {
        double camOverhead = !string.IsNullOrWhiteSpace(cameraCategoryId) ? GetOverheadSec(cameraCategoryId) : 0.0;
        double actOverhead = !string.IsNullOrWhiteSpace(actionCategoryId) ? GetOverheadSec(actionCategoryId) : 0.0;

        double remaining = totalClipDurationSec - camOverhead - actOverhead;
        return Math.Max(0.0, remaining);
    }

    /// <summary>
    /// Returns max allowed words for a clip based on Effective Speech Window and words-per-second rate.
    /// </summary>
    public int CalculateMaxSpeechWords(
        double totalClipDurationSec,
        string? cameraCategoryId = null,
        string? actionCategoryId = null,
        double wordsPerSecond = 2.6)
    {
        double speechWindow = CalculateEffectiveSpeechWindowSec(totalClipDurationSec, cameraCategoryId, actionCategoryId);
        return (int)Math.Floor(speechWindow * wordsPerSecond);
    }

    /// <summary>
    /// True if beat needs auto-splitting into Action Shot + Dialogue Shot.
    /// </summary>
    public bool ExceedsSpeechCapacity(
        int dialogueWordCount,
        double totalClipDurationSec,
        string? cameraCategoryId = null,
        string? actionCategoryId = null,
        double wordsPerSecond = 2.6)
    {
        int maxWords = CalculateMaxSpeechWords(totalClipDurationSec, cameraCategoryId, actionCategoryId, wordsPerSecond);
        return dialogueWordCount > maxWords;
    }
}
