namespace PageToMovie.Web;

/// <summary>
/// Read-aloud scripts for voice clone capture. Kids/storytelling path matches the
/// simple "library book + your voice" product bait (grandparent → bedtime).
/// </summary>
public static class VoiceCloneScripts
{
    /// <summary>Short default (~20–30s) — default for the simple path.</summary>
    public const string KidsShort =
        "Oh, wow! Look over there — a tiny glowing path into the Deepwood, sparkling like fallen stars! " +
        "Come on, if we don't hurry, the magic map will fade before sunset. " +
        "As the silver moon climbed high, the little travelers curled up in their blanket fort. " +
        "Goodnight, stars, murmured Barnaby, drifting off with a peaceful heart.";

    /// <summary>Extended kids script — optional richer clone training.</summary>
    public const string KidsFull = """
        Oh, wow! Look over there! Barnaby the bear cub scrambled up the mossy trunk of the ancient oak. Just beyond the whispering river, a tiny glowing pathway wound into the Deepwood. It sparkled like a million fallen stars! Come on, Pip! If we don't hurry, the magic map will fade away before sunset!

        The woods grew very, very quiet. Even the crickets stopped singing. Pip listened to the rustle of velvet leaves. Suddenly a tiny door in the tree trunk creaked open. Out popped a little clockwork mouse. It whispered: the secret password is bumblebee.

        Ribbit! No splashing in my puddle! croaked Barnaby, pretending to be a grumpy bullfrog. Pip bounced and squeaked: Well, catch me if you can, Captain Croaker!

        As the silver moon climbed high into the velvet sky, the travelers curled up in their cozy blanket fort. The campfire crackled softly. Barnaby yawned on a pillow of moss. Goodnight, stars, he murmured, drifting off with a peaceful heart.
        """;

    /// <summary>Neutral / general film character (legacy).</summary>
    public const string GeneralFilm =
        "Hello. This is my natural speaking voice for a film character. " +
        "I will speak clearly at a calm, steady pace. " +
        "The quick brown fox jumps over the lazy dog near the river at twilight. " +
        "Please measure five, six, seven, eight, nine, and zero. " +
        "Can you hear every word? Yes — I hope this sample helps you match my voice.";

    public static bool LooksLikeChildrensStory(string? title, string? genre, string? projectId)
    {
        var blob = $"{title} {genre} {projectId}".ToLowerInvariant();
        return blob.Contains("buster")
               || blob.Contains("children")
               || blob.Contains("bedtime")
               || blob.Contains("alice")
               || blob.Contains("storybook")
               || blob.Contains("nursery");
    }

    public static string PickDefault(bool simplePath, string? title, string? genre, string? projectId) =>
        simplePath || LooksLikeChildrensStory(title, genre, projectId)
            ? KidsShort
            : GeneralFilm;
}
