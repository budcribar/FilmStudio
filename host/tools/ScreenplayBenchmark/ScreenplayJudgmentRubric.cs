using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenplayBenchmark;

public sealed class ScreenplayEvaluationEntry
{
    [JsonPropertyName("screenplayId")]
    public string ScreenplayId { get; set; } = "";

    [JsonPropertyName("adaptationFidelity")]
    public double AdaptationFidelity { get; set; } // 1-10

    [JsonPropertyName("characterDisambiguation")]
    public double CharacterDisambiguation { get; set; } // 1-10

    [JsonPropertyName("aiVideoDirectibility")]
    public double AiVideoDirectibility { get; set; } // 1-10

    [JsonPropertyName("dramaticPacing")]
    public double DramaticPacing { get; set; } // 1-10

    [JsonPropertyName("dialogueAuthenticity")]
    public double DialogueAuthenticity { get; set; } // 1-10

    [JsonPropertyName("soundDesignMusic")]
    public double SoundDesignMusic { get; set; } // 1-10

    [JsonPropertyName("overallQualitativeScore")]
    public double OverallQualitativeScore { get; set; } // 1-10

    /// <summary>
    /// Explicit deal-breaker signal, separate from the 1-10 average so a screenplay with one fatal
    /// flaw (invented plot, broken closed-cast, unusable structure) can't hide behind decent scores
    /// on the other dimensions. Drives "would you actually ship this" independent of ranking.
    /// </summary>
    [JsonPropertyName("productionReady")]
    public bool ProductionReady { get; set; } = true;

    [JsonPropertyName("disqualifyingIssues")]
    public List<string> DisqualifyingIssues { get; set; } = new();

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = "";
}

public sealed class JudgeEvaluationPayload
{
    [JsonPropertyName("evaluations")]
    public List<ScreenplayEvaluationEntry> Evaluations { get; set; } = new();

    /// <summary>
    /// Forced ordinal ranking of Screenplay IDs from best (1st place) to worst.
    /// Example: ["Screenplay B", "Screenplay A", "Screenplay C"]
    /// </summary>
    [JsonPropertyName("forcedRanking")]
    public List<string> ForcedRanking { get; set; } = new();

    [JsonPropertyName("judgeSummaryNotes")]
    public string JudgeSummaryNotes { get; set; } = "";

    [JsonPropertyName("isMock")]
    public bool IsMock { get; set; }

    /// <summary>
    /// Tags which <see cref="ScreenplayJudgmentRubric.RubricVersion"/> produced this evaluation, so a
    /// cached judge result from a since-changed rubric (different dimensions, different JSON schema,
    /// different calibration instructions) is never silently reused as if it reflects the current one.
    /// </summary>
    [JsonPropertyName("rubricVersion")]
    public string RubricVersion { get; set; } = "";
}

public static class ScreenplayJudgmentRubric
{
    /// <summary>
    /// Bump whenever <see cref="BuildPrompt"/>'s instructions or the evaluation JSON schema change
    /// meaningfully — invalidates cached judge results from the prior rubric on the next run.
    /// </summary>
    public const string RubricVersion = "2-calibrated-selfbias-productionready";

