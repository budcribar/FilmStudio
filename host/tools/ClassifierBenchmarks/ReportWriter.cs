using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ClassifierBenchmarks;

public static class ReportWriter
{
    public static async Task WriteRunArtifactsAsync(BenchPaths paths, BenchmarkRun run)
    {
        var dir = Path.Combine(paths.Runs, run.RunId);
        Directory.CreateDirectory(dir);
        var summaryPath = Path.Combine(dir, "summary.json");
        var detailsPath = Path.Combine(dir, "details.json");
        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(StripSamples(run), JsonDefaults.Pretty));
        await File.WriteAllTextAsync(detailsPath, JsonSerializer.Serialize(run, JsonDefaults.Pretty));
        await File.WriteAllTextAsync(Path.Combine(dir, "report.md"), BuildRunMarkdown(run));
    }

    public static async Task AppendHistoryAsync(BenchPaths paths, BenchmarkRun run)
    {
        HistoryIndex index;
        if (File.Exists(paths.HistoryIndex))
        {
            index = JsonSerializer.Deserialize<HistoryIndex>(
                await File.ReadAllTextAsync(paths.HistoryIndex), JsonDefaults.Flexible) ?? new HistoryIndex();
        }
        else index = new HistoryIndex();

        index.Runs.RemoveAll(r => r.RunId == run.RunId);
        index.Runs.Insert(0, new HistoryEntry
        {
            RunId = run.RunId,
            Utc = run.Utc,
            ProjectId = run.Config.ProjectId,
            Tasks = run.Config.Tasks.ToList(),
            Models = run.Config.Models.ToList(),
            Prompts = run.Config.Prompts.ToList(),
            SummaryRel = Path.Combine("runs", run.RunId, "summary.json").Replace('\\', '/'),
            Note = run.Config.Note,
            Scores = run.Results.Select(r => new HistoryScore
            {
                Task = r.Task,
                Model = r.Model,
                PromptId = r.PromptId,
                Temperature = r.Temperature,
                Metric = r.Metric,
                BaselineScore = r.BaselineScore,
                AiScore = r.AiScore,
                Winner = r.Winner,
                SampleCount = r.SampleCount,
                CuratedGold = r.CuratedGold,
            }).ToList(),
        });

        // keep last 200 runs
        if (index.Runs.Count > 200)
            index.Runs = index.Runs.Take(200).ToList();

        await File.WriteAllTextAsync(paths.HistoryIndex, JsonSerializer.Serialize(index, JsonDefaults.Pretty));
    }

    public static async Task WriteAggregateReportsAsync(BenchPaths paths)
    {
        HistoryIndex index;
        if (!File.Exists(paths.HistoryIndex))
        {
            await File.WriteAllTextAsync(Path.Combine(paths.Reports, "LATEST.md"),
                "# Classifier benchmarks\n\nNo runs yet. `dotnet run --project host/tools/ClassifierBenchmarks -- run`\n");
            return;
        }

        index = JsonSerializer.Deserialize<HistoryIndex>(
            await File.ReadAllTextAsync(paths.HistoryIndex), JsonDefaults.Flexible) ?? new HistoryIndex();

        var md = BuildHistoryMarkdown(index);
        var html = BuildHistoryHtml(index);
        await File.WriteAllTextAsync(Path.Combine(paths.Reports, "LATEST.md"), md);
        await File.WriteAllTextAsync(Path.Combine(paths.Reports, "history.html"), html);
        await File.WriteAllTextAsync(Path.Combine(paths.Root, "LATEST.md"), md);
        Console.WriteLine($"Reports → {Path.Combine(paths.Reports, "LATEST.md")}");
        Console.WriteLine($"Graphs  → {Path.Combine(paths.Reports, "history.html")}");
    }

    static BenchmarkRun StripSamples(BenchmarkRun run)
    {
        return new BenchmarkRun
        {
            RunId = run.RunId,
            Utc = run.Utc,
            Schema = run.Schema,
            Config = run.Config,
            RepoRoot = run.RepoRoot,
            Error = run.Error,
            Results = run.Results.Select(r => new TaskResult
            {
                Task = r.Task,
                ProjectId = r.ProjectId,
                Model = r.Model,
                PromptId = r.PromptId,
                PromptLabel = r.PromptLabel,
                PromptHash = r.PromptHash,
                Temperature = r.Temperature,
                CuratedGold = r.CuratedGold,
                SampleCount = r.SampleCount,
                Metric = r.Metric,
                BaselineScore = r.BaselineScore,
                AiScore = r.AiScore,
                Winner = r.Winner,
                LatencyMs = r.LatencyMs,
                AiParseHits = r.AiParseHits,
                Note = r.Note,
            }).ToList(),
        };
    }

    public static string BuildRunMarkdown(BenchmarkRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Benchmark run `{run.RunId}`");
        sb.AppendLine();
        sb.AppendLine($"- **UTC:** {run.Utc}");
        sb.AppendLine($"- **Project:** `{run.Config.ProjectId}`");
        sb.AppendLine($"- **Models:** {string.Join(", ", run.Config.Models)}");
        sb.AppendLine($"- **Prompts:** {string.Join(", ", run.Config.Prompts)}");
        sb.AppendLine($"- **Tasks:** {string.Join(", ", run.Config.Tasks)}");
        if (!string.IsNullOrWhiteSpace(run.Config.Note))
            sb.AppendLine($"- **Note:** {run.Config.Note}");
        sb.AppendLine();
        sb.AppendLine("| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |");
        sb.AppendLine("|------|-------|--------|------|--------|---|----------|----|--------|---------|------|");
        foreach (var r in run.Results)
        {
            sb.AppendLine(
                $"| {r.Task} | `{r.Model}` | `{r.PromptId}` | {r.Temperature:0.##} | {r.Metric} | {r.SampleCount} | " +
                $"{r.BaselineScore:F3} | {r.AiScore:F3} | **{r.Winner}** | {r.LatencyMs}ms | " +
                $"{(r.CuratedGold ? "curated" : "draft")} |");
        }

        // Prompt / temp comparison table when multiple cells same model/task
        var groups = run.Results.GroupBy(r => (r.Task, r.Model));
        foreach (var g in groups.Where(x => x.Count() > 1))
        {
            sb.AppendLine();
            sb.AppendLine($"## Compare — `{g.Key.Task}` / `{g.Key.Model}`");
            sb.AppendLine();
            sb.AppendLine("| Prompt | Temp | AI score | vs best | Winner vs baseline |");
            sb.AppendLine("|--------|------|----------|---------|--------------------|");
            var best = g.Max(x => x.AiScore);
            foreach (var r in g.OrderByDescending(x => x.AiScore))
            {
                var delta = r.AiScore - best;
                sb.AppendLine(
                    $"| `{r.PromptId}` | {r.Temperature:0.##} | {r.AiScore:F3} | " +
                    $"{(delta >= -0.0005 ? "best" : delta.ToString("+0.000;-0.000", CultureInfo.InvariantCulture))} | {r.Winner} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Per-sample details: `details.json` in this run folder.");
        return sb.ToString();
    }

    public static string BuildHistoryMarkdown(HistoryIndex index)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Classifier benchmarks — history");
        sb.AppendLine();
        sb.AppendLine($"Updated: {DateTimeOffset.UtcNow:u}");
        sb.AppendLine();
        sb.AppendLine("Open **`reports/history.html`** for interactive charts (model / prompt / task over time).");
        sb.AppendLine();
        sb.AppendLine("## Latest runs");
        sb.AppendLine();
        sb.AppendLine("| When (UTC) | Run | Task | Model | Prompt | Temp | Metric | Baseline | AI | Winner | n |");
        sb.AppendLine("|------------|-----|------|-------|--------|------|--------|----------|----|--------|---|");
        foreach (var run in index.Runs.Take(40))
        {
            foreach (var s in run.Scores)
            {
                sb.AppendLine(
                    $"| {run.Utc} | `{run.RunId}` | {s.Task} | `{s.Model}` | `{s.PromptId}` | {s.Temperature:0.##} | {s.Metric} | " +
                    $"{s.BaselineScore:F3} | {s.AiScore:F3} | **{s.Winner}** | {s.SampleCount} |");
            }
        }

        // Sparkline-ish trend tables per task
        sb.AppendLine();
        sb.AppendLine("## AI score trend by task (newest first)");
        foreach (var task in index.Runs.SelectMany(r => r.Scores).Select(s => s.Task).Distinct().OrderBy(x => x))
        {
            sb.AppendLine();
            sb.AppendLine($"### `{task}`");
            sb.AppendLine();
            sb.AppendLine("| Run | Model | Prompt | Temp | AI | Baseline |");
            sb.AppendLine("|-----|-------|--------|------|----|----------|");
            foreach (var run in index.Runs)
            {
                foreach (var s in run.Scores.Where(x => x.Task == task))
                    sb.AppendLine($"| `{run.RunId}` | `{s.Model}` | `{s.PromptId}` | {s.Temperature:0.##} | {s.AiScore:F3} | {s.BaselineScore:F3} |");
            }
        }

        return sb.ToString();
    }

    public static string BuildHistoryHtml(HistoryIndex index)
    {
        // One series key per task·model·prompt·temp; AI solid + baseline dashed share color.
        var seriesMap = new Dictionary<string, List<(string Utc, double Ai, double Base)>>();
        foreach (var run in index.Runs.AsEnumerable().Reverse())
        {
            foreach (var s in run.Scores)
            {
                var key = SeriesKey(s);
                if (!seriesMap.TryGetValue(key, out var list))
                {
                    list = new List<(string, double, double)>();
                    seriesMap[key] = list;
                }
                list.Add((run.Utc, s.AiScore, s.BaselineScore));
            }
        }

        var labels = index.Runs.AsEnumerable().Reverse().Select(r => r.Utc).Distinct().ToList();
        var colors = new[]
        {
            "#2563eb", "#dc2626", "#16a34a", "#ca8a04", "#9333ea", "#0891b2", "#ea580c", "#4f46e5",
            "#0d9488", "#be185d", "#65a30d", "#7c3aed"
        };

        var datasets = new List<object>();
        var i = 0;
        foreach (var (key, pts) in seriesMap.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var color = colors[i % colors.Length];
            i++;
            var aiData = labels.Select(lab =>
            {
                var hit = pts.LastOrDefault(p => p.Utc == lab);
                return hit.Utc == null ? (double?)null : hit.Ai;
            }).ToList();
            var baseData = labels.Select(lab =>
            {
                var hit = pts.LastOrDefault(p => p.Utc == lab);
                return hit.Utc == null ? (double?)null : hit.Base;
            }).ToList();

            // AI — solid, same color family
            datasets.Add(new Dictionary<string, object?>
            {
                ["label"] = key + " · AI",
                ["data"] = aiData,
                ["borderColor"] = color,
                ["backgroundColor"] = color + "33",
                ["borderWidth"] = 2.5,
                ["spanGaps"] = true,
                ["tension"] = 0.2,
                ["pointRadius"] = 3,
                ["pointHoverRadius"] = 5,
            });
            // Baseline — dashed, identical color (easy pair compare)
            datasets.Add(new Dictionary<string, object?>
            {
                ["label"] = key + " · baseline",
                ["data"] = baseData,
                ["borderColor"] = color,
                ["backgroundColor"] = "transparent",
                ["borderWidth"] = 2,
                ["borderDash"] = new[] { 7, 4 },
                ["spanGaps"] = true,
                ["tension"] = 0,
                ["pointRadius"] = 2,
                ["pointHoverRadius"] = 4,
                ["pointStyle"] = "rectRot",
            });
        }

        var chartPayload = JsonSerializer.Serialize(new { labels, datasets }, JsonDefaults.Pretty);

        var latest = index.Runs.FirstOrDefault();
        var promptRows = new StringBuilder();
        if (latest != null)
        {
            foreach (var s in latest.Scores.OrderBy(x => x.Task).ThenBy(x => x.Model)
                         .ThenBy(x => x.PromptId).ThenBy(x => x.Temperature).ThenByDescending(x => x.AiScore))
            {
                promptRows.AppendLine(
                    $"<tr><td>{Esc(s.Task)}</td><td><code>{Esc(s.Model)}</code></td><td><code>{Esc(s.PromptId)}</code></td>" +
                    $"<td>{s.Temperature:0.##}</td><td>{s.Metric}</td><td>{s.BaselineScore:F3}</td>" +
                    $"<td><strong>{s.AiScore:F3}</strong></td><td>{Esc(s.Winner)}</td><td>{s.SampleCount}</td></tr>");
            }
        }

        var histRows = new StringBuilder();
        foreach (var run in index.Runs.Take(50))
        {
            foreach (var s in run.Scores)
            {
                histRows.AppendLine(
                    $"<tr><td>{Esc(run.Utc)}</td><td><code>{Esc(run.RunId)}</code></td><td>{Esc(s.Task)}</td>" +
                    $"<td><code>{Esc(s.Model)}</code></td><td><code>{Esc(s.PromptId)}</code></td>" +
                    $"<td>{s.Temperature:0.##}</td>" +
                    $"<td>{s.BaselineScore:F3}</td><td>{s.AiScore:F3}</td><td>{Esc(s.Winner)}</td></tr>");
            }
        }

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Classifier benchmarks</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
  <style>
    :root { font-family: ui-sans-serif, system-ui, Segoe UI, sans-serif; color: #0f172a; }
    body { margin: 24px; max-width: 1200px; }
    h1 { font-size: 1.5rem; }
    h2 { font-size: 1.15rem; margin-top: 2rem; }
    .muted { color: #64748b; font-size: 0.9rem; }
    table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
    th, td { border-bottom: 1px solid #e2e8f0; padding: 6px 8px; text-align: left; }
    th { background: #f8fafc; }
    code { font-size: 0.85em; }
    .chart-wrap { background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px; }
    .swatch { display: inline-block; width: 28px; height: 0; border-top: 3px solid #2563eb; vertical-align: middle; margin-right: 4px; }
    .swatch.dash { border-top-style: dashed; }
  </style>
</head>
<body>
  <h1>Classifier benchmarks</h1>
  <p class="muted">AI vs baseline over time · compare models and prompts · curated gold in <code>host/evals/classifier_benchmarks/gold</code></p>
  <p class="muted">
    Legend: <span class="swatch"></span> solid = AI &nbsp;
    <span class="swatch dash"></span> dashed = baseline (same color as its AI pair)
  </p>
  <p class="muted">Generated {{DateTimeOffset.UtcNow:u}}</p>

  <h2>Score over time (AI solid · baseline dashed · matched colors)</h2>
  <div class="chart-wrap"><canvas id="trend" height="140"></canvas></div>

  <h2>Latest run scores</h2>
  <table>
    <thead><tr><th>Task</th><th>Model</th><th>Prompt</th><th>Temp</th><th>Metric</th><th>Baseline</th><th>AI</th><th>Winner</th><th>n</th></tr></thead>
    <tbody>
{{promptRows}}
    </tbody>
  </table>

  <h2>History</h2>
  <table>
    <thead><tr><th>UTC</th><th>Run</th><th>Task</th><th>Model</th><th>Prompt</th><th>Temp</th><th>Baseline</th><th>AI</th><th>Winner</th></tr></thead>
    <tbody>
{{histRows}}
    </tbody>
  </table>

  <script>
    const payload = {{chartPayload}};
    new Chart(document.getElementById('trend'), {
      type: 'line',
      data: payload,
      options: {
        responsive: true,
        interaction: { mode: 'nearest', axis: 'x', intersect: false },
        scales: {
          y: { min: 0, max: 1, title: { display: true, text: 'Score (0–1)' } },
          x: { title: { display: true, text: 'Run time (UTC)' } }
        },
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 16, usePointStyle: false }
          },
          title: { display: false }
        }
      }
    });
  </script>
</body>
</html>
""";
    }

    static string SeriesKey(HistoryScore s)
    {
        var temp = s.Temperature.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{s.Task} · {s.Model} · {s.PromptId} · t={temp}";
    }

    static string Esc(string? s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
