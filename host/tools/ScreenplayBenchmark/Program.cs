using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;

namespace ScreenplayBenchmark;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        TryLoadDotEnv();

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
        bool retryFailed = false;
        bool syntaxOnly = false;

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
            else if (arg.Equals("--retry-failed", StringComparison.OrdinalIgnoreCase) || arg.Equals("--resume", StringComparison.OrdinalIgnoreCase))
            {
                retryFailed = true;
            }
            else if (arg.Equals("--syntax-only", StringComparison.OrdinalIgnoreCase) || arg.Equals("--regrade", StringComparison.OrdinalIgnoreCase))
            {
                syntaxOnly = true;
            }
        }

        var (chat, workspaceRoot) = BuildServices();
        Console.WriteLine($"📂 Workspace root: {workspaceRoot}");

        var historyFilePath = Path.Combine(workspaceRoot, "evals", "benchmark_history.json");
        var historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);

        if (showLeaderboardOnly)
        {
            PrintHistoricalLeaderboard(historyStore);
            return 0;
        }

        if (syntaxOnly)
        {
            await RegradeSyntaxOnlyAsync(historyFilePath, workspaceRoot);
            return 0;
        }

        outDir ??= Path.Combine(workspaceRoot, "evals", "results", $"screenplay_benchmark_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(outDir);

        if (!dryRun && !chat.IsConfigured)
        {
            Console.WriteLine("⚠️  No provider API key found in the environment (XAI_API_KEY / ANTHROPIC_API_KEY / GEMINI_API_KEY).");
            Console.WriteLine("   Generation and peer-judging will fall back to mock data. Pass --dry-run to silence this warning.");
        }

        List<string> bookSuiteFiles = new();
        if (!string.IsNullOrWhiteSpace(suiteDir) && Directory.Exists(suiteDir))
        {
            bookSuiteFiles = Directory.GetFiles(suiteDir, "*.txt", SearchOption.TopDirectoryOnly).ToList();
        }
        else if (string.IsNullOrWhiteSpace(bookPath))
        {
            // Default to curated 5-book benchmark suite
            bookSuiteFiles = LocateDefaultSuiteBooks(workspaceRoot);
        }

        if (bookSuiteFiles.Count > 0)
        {
            Console.WriteLine($"📚 Running Default 5-Book Evaluation Suite across {bookSuiteFiles.Count} stories...");
            foreach (var file in bookSuiteFiles)
            {
                var slug = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                await RunSingleBookBenchmarkAsync(file, slug, outDir, requestedModels, dryRun, retryFailed, historyFilePath, chat, workspaceRoot);
            }

            // Generate updated HTML Dashboard after suite execution
            historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);
            var dashboardHtml = HtmlDashboardGenerator.GenerateHtmlDashboard(historyStore);
            var dashboardFile = Path.Combine(workspaceRoot, "evals", "benchmark_dashboard.html");
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
        await RunSingleBookBenchmarkAsync(bookPath, bookSlug, outDir, requestedModels, dryRun, retryFailed, historyFilePath, chat, workspaceRoot);

        // Generate updated HTML Dashboard
        historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);
        var html = HtmlDashboardGenerator.GenerateHtmlDashboard(historyStore);
        var dashFile = Path.Combine(workspaceRoot, "evals", "benchmark_dashboard.html");
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
        bool retryFailed,
        string historyFilePath,
        IChatClient chat,
        string workspaceRoot)
    {
        var screenplaysDir = Path.Combine(outDir, bookSlug, "screenplays");
        Directory.CreateDirectory(screenplaysDir);

        Console.WriteLine($"📖 Source Book: {bookPath} (Slug: {bookSlug})");
        Console.WriteLine($"🧪 Mode: {(dryRun ? "DRY-RUN (Mock Data)" : chat.IsConfigured ? "LIVE API CALLS" : "NO API KEY (Mock Data)")}");

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

        // Canonical output of the non-AI, book-text-only fallback for THIS book. Every model that
        // hits BookToFountainConverter's internal quality-gate fallback produces this exact text —
        // used below to detect (and refuse to trust) both live fallbacks and previously-poisoned
        // disk cache entries, so a real generation failure never gets silently graded as a model's
        // actual output. See ModelScoreSummary.IsGenerationFallback.
        var canonicalFallbackText = BookToFountainConverter.ConvertHeuristic(
            Path.GetFileNameWithoutExtension(bookPath), BookToFountainConverter.NormalizeBookText(bookText), "Author");
        var generationFallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1 & 2: Generation & C# Audits
        foreach (var modelId in candidateModels)
        {
            Console.Write($"  [Adaptation] Model '{modelId}'... ");
            string screenplayText;

            var screenplayFile = Path.Combine(screenplaysDir, $"{SanitizeFileName(modelId)}.fountain");
            var cacheFile = Path.Combine(workspaceRoot, "evals", "cache", bookSlug, $"{SanitizeFileName(modelId)}.fountain");

            var diskCached = File.Exists(cacheFile) ? await File.ReadAllTextAsync(cacheFile) : null;
            var localCached = File.Exists(screenplayFile) ? await File.ReadAllTextAsync(screenplayFile) : null;

            if (diskCached is not null && !string.Equals(diskCached, canonicalFallbackText, StringComparison.Ordinal))
            {
                screenplayText = diskCached;
                Console.WriteLine("(reused from disk cache)");
            }
            else if (localCached is not null && !string.Equals(localCached, canonicalFallbackText, StringComparison.Ordinal))
            {
                screenplayText = localCached;
                Console.WriteLine("(reused from local run folder)");
            }
            else if (dryRun)
            {
                if (diskCached is not null) Console.Write("(ignoring stale fallback-poisoned cache) ");
                screenplayText = GenerateMockScreenplay(modelId);
                Console.WriteLine("(mock generated)");
            }
            else
            {
                if (diskCached is not null)
                    Console.Write("(ignoring stale fallback-poisoned cache, retrying live) ");
                try
                {
                    screenplayText = await BookToFountainConverter.ConvertAsync(
                        workspaceRoot: outDir,
                        title: Path.GetFileNameWithoutExtension(bookPath),
                        bookText: bookText,
                        author: "Author",
                        chat: chat,
                        model: modelId,
                        onHeuristicFallback: reason => generationFallbacks[modelId] = reason,
                        budgetOverride: ResolveRateLimitSafeBudgetOverride(modelId));

                    if (generationFallbacks.TryGetValue(modelId, out var fallbackReason))
                    {
                        Console.WriteLine($"FALLBACK ({fallbackReason}) — non-AI heuristic draft, not cached, excluded from comparison");
                    }
                    else
                    {
                        Console.WriteLine("DONE");

                        // Only genuine model output is ever persisted to the cross-run disk cache.
                        Directory.CreateDirectory(Path.Combine(workspaceRoot, "evals", "cache", bookSlug));
                        await File.WriteAllTextAsync(cacheFile, screenplayText);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED: {ex.Message}");
                    screenplayText = $"FADE IN:\n\nINT. ERROR - DAY\n\n[Adaptation failed for {modelId}: {ex.Message}]\n\nFADE OUT.";
                    generationFallbacks[modelId] = ex.Message;
                }
            }

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

            var judgeCacheFile = Path.Combine(workspaceRoot, "evals", "cache", bookSlug, $"judge_{judgeModelId}.json");
            JudgeEvaluationPayload? cachedJudge = null;

            if (File.Exists(judgeCacheFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(judgeCacheFile);
                    var loaded = JsonSerializer.Deserialize<JudgeEvaluationPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded is not null && !loaded.IsMock && loaded.Evaluations.Count > 0 && loaded.Evaluations.All(e => e.OverallQualitativeScore >= 0.0)
                        && loaded.RubricVersion == ScreenplayJudgmentRubric.RubricVersion)
                    {
                        cachedJudge = loaded;
                    }
                }
                catch { /* Corrupt cache — re-evaluate */ }
            }

            JudgeEvaluationPayload evalPayload;
            if (cachedJudge is not null && (!retryFailed || !cachedJudge.IsMock))
            {
                evalPayload = cachedJudge;
                Console.WriteLine("DONE (cached live evaluation)");
            }
            else if (dryRun)
            {
                evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                Console.WriteLine("(mock evaluated)");
            }
            else if (!chat.IsConfigured)
            {
                evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                Console.WriteLine("(no provider API key configured — mock evaluated)");
            }
            else
            {
                try
                {
                    var userPrompt = ScreenplayJudgmentRubric.BuildPrompt(bookText, anonScreenplays);
                    var raw = await chat.CompleteAsync(
                        systemPrompt: "Respond with ONLY the JSON object described in the instructions. No prose, no markdown code fences.",
                        userPrompt: userPrompt,
                        model: judgeModelId,
                        temperature: 0.2,
                        mode: "screenplay_benchmark_judge");
                    evalPayload = ParseJudgePayload(raw, anonMapping.Keys);
                    evalPayload.IsMock = false;
                    evalPayload.RubricVersion = ScreenplayJudgmentRubric.RubricVersion;
                    Console.WriteLine("DONE");

                    // Save valid live evaluation to cache
                    Directory.CreateDirectory(Path.Combine(workspaceRoot, "evals", "cache", bookSlug));
                    await File.WriteAllTextAsync(judgeCacheFile, JsonSerializer.Serialize(evalPayload, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAILED ({ex.Message}) — falling back to mock evaluation (-1.0)");
                    evalPayload = GenerateMockJudgePayload(anonMapping, judgeModelId);
                }
            }

            var deAnonymizedPayload = DeAnonymizePayload(evalPayload, anonMapping);
            judgeEvaluations[judgeModelId] = deAnonymizedPayload;
        }

        // Phase 5: Aggregation & History Persistence
        var runData = AggregateBenchmarkData(bookPath, candidateModels, generatedScreenplays, deterministicResults, judgeEvaluations, generationFallbacks);
        var isMockRun = dryRun || judgeEvaluations.Values.All(v => v.IsMock);
        runData.IsMockRun = isMockRun;

        var historyRun = new HistoricalBenchmarkRun
        {
            BookSlug = bookSlug,
            BookTitle = Path.GetFileNameWithoutExtension(bookPath),
            BookPath = bookPath,
            IsMockRun = isMockRun,
            ModelScores = runData.Leaderboard,
            JudgeMatrix = runData.JudgeMatrix,
            SelfBiasNotes = runData.SelfBiasNotes,
        };

        BenchmarkHistoryStore.AppendRun(historyRun, historyFilePath);

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        var jsonFile = Path.Combine(outDir, bookSlug, "run_data.json");
        await File.WriteAllTextAsync(jsonFile, JsonSerializer.Serialize(runData, jsonOpts));

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

    private static List<string> LocateDefaultSuiteBooks(string workspaceRoot)
    {
        var suite = new List<string>();

        // 1. Nick and Me (Contemporary memoir / coastal setting)
        var nickFile = new[] { Path.Combine(workspaceRoot, "books", "Nick_and_Me.txt"), Path.Combine(workspaceRoot, "projects", "NickAndMe", "book_full.txt") }.FirstOrDefault(File.Exists);
        if (nickFile != null) suite.Add(nickFile);

        // 2. The Tell-Tale Heart (Gothic suspense monologue)
        var heartFile = new[] { Path.Combine(workspaceRoot, "books", "The_Tell-Tale_Heart.txt"), Path.Combine(workspaceRoot, "projects", "TellTaleHeartV7", "book_full.txt") }.FirstOrDefault(File.Exists);
        if (heartFile != null) suite.Add(heartFile);

        // 3. Buster (Children's picture book / hero animal)
        var busterFile = new[] { Path.Combine(workspaceRoot, "projects", "Buster", "book_full.txt"), Path.Combine(workspaceRoot, "books", "The_Velveteen_Rabbit.txt") }.FirstOrDefault(File.Exists);
        if (busterFile != null) suite.Add(busterFile);

        // 4. A Christmas Carol (Time-jumps & multi-age character age-splits)
        var carolFile = Path.Combine(workspaceRoot, "books", "A_Christmas_Carol.txt");
        if (File.Exists(carolFile)) suite.Add(carolFile);

        // 5. The Call of the Wild (Hero animal wilderness action directibility)
        var callFile = Path.Combine(workspaceRoot, "books", "The_Call_of_the_Wild.txt");
        if (File.Exists(callFile)) suite.Add(callFile);

        if (suite.Count == 0)
        {
            suite.Add(LocateSampleBookFile(workspaceRoot));
        }

        return suite;
    }

    private static string LocateSampleBookFile(string workspaceRoot)
    {
        var candidates = new[]
        {
            Path.Combine(workspaceRoot, "books", "book.txt"),
            Path.Combine(workspaceRoot, "books", "sample_story.txt"),
            Path.Combine(workspaceRoot, "projects", "Buster", "book_full.txt"),
            Path.Combine(workspaceRoot, "projects", "TellTaleHeartV7", "book_full.txt"),
            Path.Combine(workspaceRoot, "evals", "sample_book.txt"),
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(found)) return found;

        var sampleDir = Path.Combine(workspaceRoot, "evals");
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

    private static (IChatClient Chat, string WorkspaceRoot) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        var workspaceRoot = ResolveWorkspaceRoot();
        services.Configure<PageToMovieOptions>(opts => opts.WorkspaceRoot = workspaceRoot);
        services.AddSingleton<ProjectReadCache>();
        services.AddSingleton<ProjectStore>();
        services.AddSingleton<ProjectTelemetryService>();
        services.AddHttpClient<GrokChatClient>();
        services.AddHttpClient<AnthropicChatClient>();
        services.AddHttpClient<GeminiChatClient>();
        services.AddSingleton<MultiProviderChatClient>();

        var provider = services.BuildServiceProvider();
        var chat = provider.GetRequiredService<MultiProviderChatClient>();
        return (chat, workspaceRoot);
    }

    /// <summary>
    /// Resolves a fixed PageToMovie checkout root so every path this tool writes (evals/cache,
    /// evals/results, benchmark_history.json, benchmark_dashboard.html, and the default books/
    /// projects/ suite lookups) lands in the same place regardless of which directory `dotnet run`
    /// was invoked from. Deliberately does NOT reuse <c>ProjectStore.ResolveWorkspaceRoot</c>'s
    /// Docker/Railway "/data" volume shortcut — on Windows, .NET resolves a leading "/" against the
    /// current drive root, so an unrelated local "C:\data" folder can silently hijack it. Instead
    /// this walks up from the executing assembly looking for the one unambiguous marker this repo
    /// actually has: <c>host/PageToMovie.slnx</c>.
    /// </summary>
    private static string ResolveWorkspaceRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("PageToMovie__WorkspaceRoot");
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
            return Path.GetFullPath(envRoot);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "host", "PageToMovie.slnx")))
                return dir.FullName;
        }

        Console.WriteLine("⚠️  Could not locate the PageToMovie checkout root (no host/PageToMovie.slnx found above this executable) — falling back to the current directory.");
        return Directory.GetCurrentDirectory();
    }

    /// <summary>Parses a judge model's raw completion into a <see cref="JudgeEvaluationPayload"/>, tolerating markdown code fences.</summary>
    private static JudgeEvaluationPayload ParseJudgePayload(string raw, IEnumerable<string> expectedLabels)
    {
        var stripped = ClassifierJsonParser.StripFences(raw);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<JudgeEvaluationPayload>(stripped, opts);

        if (payload is null || payload.Evaluations.Count == 0 || payload.ForcedRanking.Count == 0)
            throw new InvalidOperationException("Judge response was missing evaluations or a forced ranking.");

        var labelSet = new HashSet<string>(expectedLabels, StringComparer.OrdinalIgnoreCase);
        if (!payload.ForcedRanking.All(labelSet.Contains))
            throw new InvalidOperationException("Judge response ranked labels outside the anonymized candidate set.");

        return payload;
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_').Replace('/', '_');

    /// <summary>
    /// Per-model TPM caps confirmed live (e.g. gpt-4o: HTTP 429 "Limit 30000... tokens per min" on
    /// this account/org). These are account-tier rate limits, not the model's real context window —
    /// deliberately kept out of <c>models_catalog.json</c> (which drives the real product's book
    /// adaptation for all users) and scoped to this benchmark only. Forces
    /// <see cref="BookToFountainConverter.ConvertAsync"/> onto the multi-chunk path so each
    /// individual adapt call stays comfortably under the cap instead of one big one-shot request
    /// that blows through it regardless of what the model can actually hold.
    /// </summary>
    private static BookToFountainConverter.PromptBudget? ResolveRateLimitSafeBudgetOverride(string modelId)
    {
        if (!string.Equals(modelId, "gpt-4o", StringComparison.OrdinalIgnoreCase))
            return null;

        return new BookToFountainConverter.PromptBudget
        {
            ModelId = modelId,
            SingleShotBookMaxChars = 50_000,
            ChunkSoftMaxChars = 25_000,
            MaxChunks = BookToFountainConverter.MaxAdaptChunks,
            ReservedOverheadChars = BookToFountainConverter.ReservedOverheadChars,
        };
    }

    private static JudgeEvaluationPayload DeAnonymizePayload(JudgeEvaluationPayload raw, Dictionary<string, string> anonMapping)
    {
        var result = new JudgeEvaluationPayload
        {
            JudgeSummaryNotes = raw.JudgeSummaryNotes,
            // Must carry through: AggregateBenchmarkData relies on this to exclude a failed judge's
            // fabricated (alphabetical-label-order) ForcedRanking from Borda points / rank sums.
            IsMock = raw.IsMock,
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
                ProductionReady = entry.ProductionReady,
                DisqualifyingIssues = entry.DisqualifyingIssues,
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
        Dictionary<string, JudgeEvaluationPayload> judgeEvaluations,
        Dictionary<string, string> generationFallbacks)
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
            runData.JudgeSummaries[judgeId] = payload.JudgeSummaryNotes;
            runData.JudgeRationale[judgeId] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var eval in payload.Evaluations)
            {
                if (!string.IsNullOrWhiteSpace(eval.Rationale))
                    runData.JudgeRationale[judgeId][eval.ScreenplayId] = eval.Rationale; // last-wins if a malformed judge response repeats a screenplayId
            }

            if (payload.IsMock)
            {
                foreach (var key in payload.ForcedRanking)
                {
                    runData.JudgeRankMatrix[judgeId][key] = -1;
                    runData.JudgeMatrix[judgeId][key] = -1.0;
                }
                continue; // Do NOT count points or ranks for mock judges
            }

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
                runData.JudgeMatrix[judgeId][eval.ScreenplayId] = eval.OverallQualitativeScore >= 0.0 ? eval.OverallQualitativeScore : -1.0;
            }
        }

        // Self-bias check: every judge is also a candidate here, so compare each judge's score for
        // its OWN screenplay against the average score OTHER (non-mock) judges gave that same
        // candidate. A judge rating itself well above its peers' consensus is the exact failure mode
        // blind anonymized review is meant to catch.
        const double SelfBiasThreshold = 1.0;
        foreach (var judgeId in candidateModels)
        {
            if (!judgeEvaluations.TryGetValue(judgeId, out var judgePayload) || judgePayload.IsMock) continue;

            var selfEval = judgePayload.Evaluations.FirstOrDefault(e =>
                string.Equals(e.ScreenplayId, judgeId, StringComparison.OrdinalIgnoreCase) && e.OverallQualitativeScore >= 0.0);
            if (selfEval is null) continue;

            var peerScores = judgeEvaluations
                .Where(kv => !string.Equals(kv.Key, judgeId, StringComparison.OrdinalIgnoreCase) && !kv.Value.IsMock)
                .SelectMany(kv => kv.Value.Evaluations)
                .Where(e => string.Equals(e.ScreenplayId, judgeId, StringComparison.OrdinalIgnoreCase) && e.OverallQualitativeScore >= 0.0)
                .Select(e => e.OverallQualitativeScore)
                .ToList();
            if (peerScores.Count == 0) continue;

            var peerAvg = peerScores.Average();
            var delta = selfEval.OverallQualitativeScore - peerAvg;
            if (delta >= SelfBiasThreshold)
            {
                runData.SelfBiasNotes.Add(
                    $"⚠️ {judgeId} rated its own screenplay {selfEval.OverallQualitativeScore:F1}/10 vs. a {peerAvg:F1}/10 average from {peerScores.Count} other judge(s) (+{delta:F1}) — possible self-preference bias.");
            }
            else if (delta <= -SelfBiasThreshold)
            {
                runData.SelfBiasNotes.Add(
                    $"ℹ️ {judgeId} rated its own screenplay {selfEval.OverallQualitativeScore:F1}/10 vs. a {peerAvg:F1}/10 average from {peerScores.Count} other judge(s) ({delta:F1}) — notably harsher on itself than peers were.");
            }
        }

        foreach (var modelId in candidateModels)
        {
            var syntax = deterministicResults[modelId];

            var modelEvals = judgeEvaluations.Values
                .Where(p => !p.IsMock)
                .SelectMany(p => p.Evaluations)
                .Where(e => string.Equals(e.ScreenplayId, modelId, StringComparison.OrdinalIgnoreCase) && e.OverallQualitativeScore >= 0.0)
                .ToList();

            var disqualifyingFlags = judgeEvaluations
                .Where(kv => !kv.Value.IsMock)
                .SelectMany(kv => kv.Value.Evaluations
                    .Where(e => string.Equals(e.ScreenplayId, modelId, StringComparison.OrdinalIgnoreCase) && !e.ProductionReady)
                    .SelectMany(e => e.DisqualifyingIssues.Count > 0
                        ? e.DisqualifyingIssues.Select(issue => $"{kv.Key}: {issue}")
                        : new[] { $"{kv.Key}: flagged not production-ready (no specific issue given)" }))
                .ToList();

            var avgFidelity = modelEvals.Count > 0 ? modelEvals.Average(e => e.AdaptationFidelity) : 0.0;
            var avgCharSplit = modelEvals.Count > 0 ? modelEvals.Average(e => e.CharacterDisambiguation) : 0.0;
            var avgDirect = modelEvals.Count > 0 ? modelEvals.Average(e => e.AiVideoDirectibility) : 0.0;
            var avgPacing = modelEvals.Count > 0 ? modelEvals.Average(e => e.DramaticPacing) : 0.0;
            var avgDialogue = modelEvals.Count > 0 ? modelEvals.Average(e => e.DialogueAuthenticity) : 0.0;
            var avgMusic = modelEvals.Count > 0 ? modelEvals.Average(e => e.SoundDesignMusic) : 0.0;
            var avgQual = modelEvals.Count > 0 ? modelEvals.Average(e => e.OverallQualitativeScore) : 0.0;

            var avgRank = RankCounts[modelId] > 0 ? RankSums[modelId] / RankCounts[modelId] : candidateModels.Count / 2.0;
            var composite = Math.Round((syntax.OverallSyntaxScore * 0.40) + (avgQual * 10.0 * 0.60), 1);

            var isFallback = generationFallbacks.TryGetValue(modelId, out var fallbackReason);

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
                IsGenerationFallback = isFallback,
                GenerationFallbackReason = fallbackReason,
                DisqualifyingFlags = disqualifyingFlags,
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
        var payload = new JudgeEvaluationPayload
        {
            IsMock = true
        };
        var keys = anonMapping.Keys.ToList();
        payload.ForcedRanking = keys;

        foreach (var key in keys)
        {
            payload.Evaluations.Add(new ScreenplayEvaluationEntry
            {
                ScreenplayId = key,
                AdaptationFidelity = -1.0,
                CharacterDisambiguation = -1.0,
                AiVideoDirectibility = -1.0,
                DramaticPacing = -1.0,
                DialogueAuthenticity = -1.0,
                SoundDesignMusic = -1.0,
                OverallQualitativeScore = -1.0,
                ProductionReady = false,
                DisqualifyingIssues = new List<string> { "Not a real assessment — judge call failed or was skipped." },
                Rationale = $"[MOCK / FAILED JUDGE] Model '{judgeId}' failed or was skipped for candidate '{key}'.",
            });
        }
        payload.JudgeSummaryNotes = $"⚠️ Mock judge evaluation returned for {judgeId}.";
        return payload;
    }

    private static void TryLoadDotEnv()
    {
        var dirs = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
        };

        foreach (var dir in dirs.Distinct())
        {
            var envFiles = new[] { Path.Combine(dir, ".env"), Path.Combine(dir, ".env.local") };
            foreach (var envPath in envFiles)
            {
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                        var idx = trimmed.IndexOf('=');
                        if (idx > 0)
                        {
                            var k = trimmed.Substring(0, idx).Trim();
                            var v = trimmed.Substring(idx + 1).Trim(' ', '"', '\'', '\r', '\n', '\t');
                            if (!string.IsNullOrWhiteSpace(k) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k)))
                            {
                                Environment.SetEnvironmentVariable(k, v);
                            }
                        }
                    }
                }
            }
        }
    }

    private static async Task RegradeSyntaxOnlyAsync(string historyFilePath, string workspaceRoot)
    {
        Console.WriteLine("==========================================================================");
        Console.WriteLine(" 🎬 Film Studio — Syntax-Only Re-Grading (0 API Calls)");
        Console.WriteLine("==========================================================================");

        var historyStore = BenchmarkHistoryStore.LoadHistory(historyFilePath);
        if (historyStore.Runs.Count == 0)
        {
            Console.WriteLine("No history runs found to re-grade.");
            return;
        }

        foreach (var run in historyStore.Runs)
        {
            var bookSlug = run.BookSlug;
            Console.WriteLine($"\n📖 Story: '{run.BookTitle}' ({bookSlug}) — Date: {run.Timestamp}");

            string? canonicalFallbackText = null;
            if (File.Exists(run.BookPath))
            {
                var bookText = await File.ReadAllTextAsync(run.BookPath);
                canonicalFallbackText = BookToFountainConverter.ConvertHeuristic(run.BookTitle, BookToFountainConverter.NormalizeBookText(bookText), "Author");
            }

            foreach (var m in run.ModelScores)
            {
                var modelId = m.ModelId;
                var cacheFile = Path.Combine(workspaceRoot, "evals", "cache", bookSlug, $"{SanitizeFileName(modelId)}.fountain");
                if (File.Exists(cacheFile))
                {
                    var screenplayText = await File.ReadAllTextAsync(cacheFile);
                    var newSyntax = DeterministicSyntaxScorer.Evaluate(screenplayText);
                    m.SyntaxAudit = newSyntax;
                    m.IsGenerationFallback = canonicalFallbackText is not null
                        && string.Equals(screenplayText, canonicalFallbackText, StringComparison.Ordinal);

                    // Recompute composite score if live qual score is valid (>= 0)
                    if (m.AvgOverallQualitative >= 0)
                    {
                        m.CompositeScore = Math.Round((newSyntax.OverallSyntaxScore * 0.40) + (m.AvgOverallQualitative * 10.0 * 0.60), 1);
                    }

                    var fallbackTag = m.IsGenerationFallback ? " ⚠️ FALLBACK DRAFT (not real model output)" : "";
                    Console.WriteLine($"  Model '{modelId,-15}' -> Syntax: {newSyntax.OverallSyntaxScore,5:F1}% (Format: {newSyntax.FormatComplianceScore,3:F0}%, Budget: {newSyntax.SceneBudgetScore,3:F0}%, Pacing: {newSyntax.DialoguePacingScore,3:F0}%, Char: {newSyntax.CharacterDisambiguationScore,3:F0}%, Music: {newSyntax.MusicSpecScore,3:F0}%) | Composite: {m.CompositeScore:F1}{fallbackTag}");
                }
                else
                {
                    Console.WriteLine($"  Model '{modelId,-15}' -> Screenplay cache file not found on disk.");
                }
            }
        }

        BenchmarkHistoryStore.SaveHistory(historyStore, historyFilePath);

        var dashboardHtml = HtmlDashboardGenerator.GenerateHtmlDashboard(historyStore);
        var dashboardFile = Path.Combine(workspaceRoot, "evals", "benchmark_dashboard.html");
        await File.WriteAllTextAsync(dashboardFile, dashboardHtml);

        Console.WriteLine("\n✅ Syntax re-grading completed! Global Dashboard updated at:");
        Console.WriteLine($"   🌐 {Path.GetFullPath(dashboardFile)}");
    }
}
