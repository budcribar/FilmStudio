using System.Diagnostics;
using System.Text.Json;
using FilmStudio.Engine;

namespace ClassifierBenchmarks;

public static class TaskRunners
{
    public static async Task<TaskResult> RunAmbientAsync(
        BenchPaths paths,
        string projectId,
        string model,
        double temperature,
        PromptBundle prompt,
        ChatRunner chat,
        CancellationToken ct = default)
    {
        var goldPath = paths.GoldFile(projectId, "ambient_sfx");
        if (!File.Exists(goldPath))
            throw new FileNotFoundException($"Missing gold: {goldPath}");

        using var goldDoc = JsonDocument.Parse(await File.ReadAllTextAsync(goldPath, ct));
        var root = goldDoc.RootElement;
        var curated = root.TryGetProperty("curated", out var cEl) && cEl.GetBoolean();
        var labels = root.GetProperty("labels");
        var samples = new List<(string Id, string Visual, string Ga, string Gs)>();
        foreach (var el in labels.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (id.Length == 0) continue;
            var visual = el.TryGetProperty("visual", out var vEl) ? vEl.GetString() ?? "" : "";
            var ga = el.TryGetProperty("gold_ambient", out var aEl) ? aEl.GetString() ?? "" : "";
            var gs = el.TryGetProperty("gold_sfx", out var sEl) ? sEl.GetString() ?? "" : "";
            samples.Add((id, visual, ga, gs));
        }

        var payload = samples.Select(s =>
        {
            var (ha, hs) = FountainStage1Importer.InferAmbientAndSfx(s.Visual);
            var vis = s.Visual.Length > 400 ? s.Visual[..400] + "…" : s.Visual;
            return new
            {
                id = s.Id,
                visual_event = vis,
                heuristic_ambient = ha,
                heuristic_sfx = hs,
            };
        }).ToList();

        var sw = Stopwatch.StartNew();
        var raw = await chat.CompleteAsync(
            model, temperature, prompt.Text,
            "Split each beat into ambient bed vs sfx hits. JSON only.\n" +
            JsonSerializer.Serialize(new { beats = payload }),
            ct);
        sw.Stop();

        var aiMap = AmbientSfxClassifier.ParseLabels(raw);
        var sampleScores = new List<SampleScore>();
        double baseSum = 0, aiSum = 0;
        var hits = 0;
        foreach (var s in samples)
        {
            var (ha, hs) = FountainStage1Importer.InferAmbientAndSfx(s.Visual);
            aiMap.TryGetValue(s.Id, out var pair);
            if (aiMap.ContainsKey(s.Id)) hits++;
            var aa = pair.Ambient ?? "";
            var asx = pair.Sfx ?? "";
            var bScore = (AmbientSfxClassifier.TokenJaccard(ha, s.Ga) + AmbientSfxClassifier.TokenJaccard(hs, s.Gs)) / 2.0;
            var aScore = (AmbientSfxClassifier.TokenJaccard(aa, s.Ga) + AmbientSfxClassifier.TokenJaccard(asx, s.Gs)) / 2.0;
            baseSum += bScore;
            aiSum += aScore;
            sampleScores.Add(new SampleScore
            {
                Id = s.Id,
                Visual = Trunc(s.Visual, 220),
                GoldAmbient = s.Ga,
                GoldSfx = s.Gs,
                BaselineAmbient = ha,
                BaselineSfx = hs,
                AiAmbient = aa,
                AiSfx = asx,
                BaselineScore = bScore,
                AiScore = aScore,
            });
        }

        var n = samples.Count;
        var baseMean = n == 0 ? 0 : baseSum / n;
        var aiMean = n == 0 ? 0 : aiSum / n;
        return new TaskResult
        {
            Task = "ambient_sfx",
            ProjectId = projectId,
            Model = model,
            PromptId = prompt.Id,
            PromptLabel = prompt.Label,
            PromptHash = prompt.Hash,
            Temperature = temperature,
            CuratedGold = curated,
            SampleCount = n,
            Metric = "mean_token_jaccard",
            BaselineScore = baseMean,
            AiScore = aiMean,
            Winner = Winner(baseMean, aiMean),
            LatencyMs = sw.ElapsedMilliseconds,
            AiParseHits = hits,
            Note = prompt.Notes,
            Samples = sampleScores,
        };
    }