    public static string BuildPrompt(string bookText, Dictionary<string, string> anonymizedScreenplays)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You are an expert Hollywood Screenplay Coverage Executive and AI Film Director.");
        sb.AppendLine("Your job is to perform a strict, objective evaluation of candidate screenplay adaptations derived from a source book.");
        sb.AppendLine("These screenplays exist to be broken into short AI-generated video clips (roughly 5-10 seconds each), one clip per beat —");
        sb.AppendLine("that constraint should inform your judgment throughout, especially on directibility and pacing.");
        sb.AppendLine();
        sb.AppendLine("=== SOURCE BOOK TEXT ===");
        sb.AppendLine(bookText);
        sb.AppendLine("=========================");
        sb.AppendLine();
        sb.AppendLine("Below are candidate screenplay adaptations. Each candidate has been given an anonymous label (e.g. Screenplay A, Screenplay B).");
        sb.AppendLine("Evaluate each screenplay package across the following 6 qualitative dimensions (score 1 to 10 for each):");
        sb.AppendLine("1. Adaptation Fidelity & Source Coverage: Accuracy in translating the book's narrative beats, characters, and themes. Treat invented plot events, invented named characters, or dropped major beats as a SEVERE penalty on this dimension specifically (score 3 or below), not just a minor deduction spread across the average.");
        sb.AppendLine("2. Character Disambiguation & Casting Clarity: Whether character intros give distinct, consistent visual descriptions (build, clothing, defining features) that stay consistent every time that character reappears, and whether characters spanning multiple time periods (young vs older) are explicitly disambiguated (e.g. YOUNG NICK / NICK (AGE 8) vs ADULT NICK). Inconsistent wardrobe/appearance across a character's scenes should lower this score — the product locks a reference image per character and needs the description to hold.");
        sb.AppendLine("3. AI Video Directibility (\"Show, Don't Tell\"): Action lines must describe concrete, camera-observable visual actions filmable in a single short clip, avoiding unfilmable internal monologue and avoiding beats that cram multiple distinct actions/locations into one scene heading.");
        sb.AppendLine("4. Dramatic Pacing & Structure: Scene rhythm, tension escalation, transition clarity, and narrative momentum given the short-clip constraint above.");
        sb.AppendLine("5. Dialogue Authenticity & Subtext: Natural, character-distinct dialogue that sounds performable and cinematic, in lines short enough to fit a single clip.");
        sb.AppendLine("6. Sound Design & Background Music Scoring: Evaluates how effectively the background music prompts and sound beds complement each scene's emotional arc, atmosphere, and pacing.");
        sb.AppendLine();
        sb.AppendLine("SCORING CALIBRATION — use the full 1-10 range, do not default to 7-9 for everything:");
        sb.AppendLine("  9-10 = exceptional, no notes; 7-8 = solid, minor fixable issues; 4-6 = workable but flawed, needs real revision; 1-3 = broken or unusable for this dimension.");
        sb.AppendLine("If every candidate looks equally good to you at first pass, look harder for the differences — that is the point of this review. Two candidates should only receive the same score on a dimension if they are genuinely indistinguishable on it.");
        sb.AppendLine();
        sb.AppendLine("SELF-BIAS WARNING: You may be one of the models that authored one of these anonymized screenplays. Grade purely on the merits above.");
        sb.AppendLine("Do not favor a screenplay merely because its prose style, structure, or conventions happen to resemble how you would have written it — that is exactly the bias this blind review is designed to catch.");
        sb.AppendLine();
        sb.AppendLine("PRODUCTION READINESS: For each candidate, also decide productionReady (true/false) independent of the 1-10 scores — false if there is any single deal-breaking issue");
        sb.AppendLine("(major invented content, broken/unusable structure, closed-cast violation, or anything else that would make you refuse to greenlight this draft as-is), even if the averaged scores look fine. List each such issue in disqualifyingIssues.");
        sb.AppendLine();
        sb.AppendLine("=== CANDIDATE SCREENPLAYS ===");
        foreach (var (anonId, content) in anonymizedScreenplays)
        {
            sb.AppendLine($"\n--- START {anonId} ---");
            sb.AppendLine(content);
            sb.AppendLine($"--- END {anonId} ---");
        }
        sb.AppendLine("=============================");
        sb.AppendLine();
        sb.AppendLine("CRITICAL INSTRUCTION: You MUST output ONLY valid JSON matching this exact structure. In judgeSummaryNotes, explicitly name the strongest and weakest candidate and give one sentence of reasoning for each:");
        sb.AppendLine(@"{
  ""evaluations"": [
    {
      ""screenplayId"": ""Screenplay A"",
      ""adaptationFidelity"": 8.5,
      ""characterDisambiguation"": 9.0,
      ""aiVideoDirectibility"": 8.0,
      ""dramaticPacing"": 7.5,
      ""dialogueAuthenticity"": 8.5,
      ""soundDesignMusic"": 8.0,
      ""overallQualitativeScore"": 8.25,
      ""productionReady"": true,
      ""disqualifyingIssues"": [],
      ""rationale"": ""Detailed evaluation rationale...""
    }
  ],
  ""forcedRanking"": [""Screenplay B"", ""Screenplay A""],
  ""judgeSummaryNotes"": ""Strongest: Screenplay B because ... Weakest: Screenplay A because ...""
}");
        return sb.ToString();
    }
}
