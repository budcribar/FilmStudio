using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PageToMovie.Engine;

namespace ScreenplayBenchmark;

public sealed class DeterministicSyntaxResult
{
    public double OverallSyntaxScore { get; set; } // 0 - 100
    public double FormatComplianceScore { get; set; } // 0 - 100
    public double SceneBudgetScore { get; set; } // 0 - 100
    public double DialoguePacingScore { get; set; } // 0 - 100
    public double CharacterDisambiguationScore { get; set; } // 0 - 100
    public double MusicSpecScore { get; set; } // 0 - 100

    public int TotalSceneHeadings { get; set; }
    public int TotalDialogueBlocks { get; set; }
    public double AvgWordsPerDialogue { get; set; }
    public int MaxWordsInSingleDialogue { get; set; }
    public int LongMonologueCount { get; set; }
    public int GenericNumberedSpeakerCount { get; set; }
    public List<string> CharacterNamesFound { get; set; } = new();
    public List<string> AgeDisambiguatedCharacters { get; set; } = new();
    public List<string> DiagnosticWarnings { get; set; } = new();
}

public static class DeterministicSyntaxScorer
{
    private static readonly Regex AgeQualifierRegex = new(
        @"\b(YOUNG|OLD|ADULT|BOY|GIRL|CHILD|ELDER|TINY|BABY|SENIOR|TEEN|TEENAGER)\b|\bAGE\s*\d+\b|\b\d{1,2}s\b|\(\d{1,2}\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenericMusicPlaceholderRegex = new(
        @"\b(some music|background music|music plays|play music|generic music|music sound)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static DeterministicSyntaxResult Evaluate(string fountainText, List<string>? musicBedPrompts = null)
    {
        var result = new DeterministicSyntaxResult();
        if (string.IsNullOrWhiteSpace(fountainText))
        {
            result.DiagnosticWarnings.Add("Screenplay text is empty or null.");
            return result;
        }

        var parseResult = FountainParser.Parse(fountainText);
        var elements = parseResult.Elements;

        // 1. Format Compliance Score
        double formatScore = 100.0;
        if (!fountainText.TrimStart().StartsWith("FADE IN:", StringComparison.OrdinalIgnoreCase))
        {
            formatScore -= 10.0;
            result.DiagnosticWarnings.Add("Missing 'FADE IN:' starting transition.");
        }

        if (fountainText.Contains("[Page ", StringComparison.OrdinalIgnoreCase))
        {
            formatScore -= 15.0;
            result.DiagnosticWarnings.Add("Unstripped book page tags ([Page X]) detected in screenplay.");
        }

        if (fountainText.Contains("/*") && !fountainText.Contains("*/"))
        {
            formatScore -= 25.0;
            result.DiagnosticWarnings.Add("Unclosed boneyard comment block (/*) detected.");
        }

        var sceneHeadings = elements.Where(e => e.Type == FountainParser.ElementType.SceneHeading).ToList();
        result.TotalSceneHeadings = sceneHeadings.Count;
        if (result.TotalSceneHeadings == 0)
        {
            formatScore -= 50.0;
            result.DiagnosticWarnings.Add("No valid INT./EXT. scene headings detected by FountainParser.");
        }

        // Vague location language
        var vagueLocations = BookToFountainConverter.FindVagueLocationHeadings(fountainText);
        if (vagueLocations.Count > 0)
        {
            formatScore -= Math.Min(20.0, vagueLocations.Count * 5.0);
            result.DiagnosticWarnings.Add($"Vague location heading(s) found: {string.Join("; ", vagueLocations.Take(3))}");
        }
        result.FormatComplianceScore = Math.Max(0.0, formatScore);

        // 2. Scene Budget & Granularity Score
        // Soft target: 15 - 30 scenes for a standard adaptation.
        double budgetScore = 100.0;
        if (result.TotalSceneHeadings < 5)
        {
            budgetScore -= 40.0;
            result.DiagnosticWarnings.Add($"Too few scene headings ({result.TotalSceneHeadings}); story lacks visual scene progression.");
        }
        else if (result.TotalSceneHeadings > 45)
        {
            budgetScore -= Math.Min(50.0, (result.TotalSceneHeadings - 45) * 2.0);
            result.DiagnosticWarnings.Add($"Excessive scene count ({result.TotalSceneHeadings} scenes); high micro-scene density inflates video gen budget.");
        }
        result.SceneBudgetScore = Math.Max(0.0, budgetScore);

        // 3. Dialogue Pacing & Word Count Bounds
        var dialogueBlocks = elements.Where(e => e.Type == FountainParser.ElementType.Dialogue).ToList();
        result.TotalDialogueBlocks = dialogueBlocks.Count;
        if (dialogueBlocks.Count > 0)
        {
            var wordCounts = dialogueBlocks.Select(d => d.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length).ToList();
            result.AvgWordsPerDialogue = Math.Round(wordCounts.Average(), 1);
            result.MaxWordsInSingleDialogue = wordCounts.Max();
            result.LongMonologueCount = wordCounts.Count(w => w > 35);

            double pacingScore = 100.0;
            if (result.AvgWordsPerDialogue > 30.0)
            {
                pacingScore -= 20.0;
                result.DiagnosticWarnings.Add($"High average dialogue length ({result.AvgWordsPerDialogue} words/turn); speech beats risk clip overrun.");
            }

            if (result.LongMonologueCount > 0)
            {
                pacingScore -= Math.Min(30.0, result.LongMonologueCount * 5.0);
                result.DiagnosticWarnings.Add($"{result.LongMonologueCount} monologue turn(s) exceed 35 words without action line splits.");
            }
            result.DialoguePacingScore = Math.Max(0.0, pacingScore);
        }
        else
        {
            result.DialoguePacingScore = 80.0; // Text-only / silent scene screenplay
        }

        // 4. Character Age Disambiguation Score
        var charElements = elements.Where(e => e.Type == FountainParser.ElementType.Character).ToList();
        var charNames = charElements.Select(c => c.Text.Trim().Split('(')[0].Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.CharacterNamesFound = charNames;

        int genericCount = 0;
        int ageDisambiguatedCount = 0;
        foreach (var cName in charNames)
        {
            if (BookToFountainConverter.IsGenericNumberedSpeaker(cName))
            {
                genericCount++;
            }

            if (AgeQualifierRegex.IsMatch(cName))
            {
                ageDisambiguatedCount++;
                result.AgeDisambiguatedCharacters.Add(cName);
            }
        }

        result.GenericNumberedSpeakerCount = genericCount;
        double charScore = 100.0;
        if (genericCount > 0)
        {
            charScore -= Math.Min(40.0, genericCount * 10.0);
            result.DiagnosticWarnings.Add($"{genericCount} generic numbered speaker(s) found (e.g. MAN 1, OFFICER 2); replace with proper names.");
        }

        if (ageDisambiguatedCount > 0)
        {
            // Bonus / full score for explicit age-qualification in character headers
            result.DiagnosticWarnings.Add($"Detected {ageDisambiguatedCount} age-qualified character header(s) (e.g. {string.Join(", ", result.AgeDisambiguatedCharacters.Take(3))}).");
        }
        result.CharacterDisambiguationScore = Math.Max(0.0, charScore);

        // 5. Music Specification Audit Score
        double musicScore = 100.0;
        if (musicBedPrompts is not null && musicBedPrompts.Count > 0)
        {
            int emptyPrompts = 0;
            int genericPlaceholders = 0;
            int instrumentalTagged = 0;

            foreach (var prompt in musicBedPrompts)
            {
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    emptyPrompts++;
                }
                else
                {
                    if (GenericMusicPlaceholderRegex.IsMatch(prompt))
                        genericPlaceholders++;
                    if (prompt.Contains("instrumental", StringComparison.OrdinalIgnoreCase) || prompt.Contains("no vocals", StringComparison.OrdinalIgnoreCase))
                        instrumentalTagged++;
                }
            }

            if (emptyPrompts > 0)
            {
                musicScore -= Math.Min(40.0, (emptyPrompts / (double)musicBedPrompts.Count) * 100.0);
                result.DiagnosticWarnings.Add($"{emptyPrompts}/{musicBedPrompts.Count} scene(s) missing music bed prompts.");
            }

            if (genericPlaceholders > 0)
            {
                musicScore -= Math.Min(30.0, genericPlaceholders * 10.0);
                result.DiagnosticWarnings.Add($"{genericPlaceholders} generic music placeholder prompt(s) found.");
            }
            result.MusicSpecScore = Math.Max(0.0, musicScore);
        }
        else
        {
            result.MusicSpecScore = 90.0; // Default when music beds evaluated separately
        }

        // Composite C# Syntax Score
        result.OverallSyntaxScore = Math.Round(
            (result.FormatComplianceScore * 0.25) +
            (result.SceneBudgetScore * 0.20) +
            (result.DialoguePacingScore * 0.20) +
            (result.CharacterDisambiguationScore * 0.20) +
            (result.MusicSpecScore * 0.15), 1);

        return result;
    }
}
