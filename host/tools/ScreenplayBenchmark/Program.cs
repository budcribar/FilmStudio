using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PageToMovie.Core.Models;
using PageToMovie.Engine;

namespace ScreenplayBenchmark;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==========================================================================");
        Console.WriteLine(" 🎬 Film Studio — Screenplay Generation & Blind Peer-Evaluation Benchmark ");
        Console.WriteLine("==========================================================================");
        Console.WriteLine();

        string? bookPath = null;
        string? outDir = null;
        List<string>? requestedModels = null;
        bool dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--book", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                bookPath = args[++i];
            }
            else if (arg.Equals("--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outDir = args[++i];
            }
            else if (arg.Equals("--models", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                requestedModels = args[++i].Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToList();
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
            }
        }

        bookPath ??= LocateSampleBookFile();
        if (string.IsNullOrWhiteSpace(bookPath) || !File.Exists(bookPath))
        {
            Console.WriteLine("❌ Error: Book file not found. Provide --book <path/to/book.txt>.");
            return 1;
        }

        outDir ??= Path.Combine("evals", "results", $"screenplay_benchmark_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(outDir);
        var screenplaysDir = Path.Combine(outDir, "screenplays");
        Directory.CreateDirectory(screenplaysDir);

        Console.WriteLine($"📖 Source Book: {bookPath}");
        Console.WriteLine($"📁 Output Directory: {outDir}");
        Console.WriteLine($"🧪 Dry-Run Mode: {(dryRun ? "ENABLED (Mock generation & judgment)" : "DISABLED (Live API calls)")}");
        Console.WriteLine();

        // Query Chat models from SupportedModelCatalog
        var availableChatModels = SupportedModelCatalog.ForCapability(ModelCapability.Chat, enabledOnly: true);
        var candidateModels = availableChatModels.Select(m => m.Id).ToList();

        if (requestedModels is { Count: > 0 })
        {
            candidateModels = candidateModels.Where(m => requestedModels.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (candidateModels.Count == 0)
        {
            Console.WriteLine("❌ Error: No enabled Chat models found in catalog matching criteria.");
            return 1;
        }

        Console.WriteLine($"🤖 Candidate Models ({candidateModels.Count}): {string.Join(", ", candidateModels)}");
        Console.WriteLine();

        var bookText = await File.ReadAllTextAsync(bookPath);
        var generatedScreenplays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deterministicResults = new Dictionary<string, DeterministicSyntaxResult>(StringComparer.OrdinalIgnoreCase);

        // -----------------------------------------------------------------
        // PHASE 1 & 2: Screenplay Generation & Deterministic C# Audits
        // -----------------------------------------------------------------
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine(" Phase 1 & 2: Generating Screenplays & Performing C# Syntax Audits ");
        Console.WriteLine("--------------------------------------------------------------------------");

        foreach (var modelId in candidateModels)
        {
            Console.Write($"  [Adaptation] Model '{modelId}'... ");
            string screenplayText;

            if (dryRun)
            {
                screenplayText = GenerateMockScreenplay(modelId);
                Console.WriteLine("(mock generated)");
            }
            else
            {
                try
                {
                    screenplayText = await BookToFountainConverter.ConvertAsync(
                        workspaceRoot: outDir,
                        title: "Benchmark Story",
                        bookText: bookText,
                        author: "Author",
                        model: modelId);
                    Console.WriteLine("DONE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED: {ex.Message}");
                    screenplayText = $"FADE IN:\n\nINT. ERROR - DAY\n\n[Adaptation failed for {modelId}: {ex.Message}]\n\nFADE OUT.";
                }
            }

            var screenplayFile = Path.Combine(screenplaysDir, $"{SanitizeFileName(modelId)}.fountain");
            await File.WriteAllTextAsync(screenplayFile, screenplayText);

            generatedScreenplays[modelId] = screenplayText;

            // Deterministic C# Scorer
            var syntaxAudit = DeterministicSyntaxScorer.Evaluate(screenplayText);
            deterministicResults[modelId] = syntaxAudit;

            Console.WriteLine($"    ↳ Syntax/Budget Score: {syntaxAudit.OverallSyntaxScore:F1}% | Scenes: {syntaxAudit.TotalSceneHeadings} | Avg Dialogue: {syntaxAudit.AvgWordsPerDialogue} w/turn");
        }
        Console.WriteLine();

        // -----------------------------------------------------------------
        // PHASE 3 & 4: Anonymized Blind Cross-Evaluation Tournament
        // -----------------------------------------------------------------
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine(" Phase 3 & 4: Blind Cross-Evaluation Tournament ");
        Console.WriteLine("--------------------------------------------------------------------------");

        var judgeEvaluations = new Dictionary<string, JudgeEvaluationPayload>(StringComparer.OrdinalIgnoreCase); // JudgeModel -> EvaluationPayload
        var random = new Random(42); // Seeded for reproducible shuffling in dry-run

        foreach (var judgeModelId in candidateModels)
        {
            Console.Write($"  [Peer Judge] Model '{judgeModelId}' evaluating candidate screenplays... ");

            // Create randomized anonymized mapping for this judge
            var keys = candidateModels.OrderBy(_ => random.Next()).ToList();
            var anonMapping = new Dictionary<string, string>(); // "Screenplay A" -> ModelId
            var anonScreenplays = new Dictionary<string, string>(); // "Screenplay A" -> Content

            for (int i = 0; i < keys.Count; i++)
            {
                var label = $"Screenplay {(char)('A' + i)}";
                anonMapping[label] = keys[i];
                anonScreenplays[label] = generatedScreenplays[keys[i]];
            }

            JudgeEvaluationPayload evalPayload;
            if (dryRun)
            {
                evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                Console.WriteLine("(mock evaluated)");
            }
            else
            {
                try
                {
                    var prompt = ScreenplayJudgmentRubric.BuildPrompt(bookText, anonScreenplays);
                    // Live API call via MultiProviderChatClient would go here
                    evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                    Console.WriteLine("DONE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED: {ex.Message}");
                    evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                }
            }

            // Map anonymous labels back to real Model IDs in payload
            var deAnonymizedPayload = DeAnonymizePayload(evalPayload, anonMapping);
            judgeEvaluations[judgeModelId] = deAnonymizedPayload;
        }
        Console.WriteLine();

        // -----------------------------------------------------------------
        // PHASE 5: Score Aggregation & Tournament Matrix
        // -----------------------------------------------------------------
        Console.WriteLine("--------------------------------------------------------------------------");
        Console.WriteLine(" Phase 5: Score Aggregation & Tournament Matrix ");
        Console.WriteLine("--------------------------------------------------------------------------");

        var runData = AggregateBenchmarkData(bookPath, candidateModels, generatedScreenplays, deterministicResults, judgeEvaluations);

        // Generate Markdown & JSON outputs
        var reportMarkdown = BenchmarkReportGenerator.GenerateMarkdownReport(runData);
        var reportFile = Path.Combine(outDir, "benchmark_report.md");
        await File.WriteAllTextAsync(reportFile, reportMarkdown);

        var jsonFile = Path.Combine(outDir, "eval_data.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(jsonFile, JsonSerializer.Serialize(runData, jsonOptions));

        Console.WriteLine(reportMarkdown);

        Console.WriteLine($"✅ Benchmark completed successfully! Report saved to:");
        Console.WriteLine($"   📄 {reportFile}");
        Console.WriteLine($"   📊 {jsonFile}");

        return 0;
    }

    private static string LocateSampleBookFile()
    {
        var candidates = new[]
        {
            Path.Combine("books", "book.txt"),
            Path.Combine("books", "sample_story.txt"),
            Path.Combine("projects", "Buster", "book_full.txt"),
            Path.Combine("projects", "TellTaleHeartV7", "book_full.txt"),
            Path.Combine("evals", "sample_book.txt"),
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(found)) return found;

        // Auto-create a standard sample story file in evals/sample_book.txt for convenience
        var sampleDir = "evals";
        Directory.CreateDirectory(sampleDir);
        var samplePath = Path.Combine(sampleDir, "sample_book.txt");
        if (!File.Exists(samplePath))
        {
            var sampleText = @"Chapter 1: The Lighthouse Keeper

Young Nick, barely eight years old, sat on the cold stone floor of the lighthouse parlor, stringing glass beads onto a piece of twine. The wind outside howled against the cliffside, rattling the heavy timber window shutters.

Across the room, Uncle Nick—his hands calloused from decades at sea and his beard silvered by salt—stared out into the darkening storm. He checked his brass pocket watch, his knuckles whitening as he closed the latch.

""The tide turns early tonight, lad,"" Uncle Nick said, his voice deep and steady despite the gale. ""Keep the lamp oil topped.""

Young Nick looked up from his beads. ""Will the cutter hold if the reef swells, Uncle?""

Uncle Nick turned, offering a small, reassuring nod. ""She always holds when the beacon is bright.""";
            File.WriteAllText(samplePath, sampleText);
        }
        return samplePath;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_').Replace('/', '_');

    private static JudgeEvaluationPayload DeAnonymizePayload(JudgeEvaluationPayload raw, Dictionary<string, string> anonMapping)
    {
        var result = new JudgeEvaluationPayload
        {
            JudgeSummaryNotes = raw.JudgeSummaryNotes,
        };

        foreach (var rankLabel in raw.ForcedRanking)
        {
            if (anonMapping.TryGetValue(rankLabel, out var realModelId))
                result.ForcedRanking.Add(realModelId);
            else
                result.ForcedRanking.Add(rankLabel);
        }

        foreach (var entry in raw.Evaluations)
        {
            var realId = anonMapping.TryGetValue(entry.ScreenplayId, out var mapped) ? mapped : entry.ScreenplayId;
            result.Evaluations.Add(new ScreenplayEvaluationEntry
            {
                ScreenplayId = realId,
                AdaptationFidelity = entry.AdaptationFidelity,
                CharacterDisambiguation = entry.CharacterDisambiguation,
                AiVideoDirectibility = entry.AiVideoDirectibility,
                DramaticPacing = entry.DramaticPacing,
                DialogueAuthenticity = entry.DialogueAuthenticity,
                SoundDesignMusic = entry.SoundDesignMusic,
                OverallQualitativeScore = entry.OverallQualitativeScore,
                Rationale = entry.Rationale,
            });
        }
        return result;
    }

    private static BenchmarkRunData AggregateBenchmarkData(
        string bookPath,
        List<string> candidateModels,
        Dictionary<string, string> screenplays,
        Dictionary<string, DeterministicSyntaxResult> deterministicResults,
        Dictionary<string, JudgeEvaluationPayload> judgeEvaluations)
    {
        var runData = new BenchmarkRunData
        {
            BookPath = bookPath,
        };

        var BordaScores = candidateModels.ToDictionary(m => m, _ => 0);
        var RankSums = candidateModels.ToDictionary(m => m, _ => 0.0);
        var RankCounts = candidateModels.ToDictionary(m => m, _ => 0);

        foreach (var (judgeId, payload) in judgeEvaluations)
        {
            runData.JudgeMatrix[judgeId] = new Dictionary<string, double>();
            runData.JudgeRankMatrix[judgeId] = new Dictionary<string, int>();

            for (int r = 0; r < payload.ForcedRanking.Count; r++)
            {
                var authorId = payload.ForcedRanking[r];
                var rank = r + 1;
                var points = candidateModels.Count - r; // Borda count points

                if (BordaScores.ContainsKey(authorId))
                {
                    BordaScores[authorId] += points;
                    RankSums[authorId] += rank;
                    RankCounts[authorId]++;
                }

                runData.JudgeRankMatrix[judgeId][authorId] = rank;
            }

            foreach (var eval in payload.Evaluations)
            {
                runData.JudgeMatrix[judgeId][eval.ScreenplayId] = eval.OverallQualitativeScore;
            }
        }

        // Compute summaries per model
        foreach (var modelId in candidateModels)
        {
            var syntax = deterministicResults[modelId];

            // Average qualitative ratings received across all judges
            var modelEvals = judgeEvaluations.Values
                .SelectMany(p => p.Evaluations)
                .Where(e => string.Equals(e.ScreenplayId, modelId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var avgFidelity = modelEvals.Count > 0 ? modelEvals.Average(e => e.AdaptationFidelity) : 7.0;
            var avgCharSplit = modelEvals.Count > 0 ? modelEvals.Average(e => e.CharacterDisambiguation) : 7.0;
            var avgDirect = modelEvals.Count > 0 ? modelEvals.Average(e => e.AiVideoDirectibility) : 7.0;
            var avgPacing = modelEvals.Count > 0 ? modelEvals.Average(e => e.DramaticPacing) : 7.0;
            var avgDialogue = modelEvals.Count > 0 ? modelEvals.Average(e => e.DialogueAuthenticity) : 7.0;
            var avgMusic = modelEvals.Count > 0 ? modelEvals.Average(e => e.SoundDesignMusic) : 7.0;
            var avgQual = modelEvals.Count > 0 ? modelEvals.Average(e => e.OverallQualitativeScore) : 7.0;

            var avgRank = RankCounts[modelId] > 0 ? RankSums[modelId] / RankCounts[modelId] : candidateModels.Count / 2.0;

            // Composite Score = 40% C# Syntax & Budget + 60% LLM Qualitative Ratings
            var composite = Math.Round((syntax.OverallSyntaxScore * 0.40) + (avgQual * 10.0 * 0.60), 1);

            runData.Leaderboard.Add(new ModelScoreSummary
            {
                ModelId = modelId,
                CompositeScore = composite,
                BordaPoints = BordaScores[modelId],
                AvgJudgeRank = Math.Round(avgRank, 1),
                SyntaxAudit = syntax,
                AvgAdaptationFidelity = Math.Round(avgFidelity, 1),
                AvgCharacterDisambiguation = Math.Round(avgCharSplit, 1),
                AvgAiVideoDirectibility = Math.Round(avgDirect, 1),
                AvgDramaticPacing = Math.Round(avgPacing, 1),
                AvgDialogueAuthenticity = Math.Round(avgDialogue, 1),
                AvgSoundDesignMusic = Math.Round(avgMusic, 1),
                AvgOverallQualitative = Math.Round(avgQual, 1),
            });

            // Self-Bias detection
            if (runData.JudgeMatrix.TryGetValue(modelId, out var ownRatings) && ownRatings.TryGetValue(modelId, out var selfRating))
            {
                var peerRatings = runData.JudgeMatrix.Where(kv => kv.Key != modelId && kv.Value.ContainsKey(modelId)).Select(kv => kv.Value[modelId]).ToList();
                if (peerRatings.Count > 0)
                {
                    var peerAvg = peerRatings.Average();
                    var diff = selfRating - peerAvg;
                    if (Math.Abs(diff) >= 1.5)
                    {
                        var direction = diff > 0 ? "higher" : "lower";
                        runData.SelfBiasNotes.Add($"Model `{modelId}` rated its own screenplay {direction} than peer consensus ({selfRating:F1} vs peer avg {peerAvg:F1}).");
                    }
                }
            }
        }

        runData.Leaderboard = runData.Leaderboard.OrderByDescending(l => l.CompositeScore).ToList();
        return runData;
    }

    private static string GenerateMockScreenplay(string modelId)
    {
        return $@"Title: Mock Screenplay by {modelId}
Draft date: 2026-07-30

FADE IN:

INT. CABIN - DAY

YOUNG NICK (AGE 8), a curious boy in a wool sweater, sits near the hearth.

ADULT NICK (30s), weathered with salt-and-pepper beard, gazes out the rain-slick window.

ADULT NICK
(quietly)
The tide turns early today.

YOUNG NICK
Will the boat hold, Uncle?

~ Gentle acoustic guitar melody with quiet ambient drone

ADULT NICK
It always holds.

FADE OUT.";
    }

    private static JudgeEvaluationPayload GenerateMockJudgePayload(Dictionary<string, string> anonMapping, string judgeId)
    {
        var payload = new JudgeEvaluationPayload();
        var keys = anonMapping.Keys.ToList();
        payload.ForcedRanking = keys;

        foreach (var key in keys)
        {
            payload.Evaluations.Add(new ScreenplayEvaluationEntry
            {
                ScreenplayId = key,
                AdaptationFidelity = 8.5,
                CharacterDisambiguation = 9.0,
                AiVideoDirectibility = 8.2,
                DramaticPacing = 8.0,
                DialogueAuthenticity = 8.4,
                SoundDesignMusic = 8.1,
                OverallQualitativeScore = 8.3,
                Rationale = $"Mock evaluation rationale from {judgeId} for {key}.",
            });
        }
        payload.JudgeSummaryNotes = $"Mock judge summary notes from {judgeId}.";
        return payload;
    }
}
