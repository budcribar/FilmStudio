using System.Text;

namespace PageToMovie.Engine.ModelBacked;

/// <summary>
/// Shared user-prompt fragments reused by the per-scene (non-beat-list) chat classifiers
/// (cinematic lighting, color palette grading, wardrobe continuity, …) that do not derive from
/// <see cref="BeatChatClassifierBase{TItem}"/> but still serialize scenes into prompts identically.
/// </summary>
internal static class ClassifierPromptParts
{
    /// <summary>
    /// Appends the shared "SAMPLE BEATS:" block: up to the first 3 <c>story_beats</c> entries, each
    /// rendered as its visual event or, failing that, its spoken dialogue. No-op when the scene has no
    /// beat list.
    /// </summary>
    public static void AppendSampleBeats(StringBuilder sb, Dictionary<string, object?> scene)
    {
        if (scene.TryGetValue("story_beats", out var beatsObj) && beatsObj is List<object?> rawBeats)
        {
            sb.AppendLine("SAMPLE BEATS:");
            var beats = rawBeats.OfType<Dictionary<string, object?>>().Take(3);
            foreach (var b in beats)
            {
                var ve = b.GetValueOrDefault("visual_event");
                var dlg = b.GetValueOrDefault("dialogue");
                if (!string.IsNullOrWhiteSpace(ve?.ToString()))
                    sb.AppendLine($"  - {ve}");
                else if (!string.IsNullOrWhiteSpace(dlg?.ToString()))
                    sb.AppendLine($"  - Spoken: \"{dlg}\"");
            }
        }
    }
}
