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
        string? suiteDir = null;
        string? outDir = null;
        string? bookSlug = null;
        List<string>? requestedModels = null;
        bool dryRun = false;
        bool showLeaderboardOnly = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--book", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                bookPath = args[++i];
            }
            else if (arg.Equals("--suite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                suiteDir = args[++i];
            }
            else if (arg.Equals("--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outDir = args[++i];
            }
            else if (arg.Equals("--book-slug", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                bookSlug = args[++i];
            }
            else if (arg.Equals("--models", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                requestedModels = args[++i].Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToList();
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
            }
            else if (arg.Equals("--leaderboard", StringComparison.OrdinalIgnoreCase))
            {
                showLeaderboardOnly = true;
            }
        }

        var historyFilePath = Path.Combine("evals", "benchmark_history.json");
        var historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);

        if (showLeaderboardOnly)
        {
            PrintHistoricalLeaderboard(historyStore);
            return 0;
        }

        outDir ??= Path.Combine("evals", "results", $"screenplay_benchmark_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(outDir);

        List<string> bookSuiteFiles = new();
        if (!string.IsNullOrWhiteSpace(suiteDir) && Directory.Exists(suiteDir))
        {
            bookSuiteFiles = Directory.GetFiles(suiteDir, "*.txt", SearchOption.TopDirectoryOnly).ToList();
        }
        else if (string.IsNullOrWhiteSpace(bookPath))
        {
            // Default to curated 5-book benchmark suite
            bookSuiteFiles = LocateDefaultSuiteBooks();
        }

        if (bookSuiteFiles.Count > 0)
        {
            Console.WriteLine($"📚 Running Default 5-Book Evaluation Suite across {bookSuiteFiles.Count} stories...");
            foreach (var file in bookSuiteFiles)
            {
                var slug = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                await RunSingleBookBenchmarkAsync(file, slug, outDir, requestedModels, dryRun, historyFilePath);
            }

            // Generate updated HTML Dashboard after suite execution
            historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);
            var dashboardHtml = HtmlDashboardGenerator.GenerateHtmlDashboard(historyStore);
            var dashboardFile = Path.Combine("evals", "benchmark_dashboard.html");
            await File.WriteAllTextAsync(dashboardFile, dashboardHtml);

            Console.WriteLine();
            Console.WriteLine($"✅ Multi-Book Suite Completed! Global Dashboard updated at:");
            Console.WriteLine($"   🌐 {Path.GetFullPath(dashboardFile)}");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(bookPath) || !File.Exists(bookPath))
        {
            Console.WriteLine("❌ Error: Book file not found. Provide --book <path/to/book.txt> or --suite <dir>.");
            return 1;
        }

        bookSlug ??= Path.GetFileNameWithoutExtension(bookPath).ToLowerInvariant();
        await RunSingleBookBenchmarkAsync(bookPath, bookSlug, outDir, requestedModels, dryRun, historyFilePath);

        // Generate updated HTML Dashboard
        historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);
        var html = HtmlDashboardGenerator.GenerateHtmlDashboard(historyStore);
        var dashFile = Path.Combine("evals", "benchmark_dashboard.html");
        await File.WriteAllTextAsync(dashFile, html);

        Console.WriteLine($"   🌐 Interactive HTML Dashboard: {Path.GetFullPath(dashFile)}");
        return 0;
    }

    private static async Task RunSingleBookBenchmarkAsync(
        string bookPath,
        string bookSlug,
        string outDir,
        List<string>? requestedModels,
        bool dryRun,
        string historyFilePath)
    {
        var screenplaysDir = Path.Combine(outDir, bookSlug, "screenplays");
        Directory.CreateDirectory(screenplaysDir);

        Console.WriteLine($"📖 Source Book: {bookPath} (Slug: {bookSlug})");
        Console.WriteLine($"🧪 Mode: {(dryRun ? "DRY-RUN (Mock Data)" : "LIVE API CALLS")}");

        var availableChatModels = SupportedModelCatalog.ForCapability(ModelCapability.Chat, enabledOnly: true);
        var candidateModels = availableChatModels.Select(m => m.Id).ToList();

        if (requestedModels is { Count: > 0 })
        {
            candidateModels = candidateModels.Where(m => requestedModels.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (candidateModels.Count == 0)
        {
            Console.WriteLine("❌ Error: No enabled Chat models found matching criteria.");
            return;
        }

        Console.WriteLine($"🤖 Candidate Models ({candidateModels.Count}): {string.Join(", ", candidateModels)}");
        var bookText = await File.ReadAllTextAsync(bookPath);
        var generatedScreenplays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deterministicResults = new Dictionary<string, DeterministicSyntaxResult>(StringComparer.OrdinalIgnoreCase);

        // Phase 1 & 2: Generation & C# Audits
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
                        title: Path.GetFileNameWithoutExtension(bookPath),
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

            var syntaxAudit = DeterministicSyntaxScorer.Evaluate(screenplayText);
            deterministicResults[modelId] = syntaxAudit;
        }

        // Phase 3 & 4: Blind Cross-Evaluation
        var judgeEvaluations = new Dictionary<string, JudgeEvaluationPayload>(StringComparer.OrdinalIgnoreCase);
        var random = new Random(42);

        foreach (var judgeModelId in candidateModels)
        {
            Console.Write($"  [Peer Judge] Model '{judgeModelId}'... ");

            var keys = candidateModels.OrderBy(_ => random.Next()).ToList();
            var anonMapping = new Dictionary<string, string>();
            var anonScreenplays = new Dictionary<string, string>();

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
                    evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                    Console.WriteLine("DONE");
                }
                catch
                {
                    evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                }
            }

            var deAnonymizedPayload = DeAnonymizePayload(evalPayload, anonMapping);
            judgeEvaluations[judgeModelId] = deAnonymizedPayload;
        }

        // Phase 5: Aggregation & History Persistence
        var runData = AggregateBenchmarkData(bookPath, candidateModels, generatedScreenplays, deterministicResults, judgeEvaluations);

        var historyRun = new HistoricalBenchmarkRun
        {
            BookSlug = bookSlug,
            BookTitle = Path.GetFileNameWithoutExtension(bookPath),
            BookPath = bookPath,
            ModelScores = runData.Leaderboard,
            JudgeMatrix = runData.JudgeMatrix,
            SelfBiasNotes = runData.SelfBiasNotes,
        };

        BenchmarkHistoryStore.AppendRun(historyRun, historyFilePath);

        var reportMarkdown = BenchmarkReportGenerator.GenerateMarkdownReport(runData);
        var reportFile = Path.Combine(outDir, bookSlug, "benchmark_report.md");
        await File.WriteAllTextAsync(reportFile, reportMarkdown);

        Console.WriteLine($"✅ Benchmark for '{bookSlug}' completed!");
        Console.WriteLine($"   📄 Report: {reportFile}");
    }

    private static void PrintHistoricalLeaderboard(HistoricalStoreContainer historyStore)
    {
        Console.WriteLine("==========================================================================");
        Console.WriteLine(" 🏆 ALL-TIME MULTI-BOOK COMPOSITE MODEL LEADERBOARD ");
        Console.WriteLine("==========================================================================");
        Console.WriteLine();

        var globalLeaderboard = BenchmarkHistoryStore.ComputeGlobalCompositeLeaderboard(historyStore);
        if (globalLeaderboard.Count == 0)
        {
            Console.WriteLine("No benchmark runs recorded in history yet.");
            return;
        }

        Console.WriteLine(string.Format("{0,-6} | {1,-20} | {2,-15} | {3,-12} | {4,-12} | {5,-10}", "Rank", "Model ID", "Multi-Book Score", "C# Syntax", "LLM Peer", "Wins"));
        Console.WriteLine(new string('-', 85));

        for (int i = 0; i < globalLeaderboard.Count; i++)
        {
            var m = globalLeaderboard[i];
            var rank = i switch { 0 => "🥇 1", 1 => "🥈 2", 2 => "🥉 3", _ => $"   {i + 1}" };
            Console.WriteLine(string.Format("{0,-6} | {1,-20} | {2,-15:F1} | {3,-12:F1}% | {4,-12:F1}% | {5,-10}", rank, m.ModelId, m.MultiBookCompositeScore, m.AvgSyntaxScore, m.AvgQualitativeScore, m.FirstPlaceWins));
        }
        Console.WriteLine();
    }

    private static List<string> LocateDefaultSuiteBooks()
    {
        var suite = new List<string>();

        // 1. Nick and Me (Contemporary memoir / coastal setting)
        var nickFile = new[] { Path.Combine("books", "Nick_and_Me.txt"), Path.Combine("projects", "NickAndMe", "book_full.txt") }.FirstOrDefault(File.Exists);
        if (nickFile != null) suite.Add(nickFile);

        // 2. The Tell-Tale Heart (Gothic suspense monologue)
        var heartFile = new[] { Path.Combine("books", "The_Tell-Tale_Heart.txt"), Path.Combine("projects", "TellTaleHeartV7", "book_full.txt") }.FirstOrDefault(File.Exists);
        if (heartFile != null) suite.Add(heartFile);

        // 3. Buster (Children's picture book / hero animal)
        var busterFile = new[] { Path.Combine("projects", "Buster", "book_full.txt"), Path.Combine("books", "The_Velveteen_Rabbit.txt") }.FirstOrDefault(File.Exists);
        if (busterFile != null) suite.Add(busterFile);

        // 4. A Christmas Carol (Time-jumps & multi-age character age-splits)
        var carolFile = Path.Combine("books", "A_Christmas_Carol.txt");
        if (File.Exists(carolFile)) suite.Add(carolFile);

        // 5. The Call of the Wild (Hero animal wilderness action directibility)
        var callFile = Path.Combine("books", "The_Call_of_the_Wild.txt");
        if (File.Exists(callFile)) suite.Add(callFile);

        if (suite.Count == 0)
        {
            suite.Add(LocateSampleBookFile());
        }

        return suite;
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
                var points = candidateModels.Count - r;

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

        foreach (var modelId in candidateModels)
        {
            var syntax = deterministicResults[modelId];

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
