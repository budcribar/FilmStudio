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
}

public static class ScreenplayJudgmentRubric
{
    public static string BuildPrompt(string bookText, Dictionary<string, string> anonymizedScreenplays)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("You are an expert Hollywood Screenplay Coverage Executive and AI Film Director.");
        sb.AppendLine("Your job is to perform a strict, objective evaluation of candidate screenplay adaptations derived from a source book.");
        sb.AppendLine();
        sb.AppendLine("=== SOURCE BOOK TEXT ===");
        sb.AppendLine(bookText);
        sb.AppendLine("=========================");
        sb.AppendLine();
        sb.AppendLine("Below are candidate screenplay adaptations. Each candidate has been given an anonymous label (e.g. Screenplay A, Screenplay B).");
        sb.AppendLine("Evaluate each screenplay package across the following 6 qualitative dimensions (score 1 to 10 for each):");
        sb.AppendLine("1. Adaptation Fidelity & Source Coverage: Accuracy in translating the book's narrative beats, characters, and themes without hallucination or major omission.");
        sb.AppendLine("2. Character Disambiguation & Casting Clarity: Evaluates whether character intros provide distinct visual descriptions and whether characters spanning multiple time periods (young vs older) are explicitly disambiguated (e.g. YOUNG NICK / NICK (AGE 8) vs ADULT NICK).");
        sb.AppendLine("3. AI Video Directibility (\"Show, Don't Tell\"): Action lines must describe concrete, camera-observable visual actions suitable for AI video models (Grok/Veo), avoiding unfilmable internal monologues.");
        sb.AppendLine("4. Dramatic Pacing & Structure: Scene rhythm, tension escalation, transition clarity, and narrative momentum.");
        sb.AppendLine("5. Dialogue Authenticity & Subtext: Natural, character-distinct dialogue that sounds performable and cinematic.");
        sb.AppendLine("6. Sound Design & Background Music Scoring: Evaluates how effectively the background music prompts and sound beds complement each scene's emotional arc, atmosphere, and pacing.");
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
        sb.AppendLine("CRITICAL INSTRUCTION: You MUST output ONLY valid JSON matching this exact structure:");
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
      ""rationale"": ""Detailed evaluation rationale...""
    }
  ],
  ""forcedRanking"": [""Screenplay B"", ""Screenplay A""],
  ""judgeSummaryNotes"": ""Summary of overall comparison...""
}");
        return sb.ToString();
    }
}