    public static async Task<TaskResult> RunSpeciesAsync(
        BenchPaths paths,
        string projectId,
        string model,
        double temperature,
        PromptBundle prompt,
        ChatRunner chat,
        CancellationToken ct = default)
    {
        var goldPath = paths.GoldFile(projectId, "species_kind");
        if (!File.Exists(goldPath))
            throw new FileNotFoundException($"Missing gold: {goldPath}");

        using var goldDoc = JsonDocument.Parse(await File.ReadAllTextAsync(goldPath, ct));
        var root = goldDoc.RootElement;
        var labelsEl = root.TryGetProperty("labels", out var l) ? l : root;
        var samples = new List<(string Key, string Desc, string Gold)>();
        foreach (var el in labelsEl.EnumerateArray())
        {
            var key = el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            if (key.Length == 0) continue;
            var desc = el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var gold = el.TryGetProperty("gold", out var g) ? g.GetString() ?? "" : "";
            samples.Add((key, desc, gold));
        }

        var payload = samples.Select(s => new
        {
            key = s.Key,
            description = Trunc(s.Desc, 280),
            visual_lock = "",
            heuristic = SpeciesKindClassifier.BaselineKind(s.Key, s.Desc, ""),
        }).ToList();

        var sw = Stopwatch.StartNew();
        var raw = await chat.CompleteAsync(
            model, temperature, prompt.Text,
            "Label animal|human|other. JSON only.\n" + JsonSerializer.Serialize(new { cast = payload }),
            ct);
        sw.Stop();

        var aiMap = SpeciesKindClassifier.ParseLabels(raw);
        var sampleScores = new List<SampleScore>();
        int baseOk = 0, aiOk = 0, hits = 0;
        foreach (var s in samples)
        {
            var h = SpeciesKindClassifier.BaselineKind(s.Key, s.Desc, "");
            aiMap.TryGetValue(s.Key, out var ac);
            if (aiMap.ContainsKey(s.Key)) hits++;
            ac ??= "";
            var b = string.Equals(h, s.Gold, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
            var a = string.Equals(ac, s.Gold, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
            baseOk += (int)b;
            aiOk += (int)a;
            sampleScores.Add(new SampleScore
            {
                Id = s.Key,
                Visual = Trunc(s.Desc, 180),
                GoldLabel = s.Gold,
                BaselineLabel = h,
                AiLabel = ac,
                BaselineScore = b,
                AiScore = a,
            });
        }

        var n = samples.Count;
        var baseMean = n == 0 ? 0 : (double)baseOk / n;
        var aiMean = n == 0 ? 0 : (double)aiOk / n;
        return new TaskResult
        {
            Task = "species_kind",
            ProjectId = projectId,
            Model = model,
            PromptId = prompt.Id,
            PromptLabel = prompt.Label,
            PromptHash = prompt.Hash,
            Temperature = temperature,
            CuratedGold = true,
            SampleCount = n,
            Metric = "accuracy",
            BaselineScore = baseMean,
            AiScore = aiMean,
            Winner = Winner(baseMean, aiMean),
            LatencyMs = sw.ElapsedMilliseconds,
            AiParseHits = hits,
            Note = prompt.Notes,
            Samples = sampleScores,
        };
    }

    public static string DefaultPromptId(string task) => task switch
    {
        "ambient_sfx" => "v1_product",
        "species_kind" => "v1_product",
        _ => "v1_product",
    };

    public static string Winner(double baseline, double ai, double eps = 0.02) =>
        Math.Abs(baseline - ai) < eps ? "tie" : ai > baseline ? "AI" : "baseline";

    static string Trunc(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";
}
