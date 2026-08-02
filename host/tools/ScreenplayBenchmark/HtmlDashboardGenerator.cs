using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ScreenplayBenchmark;

public static class HtmlDashboardGenerator
{
    public static string GenerateHtmlDashboard(
        HistoricalStoreContainer historyContainer, string? currentWorkingTreePromptVersion = null, string? currentGitHeadPromptVersion = null)
    {
        var globalLeaderboard = BenchmarkHistoryStore.ComputeGlobalCompositeLeaderboard(historyContainer);
        var judgeLeaderboard = BenchmarkHistoryStore.ComputeJudgeLeaderboard(historyContainer);
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
        var historyJson = JsonSerializer.Serialize(historyContainer, jsonOpts);
        var globalJson = JsonSerializer.Serialize(globalLeaderboard, jsonOpts);
        var judgeJson = JsonSerializer.Serialize(judgeLeaderboard, jsonOpts);
        var currentWorkingTreeJson = JsonSerializer.Serialize(currentWorkingTreePromptVersion, jsonOpts);
        var currentGitHeadJson = JsonSerializer.Serialize(currentGitHeadPromptVersion, jsonOpts);

        var sb = new StringBuilder();
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Film Studio — Screenplay Model Benchmark Dashboard</title>
  <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
  <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
  <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&family=Fira+Code:wght@400;500&display=swap"" rel=""stylesheet"">
  <style>
    :root {
      --bg-main: #0b0f19;
      --bg-card: rgba(18, 26, 43, 0.75);
      --border-card: rgba(255, 255, 255, 0.08);
      --accent-gold: #fbbf24;
      --accent-silver: #9ca3af;
      --accent-bronze: #d97706;
      --accent-cyan: #38bdf8;
      --accent-purple: #a855f7;
      --text-main: #f3f4f6;
      --text-muted: #9ca3af;
      --font-main: 'Inter', sans-serif;
      --font-code: 'Fira Code', monospace;
    }

    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background-color: var(--bg-main);
      color: var(--text-main);
      font-family: var(--font-main);
      line-height: 1.5;
      padding: 2rem;
      min-height: 100vh;
    }

    header {
      margin-bottom: 2rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid var(--border-card);
      padding-bottom: 1.5rem;
    }

    .title-group h1 {
      font-size: 1.875rem;
      font-weight: 800;
      background: linear-gradient(135deg, #38bdf8 0%, #a855f7 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .title-group p { color: var(--text-muted); font-size: 0.875rem; margin-top: 0.25rem; }

    .date-badge {
      display: inline-block;
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.3);
      color: var(--accent-cyan);
      padding: 0.25rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
      margin-left: 0.75rem;
    }

    .nav-tabs {
      display: flex;
      gap: 0.75rem;
      margin-bottom: 2rem;
      border-bottom: 1px solid var(--border-card);
      padding-bottom: 0.75rem;
    }
    .tab-btn {
      background: transparent;
      border: 1px solid transparent;
      color: var(--text-muted);
      padding: 0.6rem 1.25rem;
      border-radius: 0.5rem;
      font-weight: 600;
      font-size: 0.9rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .tab-btn:hover { background: rgba(255, 255, 255, 0.05); color: var(--text-main); }
    .tab-btn.active {
      background: rgba(56, 189, 248, 0.15);
      border-color: rgba(56, 189, 248, 0.4);
      color: var(--accent-cyan);
    }

    .tab-content { display: none; }
    .tab-content.active { display: block; }

    .card {
      background: var(--bg-card);
      backdrop-filter: blur(12px);
      border: 1px solid var(--border-card);
      border-radius: 0.75rem;
      padding: 1.5rem;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.3);
    }

    .card h3 { font-size: 1.1rem; font-weight: 700; margin-bottom: 0.5rem; }

    table {
      width: 100%;
      border-collapse: collapse;
      margin-top: 1rem;
      font-size: 0.9rem;
    }
    th, td {
      padding: 0.85rem 1rem;
      text-align: left;
      border-bottom: 1px solid var(--border-card);
    }
    th {
      background: rgba(255, 255, 255, 0.03);
      color: var(--text-muted);
      font-weight: 600;
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    tr:hover td { background: rgba(255, 255, 255, 0.02); }

    .score-badge {
      display: inline-block;
      padding: 0.25rem 0.6rem;
      border-radius: 0.375rem;
      font-weight: 700;
      font-size: 0.85rem;
    }
    .score-high { background: rgba(34, 197, 94, 0.15); color: #4ade80; border: 1px solid rgba(34, 197, 94, 0.3); }
    .score-mid { background: rgba(234, 179, 8, 0.15); color: #facc15; border: 1px solid rgba(234, 179, 8, 0.3); }
    .score-mock { background: rgba(239, 68, 68, 0.15); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.3); }

    select {
      background: var(--bg-card);
      color: var(--text-main);
      border: 1px solid var(--border-card);
      padding: 0.5rem 1rem;
      border-radius: 0.5rem;
      font-size: 0.9rem;
      outline: none;
      cursor: pointer;
    }
  </style>
</head>
<body>

  <header>
    <div class=""title-group"">
      <h1>🎬 Film Studio — Model Benchmark Dashboard</h1>
      <p>Screenplay Adaptation & Peer-Evaluation Leaderboard over Time <span class=""date-badge"">📅 Last Run: <strong id=""last-run-header"">Loading...</strong></span></p>
    </div>
  </header>

  <nav class=""nav-tabs"">
    <button class=""tab-btn active"" onclick=""switchTab('global')"">🏆 Multi-Book Global Leaderboard</button>
    <button class=""tab-btn"" onclick=""switchTab('perbook')"">📚 Per-Book History</button>
    <button class=""tab-btn"" onclick=""switchTab('heatmap')"">⚖️ Peer Judge Heatmap</button>
    <button class=""tab-btn"" onclick=""switchTab('judges')"">🧑‍⚖️ Judge Leaderboard</button>
    <button class=""tab-btn"" onclick=""switchTab('promptversions')"">📝 Prompt Versions</button>
    <button class=""tab-btn"" onclick=""switchTab('progress')"">📈 Progress Over Time</button>
  </nav>

  <!-- TAB 1: GLOBAL MULTI-BOOK LEADERBOARD -->
  <div id=""tab-global"" class=""tab-content active"">
    <div class=""card"">
      <div style=""display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;"">
        <div style=""display: flex; align-items: center; gap: 1rem;"">
          <h3>🏆 Multi-Book Composite Model Rankings</h3>
          <button class=""tab-btn"" onclick=""toggleSyntaxExpand()"" id=""syntax-toggle-btn"" style=""padding: 0.35rem 0.85rem; font-size: 0.8rem; border-radius: 4px; border: 1px solid var(--accent-cyan); color: var(--accent-cyan);"">🔍 Expand Syntax Breakdown</button>
          <button class=""tab-btn"" onclick=""togglePeerExpand()"" id=""peer-toggle-btn"" style=""padding: 0.35rem 0.85rem; font-size: 0.8rem; border-radius: 4px; border: 1px solid var(--accent-purple); color: var(--accent-purple);"">🔍 Expand LLM Peer Breakdown</button>
        </div>
        <span style=""font-size: 0.85rem; color: var(--text-muted);"">Date Run: <strong id=""global-date-subtitle"" style=""color: var(--text-main);"">—</strong></span>
      </div>
      <p style=""color: var(--text-muted); font-size: 0.85rem;"">Aggregated average composite scores (40% C# Syntax + 60% LLM Peer Ratings) across all benchmarked books in the evaluation suite. Click any column header to sort.</p>

      <div style=""display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.5rem; padding: 0.75rem; background: rgba(255,255,255,0.02); border: 1px solid var(--border-card); border-radius: 0.5rem;"">
        <span style=""font-size: 0.8rem; color: var(--text-muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;"">Books:</span>
        <div id=""global-book-filter"" style=""display: flex; gap: 1rem; flex-wrap: wrap; flex: 1;""></div>
        <button class=""tab-btn"" onclick=""setAllGlobalBooks(true)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Select All</button>
        <button class=""tab-btn"" onclick=""setAllGlobalBooks(false)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Clear</button>
      </div>
      <div style=""display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.02); border: 1px solid var(--border-card); border-radius: 0.5rem;"">
        <span style=""font-size: 0.8rem; color: var(--text-muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;"" title=""Runs at different reasoning-effort levels are not directly comparable — mixing them blends different conditions into one average."">Reasoning Effort:</span>
        <div id=""global-effort-filter"" style=""display: flex; gap: 1rem; flex-wrap: wrap; flex: 1;""></div>
        <button class=""tab-btn"" onclick=""setAllGlobalEfforts(true)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Select All</button>
        <button class=""tab-btn"" onclick=""setAllGlobalEfforts(false)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Clear</button>
      </div>
      <div style=""display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.02); border: 1px solid var(--border-card); border-radius: 0.5rem;"">
        <span style=""font-size: 0.8rem; color: var(--text-muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;"" title=""Committed Git revision that last changed prompts/book_to_fountain.txt. Benchmarks refuse uncommitted prompt edits."">Prompt Commit:</span>
        <div id=""global-prompt-filter"" style=""display: flex; gap: 1rem; flex-wrap: wrap; flex: 1;""></div>
        <button class=""tab-btn"" onclick=""setAllGlobalPromptVersions(true)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Select All</button>
        <button class=""tab-btn"" onclick=""setAllGlobalPromptVersions(false)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Clear</button>
      </div>

      <table id=""global-table"">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Model ID</th>
            <th>Multi-Book Composite</th>
            <th>C# Syntax Avg</th>
            <th>LLM Peer Avg</th>
            <th>1st Place Wins</th>
            <th>Books Evaluated (Live)</th>
          </tr>
        </thead>
        <tbody id=""global-tbody"">
        </tbody>
      </table>
    </div>
  </div>

  <!-- TAB 2: PER-BOOK HISTORY -->
  <div id=""tab-perbook"" class=""tab-content"">
    <div class=""card"">
      <div style=""display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;"">
        <h3>📚 Book Score History</h3>
        <div>
          <label style=""font-size: 0.85rem; color: var(--text-muted); margin-right: 0.5rem;"">Select Book:</label>
          <select id=""book-select"" onchange=""renderPerBookTable()""></select>
        </div>
      </div>

      <table id=""perbook-table"">
        <thead>
          <tr>
            <th>Date Run</th>
            <th>Reasoning Effort</th>
            <th>Temperature</th>
            <th>Prompt Version</th>
            <th>Model ID</th>
            <th>Composite Score</th>
            <th>Syntax Score</th>
            <th>Peer Score</th>
            <th>Borda Pts</th>
          </tr>
        </thead>
        <tbody id=""perbook-tbody"">
        </tbody>
      </table>
    </div>
  </div>

  <!-- TAB 3: PEER JUDGE HEATMAP -->
  <div id=""tab-heatmap"" class=""tab-content"">
    <div class=""card"">
      <div style=""display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;"">
        <h3>⚖️ Peer Judge Cross-Evaluation Matrix</h3>
        <div>
          <label style=""font-size: 0.85rem; color: var(--text-muted); margin-right: 0.5rem;"">Select Book:</label>
          <select id=""heatmap-book-select"" onchange=""renderHeatmap()""></select>
        </div>
      </div>
      <p style=""color: var(--text-muted); font-size: 0.85rem; margin-bottom: 1rem;"">Cross-tabulation showing how peer judge models evaluated candidate screenplays for the selected book.</p>
      <div id=""heatmap-container""></div>
    </div>
  </div>

  <!-- TAB 4: JUDGE LEADERBOARD -->
  <div id=""tab-judges"" class=""tab-content"">
    <div class=""card"">
      <h3>🧑‍⚖️ Judge Reliability & Self-Bias Leaderboard</h3>
      <p style=""color: var(--text-muted); font-size: 0.85rem;"">Ranks models by how trustworthy they are AS JUDGES (not as candidates): reliability = fraction of books judged without falling back to mock data; self-bias = how a judge's score of its own screenplay compares to peer judges' scores of that same screenplay (near zero is best).</p>

      <table id=""judges-table"">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Model ID</th>
            <th>Reliability</th>
            <th>Net Self-Bias</th>
            <th>Abs Self-Bias</th>
            <th>Books Judged</th>
            <th>Self-Bias Samples</th>
          </tr>
        </thead>
        <tbody id=""judges-tbody"">
        </tbody>
      </table>
    </div>
  </div>

  <!-- TAB 5: PROMPT VERSIONS -->
  <div id=""tab-promptversions"" class=""tab-content"">
    <div class=""card"" style=""margin-bottom: 1.5rem;"">
      <h3>📝 Tracked Prompt Commits</h3>
      <p style=""color: var(--text-muted); font-size: 0.85rem;"">Every committed prompt revision used for a benchmark. A benchmark cannot start while the prompt has uncommitted edits.</p>
      <table id=""promptversions-table"">
        <thead>
          <tr>
            <th>Version</th>
            <th>First Seen</th>
            <th>Status</th>
            <th>Books × Models Tested</th>
          </tr>
        </thead>
        <tbody id=""promptversions-tbody""></tbody>
      </table>
    </div>

    <div class=""card"">
      <h3>🔀 Compare Two Versions</h3>
      <p style=""color: var(--text-muted); font-size: 0.85rem;"">Average composite-score change per model, counting only books/effort-levels where BOTH versions have real (non-fallback) data — so a gap in one version never silently skews the delta.</p>
      <div style=""display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; margin: 1rem 0;"">
        <label style=""font-size: 0.85rem; color: var(--text-muted);"">From (older):
          <select id=""pv-compare-from"" onchange=""renderPromptComparison()"" style=""margin-left: 0.5rem;""></select>
        </label>
        <label style=""font-size: 0.85rem; color: var(--text-muted);"">To (newer):
          <select id=""pv-compare-to"" onchange=""renderPromptComparison()"" style=""margin-left: 0.5rem;""></select>
        </label>
      </div>
      <div style=""display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem; padding: 0.75rem; background: rgba(255,255,255,0.02); border: 1px solid var(--border-card); border-radius: 0.5rem;"">
        <span style=""font-size: 0.8rem; color: var(--text-muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;"">Books:</span>
        <div id=""pv-book-filter"" style=""display: flex; gap: 1rem; flex-wrap: wrap; flex: 1;""></div>
        <button class=""tab-btn"" onclick=""setAllPromptVersionBooks(true)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Select All</button>
        <button class=""tab-btn"" onclick=""setAllPromptVersionBooks(false)"" style=""padding: 0.3rem 0.75rem; font-size: 0.78rem;"">Clear</button>
      </div>
      <table id=""pv-compare-table"">
        <thead>
          <tr>
            <th>Model</th>
            <th>Common Books</th>
            <th>Avg Δ</th>
            <th>Per-Book Δ</th>
          </tr>
        </thead>
        <tbody id=""pv-compare-tbody""></tbody>
      </table>
    </div>
  </div>

  <!-- TAB 6: PROGRESS OVER TIME -->
  <div id=""tab-progress"" class=""tab-content"">
    <div class=""card"" style=""position: relative;"">
      <h3>📈 Best Multi-Book Composite Score Over Time</h3>
      <p style=""color: var(--text-muted); font-size: 0.85rem;"">Every dot is one (model, reasoning effort, temperature, prompt commit) combination's average composite score. The bold line traces the running best — the highest score achieved as of that point in time. Hover any dot for what it's made of.</p>
      <div id=""progress-chart-container"" style=""margin-top: 1rem;""></div>
      <div id=""progress-tooltip"" style=""display: none; position: fixed; z-index: 1000; background: #0b0f19; border: 1px solid var(--accent-cyan); border-radius: 0.5rem; padding: 0.6rem 0.85rem; font-size: 0.8rem; pointer-events: none; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.5); width: min(280px, calc(100vw - 24px)); box-sizing: border-box; overflow-wrap: anywhere;""></div>
    </div>
  </div>

  <script>
    window.BENCHMARK_HISTORY = ");
        sb.AppendLine(historyJson);
        sb.AppendLine(";");
        sb.AppendLine("    window.GLOBAL_LEADERBOARD = ");
        sb.AppendLine(globalJson);
        sb.AppendLine(";");
        sb.AppendLine("    window.JUDGE_LEADERBOARD = ");
        sb.AppendLine(judgeJson);
        sb.AppendLine(";");
        sb.AppendLine("    window.CURRENT_WORKING_TREE_PROMPT_VERSION = ");
        sb.AppendLine(currentWorkingTreeJson);
        sb.AppendLine(";");
        sb.AppendLine("    window.CURRENT_GIT_HEAD_PROMPT_VERSION = ");
        sb.AppendLine(currentGitHeadJson);
        sb.AppendLine(";");

        sb.AppendLine(@"
    let isSyntaxExpanded = false;
    let isPeerExpanded = false;
    let globalSortKey = 'composite';
    let globalSortAsc = false;

    function toggleSyntaxExpand() {
      isSyntaxExpanded = !isSyntaxExpanded;
      const btn = document.getElementById('syntax-toggle-btn');
      if (btn) {
        btn.textContent = isSyntaxExpanded ? '◀ Collapse Syntax Breakdown' : '🔍 Expand Syntax Breakdown';
      }
      renderGlobalTable();
    }

    function togglePeerExpand() {
      isPeerExpanded = !isPeerExpanded;
      const btn = document.getElementById('peer-toggle-btn');
      if (btn) {
        btn.textContent = isPeerExpanded ? '◀ Collapse LLM Peer Breakdown' : '🔍 Expand LLM Peer Breakdown';
      }
      renderGlobalTable();
    }

    function sortGlobal(key) {
      if (globalSortKey === key) {
        globalSortAsc = !globalSortAsc;
      } else {
        globalSortKey = key;
        globalSortAsc = false; // default descending for scores
      }
      renderGlobalTable();
    }

    function switchTab(tabId) {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
      
      const btn = Array.from(document.querySelectorAll('.tab-btn')).find(b => b.getAttribute('onclick').includes(tabId));
      if (btn) btn.classList.add('active');
      document.getElementById('tab-' + tabId).classList.add('active');
    }

    function updateHeaderDates() {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      const headerElem = document.getElementById('last-run-header');
      const globalDateElem = document.getElementById('global-date-subtitle');

      if (runs.length > 0) {
        const liveRuns = runs.filter(r => !r.isMockRun && !r.IsMockRun);
        const latestRun = liveRuns.length > 0 ? liveRuns[liveRuns.length - 1] : runs[runs.length - 1];
        const lastDate = latestRun.timestamp || latestRun.Timestamp || 'Unknown';
        if (headerElem) headerElem.textContent = lastDate;
        if (globalDateElem) globalDateElem.textContent = lastDate;
      } else {
        if (headerElem) headerElem.textContent = 'No runs recorded';
      }
    }

    function getSortVal(m, key) {
      switch(key) {
        case 'modelId': return (m.modelId || m.ModelId || '').toLowerCase();
        case 'effort': return (m.reasoningEffort || m.ReasoningEffort || 'default').toLowerCase();
        case 'temperature': return Number(m.samplingTemperature ?? m.SamplingTemperature ?? 0.2);
        // Prompt revisions are displayed as V0, V1, V2 in first-seen order. Sort by that
        // numeric version order instead of the unrelated lexical order of the Git hashes.
        case 'promptversion': {
          const version = m.promptVersion || m.PromptVersion || 'unknown';
          const index = getTrackedPromptVersions().findIndex(v => v.version === version);
          return index;
        }
        case 'composite': return (m.multiBookCompositeScore !== undefined ? m.multiBookCompositeScore : m.MultiBookCompositeScore) || 0;
        case 'syntax': return (m.avgSyntaxScore !== undefined ? m.avgSyntaxScore : m.AvgSyntaxScore) || 0;
        case 'format': return (m.avgFormatCompliance !== undefined ? m.avgFormatCompliance : m.AvgFormatCompliance) || 0;
        case 'budget': return (m.avgSceneBudget !== undefined ? m.avgSceneBudget : m.AvgSceneBudget) || 0;
        case 'pacing': return (m.avgDialoguePacing !== undefined ? m.avgDialoguePacing : m.AvgDialoguePacing) || 0;
        case 'charsplit': return (m.avgCharDisambiguationSyntax !== undefined ? m.avgCharDisambiguationSyntax : m.AvgCharDisambiguationSyntax) || 0;
        case 'music': return (m.avgMusicSpec !== undefined ? m.avgMusicSpec : m.AvgMusicSpec) || 0;
        case 'qual': return (m.avgQualitativeScore !== undefined ? m.avgQualitativeScore : m.AvgQualitativeScore) || 0;
        case 'jfidelity': return (m.avgFidelity !== undefined ? m.avgFidelity : m.AvgFidelity) || 0;
        case 'jcharsplit': return (m.avgCharSplit !== undefined ? m.avgCharSplit : m.AvgCharSplit) || 0;
        case 'jvideodirect': return (m.avgVideoDirect !== undefined ? m.avgVideoDirect : m.AvgVideoDirect) || 0;
        case 'jpacing': return (m.avgPacing !== undefined ? m.avgPacing : m.AvgPacing) || 0;
        case 'jdialogue': return (m.avgDialogue !== undefined ? m.avgDialogue : m.AvgDialogue) || 0;
        case 'jmusic': return (m.avgMusic !== undefined ? m.avgMusic : m.AvgMusic) || 0;
        case 'wins': return (m.firstPlaceWins !== undefined ? m.firstPlaceWins : m.FirstPlaceWins) || 0;
        case 'books': return (m.totalBooksEvaluated !== undefined ? m.totalBooksEvaluated : m.TotalBooksEvaluated) || 0;
        default: return 0;
      }
    }

    // Client-side port of BenchmarkHistoryStore.IsLiveRun / ComputeGlobalCompositeLeaderboard,
    // so the Global Leaderboard can be recomputed on demand for an arbitrary book subset without
    // a server round-trip. Field access mirrors the server's PascalCase/camelCase dual-fallback
    // convention already used elsewhere in this file (see getSortVal, renderPerBookTable).
    function isLiveRunJs(run) {
      if (run.isMockRun || run.IsMockRun) return false;
      const scores = run.modelScores || run.ModelScores || [];
      if (scores.length === 0) return false;

      const validScores = scores
        .filter(m => !(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback))
        .map(m => (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0)
        .filter(s => s >= 0);
      const distinctCount = new Set(validScores).size;
      if (validScores.length === 0 || (distinctCount <= 1 && validScores.length > 1)) return false;

      const matrix = run.judgeMatrix || run.JudgeMatrix;
      if (!matrix || Object.keys(matrix).length === 0) return false;
      return Object.values(matrix).some(row => Object.values(row).some(v => v > 0));
    }

    function getRunEffortKey(run) {
      const e = run.reasoningEffort || run.ReasoningEffort || '';
      return e ? e : 'default';
    }

    function getRunPromptVersionKey(run) {
      const p = run.promptVersion || run.PromptVersion || '';
      return p ? p : 'unknown';
    }

    function getRunTemperatureKey(run) {
      return Number(run.samplingTemperature ?? run.SamplingTemperature ?? 0.2).toFixed(2);
    }

    function computeGlobalLeaderboardJs(selectedSlugs, selectedEfforts, selectedPromptVersions) {
      const allRuns = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      const liveRuns = allRuns
        .filter(isLiveRunJs)
        .filter(r => selectedSlugs.has(r.bookSlug || r.BookSlug))
        .filter(r => !selectedEfforts || selectedEfforts.has(getRunEffortKey(r)))
        .filter(r => !selectedPromptVersions || selectedPromptVersions.has(getRunPromptVersionKey(r)));
      if (liveRuns.length === 0) return [];

      const avg = arr => arr.reduce((a, b) => a + b, 0) / arr.length;
      const round1 = n => Math.round(n * 10) / 10;

      const modelId = m => m.modelId || m.ModelId || 'Unknown';
      const composite = m => (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0;
      const isFallback = m => !!(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback);
      const syntaxAudit = m => m.syntaxAudit || m.SyntaxAudit || {};
      const sa = (m, key, pkey) => { const s = syntaxAudit(m); return (s[key] !== undefined ? s[key] : s[pkey]) || 0; };
      const num = (m, key, pkey) => (m[key] !== undefined ? m[key] : m[pkey]) || 0;

      // Group by (model, effort, promptVersion) triple, not just model — a model run under two
      // different effort levels or two different prompt texts is two (or more) different
      // conditions and must appear as separate rows so rankings stay honest instead of silently
      // blending ""old prompt"" and ""new prompt"" (or ""default"" and ""max"" effort) into one score.
      const groupKeys = [...new Set(liveRuns.flatMap(r => {
        const effort = getRunEffortKey(r);
        const promptVersion = getRunPromptVersionKey(r);
        return (r.modelScores || r.ModelScores || []).map(m => JSON.stringify([modelId(m), effort, promptVersion, getRunTemperatureKey(r)]));
      }))];
      const result = [];

      groupKeys.forEach(key => {
        const [id, rowEffort, rowPromptVersion, rowTemperature] = JSON.parse(key);

        const modelRuns = liveRuns.filter(r =>
          getRunEffortKey(r) === rowEffort && getRunPromptVersionKey(r) === rowPromptVersion && getRunTemperatureKey(r) === rowTemperature
          && (r.modelScores || r.ModelScores || []).some(m => modelId(m) === id));
        if (modelRuns.length === 0) return;

        const modelScoresList = modelRuns
          .map(r => (r.modelScores || r.ModelScores || []).find(m => modelId(m) === id))
          .filter(m => composite(m) >= 0 && !isFallback(m));
        if (modelScoresList.length === 0) return;

        // Wins only count within the SAME effort level AND prompt version's runs — comparing
        // scores generated under different conditions isn't a fair contest either way.
        const sameConditionRuns = liveRuns.filter(r =>
          getRunEffortKey(r) === rowEffort && getRunPromptVersionKey(r) === rowPromptVersion);
        let wins = 0;
        sameConditionRuns.forEach(run => {
          const validScores = (run.modelScores || run.ModelScores || [])
            .filter(m => composite(m) >= 0 && !isFallback(m))
            .sort((a, b) => composite(b) - composite(a));
          if (validScores.length > 0) {
            const topScore = composite(validScores[0]);
            const topTies = validScores.filter(m => Math.abs(composite(m) - topScore) < 0.01);
            if (topScore > 0 && topTies.length < validScores.length && topTies.some(m => modelId(m) === id)) wins++;
          }
        });

        const titles = [...new Set(modelRuns.map(r => {
          const t = r.bookTitle || r.BookTitle;
          return (t && t.trim().length > 0) ? t : (r.bookSlug || r.BookSlug);
        }))];

        result.push({
          modelId: id,
          reasoningEffort: rowEffort,
          samplingTemperature: rowTemperature,
          promptVersion: rowPromptVersion,
          multiBookCompositeScore: round1(avg(modelScoresList.map(composite))),
          avgSyntaxScore: round1(avg(modelScoresList.map(m => sa(m, 'overallSyntaxScore', 'OverallSyntaxScore')))),
          avgFormatCompliance: round1(avg(modelScoresList.map(m => sa(m, 'formatComplianceScore', 'FormatComplianceScore')))),
          avgSceneBudget: round1(avg(modelScoresList.map(m => sa(m, 'sceneBudgetScore', 'SceneBudgetScore')))),
          avgDialoguePacing: round1(avg(modelScoresList.map(m => sa(m, 'dialoguePacingScore', 'DialoguePacingScore')))),
          avgCharDisambiguationSyntax: round1(avg(modelScoresList.map(m => sa(m, 'characterDisambiguationScore', 'CharacterDisambiguationScore')))),
          avgMusicSpec: round1(avg(modelScoresList.map(m => sa(m, 'musicSpecScore', 'MusicSpecScore')))),
          avgQualitativeScore: round1(avg(modelScoresList.map(m => num(m, 'avgOverallQualitative', 'AvgOverallQualitative') * 10.0))),
          avgFidelity: round1(avg(modelScoresList.map(m => num(m, 'avgAdaptationFidelity', 'AvgAdaptationFidelity')))),
          avgCharSplit: round1(avg(modelScoresList.map(m => num(m, 'avgCharacterDisambiguation', 'AvgCharacterDisambiguation')))),
          avgVideoDirect: round1(avg(modelScoresList.map(m => num(m, 'avgAiVideoDirectibility', 'AvgAiVideoDirectibility')))),
          avgPacing: round1(avg(modelScoresList.map(m => num(m, 'avgDramaticPacing', 'AvgDramaticPacing')))),
          avgDialogue: round1(avg(modelScoresList.map(m => num(m, 'avgDialogueAuthenticity', 'AvgDialogueAuthenticity')))),
          avgMusic: round1(avg(modelScoresList.map(m => num(m, 'avgSoundDesignMusic', 'AvgSoundDesignMusic')))),
          totalBooksEvaluated: new Set(modelRuns.map(r => (r.bookSlug || r.BookSlug || '').toLowerCase())).size,
          evaluatedBookTitles: titles,
          firstPlaceWins: wins,
        });
      });

      result.sort((a, b) => b.multiBookCompositeScore - a.multiBookCompositeScore);
      return result;
    }

    function getAllGlobalBookSlugs() {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      return [...new Set(runs.map(r => r.bookSlug || r.BookSlug))].filter(Boolean);
    }

    function getSelectedGlobalBookSlugs() {
      const checked = Array.from(document.querySelectorAll('#global-book-filter input[type=checkbox]:checked'));
      return new Set(checked.map(c => c.value));
    }

    function initGlobalBookFilter() {
      const container = document.getElementById('global-book-filter');
      if (!container) return;
      container.innerHTML = '';
      getAllGlobalBookSlugs().forEach(slug => {
        const label = document.createElement('label');
        label.style.cssText = 'display: flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; cursor: pointer;';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = slug;
        cb.checked = true;
        cb.onchange = renderGlobalTable;
        label.appendChild(cb);
        label.appendChild(document.createTextNode(formatTitle(slug)));
        container.appendChild(label);
      });
    }

    function setAllGlobalBooks(checked) {
      document.querySelectorAll('#global-book-filter input[type=checkbox]').forEach(cb => { cb.checked = checked; });
      renderGlobalTable();
    }

    function getAllGlobalEfforts() {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      return [...new Set(runs.map(getRunEffortKey))];
    }

    function getSelectedGlobalEfforts() {
      const checked = Array.from(document.querySelectorAll('#global-effort-filter input[type=checkbox]:checked'));
      return new Set(checked.map(c => c.value));
    }

    function initGlobalEffortFilter() {
      const container = document.getElementById('global-effort-filter');
      if (!container) return;
      container.innerHTML = '';
      getAllGlobalEfforts().forEach(effort => {
        const label = document.createElement('label');
        label.style.cssText = 'display: flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; cursor: pointer;';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = effort;
        cb.checked = true;
        cb.onchange = renderGlobalTable;
        label.appendChild(cb);
        label.appendChild(document.createTextNode(effort));
        container.appendChild(label);
      });
    }

    function setAllGlobalEfforts(checked) {
      document.querySelectorAll('#global-effort-filter input[type=checkbox]').forEach(cb => { cb.checked = checked; });
      renderGlobalTable();
    }

    function getAllGlobalPromptVersions() {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      return [...new Set(runs.map(getRunPromptVersionKey))];
    }

    function getSelectedGlobalPromptVersions() {
      const checked = Array.from(document.querySelectorAll('#global-prompt-filter input[type=checkbox]:checked'));
      return new Set(checked.map(c => c.value));
    }

    function initGlobalPromptVersionFilter() {
      const container = document.getElementById('global-prompt-filter');
      if (!container) return;
      container.innerHTML = '';
      getAllGlobalPromptVersions().forEach(pv => {
        const label = document.createElement('label');
        label.style.cssText = 'display: flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; cursor: pointer;';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = pv;
        cb.checked = true;
        cb.onchange = renderGlobalTable;
        label.appendChild(cb);
        label.appendChild(document.createTextNode(promptVersionLabel(pv)));
        container.appendChild(label);
      });
    }

    function setAllGlobalPromptVersions(checked) {
      document.querySelectorAll('#global-prompt-filter input[type=checkbox]').forEach(cb => { cb.checked = checked; });
      renderGlobalTable();
    }

    function renderGlobalTable() {
      const thead = document.querySelector('#global-table thead');
      const tbody = document.getElementById('global-tbody');
      const selectedSlugs = getSelectedGlobalBookSlugs();
      const selectedEfforts = getSelectedGlobalEfforts();
      const selectedPromptVersions = getSelectedGlobalPromptVersions();
      const leaderboard = (selectedSlugs.size > 0 && selectedEfforts.size > 0 && selectedPromptVersions.size > 0
        ? computeGlobalLeaderboardJs(selectedSlugs, selectedEfforts, selectedPromptVersions)
        : []);

      leaderboard.sort((a, b) => {
        const valA = getSortVal(a, globalSortKey);
        const valB = getSortVal(b, globalSortKey);
        if (valA < valB) return globalSortAsc ? -1 : 1;
        if (valA > valB) return globalSortAsc ? 1 : -1;
        return 0;
      });

      const arrow = (key) => globalSortKey === key ? (globalSortAsc ? ' ▲' : ' ▼') : '';
      const colStyle = ""cursor: pointer; user-select: none;"";
      const compositeLabel = (isSyntaxExpanded || isPeerExpanded) ? 'Composite' : 'Multi-Book Composite';

      let headerHtml = `<tr>
          <th style=""${colStyle}"" onclick=""sortGlobal('rank')"">Rank${arrow('rank')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('modelId')"">Model ID${arrow('modelId')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('effort')"" title=""--reasoning-effort this row's runs used; a model at two effort levels appears as two rows so you can compare directly"">Reasoning Effort${arrow('effort')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('temperature')"">Temperature${arrow('temperature')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('promptversion')"" title=""Committed Git revision of the prompt used for these runs; a model under two prompt revisions appears as two rows"">Prompt Commit${arrow('promptversion')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('composite')"">${compositeLabel}${arrow('composite')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('syntax')"">C# Syntax Avg${arrow('syntax')}</th>`;

      if (isSyntaxExpanded) {
        headerHtml += `
          <th style=""${colStyle}; color: var(--accent-cyan);"" onclick=""sortGlobal('format')"">Format %${arrow('format')}</th>
          <th style=""${colStyle}; color: var(--accent-cyan);"" onclick=""sortGlobal('budget')"">Budget %${arrow('budget')}</th>
          <th style=""${colStyle}; color: var(--accent-cyan);"" onclick=""sortGlobal('pacing')"">Pacing %${arrow('pacing')}</th>
          <th style=""${colStyle}; color: var(--accent-cyan);"" onclick=""sortGlobal('charsplit')"">Char Split %${arrow('charsplit')}</th>
          <th style=""${colStyle}; color: var(--accent-cyan);"" onclick=""sortGlobal('music')"">Music Spec %${arrow('music')}</th>`;
      }

      headerHtml += `
          <th style=""${colStyle}"" onclick=""sortGlobal('qual')"">LLM Peer Avg${arrow('qual')}</th>`;

      if (isPeerExpanded) {
        headerHtml += `
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jfidelity')"">Fidelity${arrow('jfidelity')}</th>
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jcharsplit')"">Char Clarity${arrow('jcharsplit')}</th>
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jvideodirect')"">Video Direct${arrow('jvideodirect')}</th>
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jpacing')"">Dramatic Pacing${arrow('jpacing')}</th>
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jdialogue')"">Dialogue${arrow('jdialogue')}</th>
          <th style=""${colStyle}; color: var(--accent-purple);"" onclick=""sortGlobal('jmusic')"">Sound/Music${arrow('jmusic')}</th>`;
      }

      headerHtml += `
          <th style=""${colStyle}"" onclick=""sortGlobal('wins')"">1st Place Wins${arrow('wins')}</th>
          <th style=""${colStyle}"" onclick=""sortGlobal('books')"">Books Evaluated (Live)${arrow('books')}</th>
        </tr>`;
      thead.innerHTML = headerHtml;

      tbody.innerHTML = '';
      if (leaderboard.length === 0) {
        let colSpan = 9;
        if (isSyntaxExpanded) colSpan += 5;
        if (isPeerExpanded) colSpan += 6;
        const msg = selectedSlugs.size === 0
          ? 'No books selected — check at least one book above to compute a leaderboard.'
          : selectedEfforts.size === 0
          ? 'No reasoning-effort levels selected — check at least one above to compute a leaderboard.'
          : selectedPromptVersions.size === 0
          ? 'No prompt versions selected — check at least one above to compute a leaderboard.'
          : 'No live benchmark history runs recorded yet for the selected book(s)/effort level(s)/prompt version(s).';
        tbody.innerHTML = `<tr><td colspan=""${colSpan}"" style=""text-align: center; color: var(--text-muted);"">${msg}</td></tr>`;
        return;
      }

      leaderboard.forEach((m, idx) => {
        const medal = idx === 0 ? '🥇 ' : idx === 1 ? '🥈 ' : idx === 2 ? '🥉 ' : (idx + 1) + '. ';
        const modelId = m.modelId || m.ModelId || 'Unknown';
        const effort = getSortVal(m, 'effort');
        // Keep the original revision for the V# badge; getSortVal returns only its
        // numeric chronological position for sorting.
        const promptVersion = m.promptVersion || m.PromptVersion || 'unknown';
        const composite = getSortVal(m, 'composite');
        const syntax = getSortVal(m, 'syntax');
        const fmt = getSortVal(m, 'format');
        const bdg = getSortVal(m, 'budget');
        const pac = getSortVal(m, 'pacing');
        const chr = getSortVal(m, 'charsplit');
        const mus = getSortVal(m, 'music');
        const qual = getSortVal(m, 'qual');
        const jfid = getSortVal(m, 'jfidelity');
        const jchr = getSortVal(m, 'jcharsplit');
        const jvid = getSortVal(m, 'jvideodirect');
        const jpac = getSortVal(m, 'jpacing');
        const jdlg = getSortVal(m, 'jdialogue');
        const jmus = getSortVal(m, 'jmusic');
        const wins = getSortVal(m, 'wins');
        const books = getSortVal(m, 'books');
        const titles = m.evaluatedBookTitles || m.EvaluatedBookTitles || [];
        const formattedTitles = titles.map(t => formatTitle(t)).join(', ');
        const booksCell = formattedTitles.length > 0
          ? `<strong style=""color: var(--accent-cyan);"">${books}</strong> <span style=""font-size: 0.82rem; color: var(--text-muted); margin-left: 0.3rem;"">(${formattedTitles})</span>`
          : `<strong>${books}</strong>`;

        let row = `<tr>
          <td><strong>${medal}</strong></td>
          <td><strong>${modelId}</strong></td>
          <td><span class=""score-badge ${effort === 'default' ? 'score-high' : 'score-mid'}"">${effort}</span></td>
          <td>${Number(m.samplingTemperature ?? m.SamplingTemperature ?? 0.2).toFixed(2)}</td>
          <td>${promptVersionBadge(promptVersion)}</td>
          <td><span class=""score-badge score-high"">${composite.toFixed(1)}</span></td>
          <td>${syntax.toFixed(1)}%</td>`;

        if (isSyntaxExpanded) {
          row += `
            <td style=""color: var(--accent-cyan);"">${fmt.toFixed(0)}%</td>
            <td style=""color: var(--accent-cyan);"">${bdg.toFixed(0)}%</td>
            <td style=""color: var(--accent-cyan);"">${pac.toFixed(0)}%</td>
            <td style=""color: var(--accent-cyan);"">${chr.toFixed(0)}%</td>
            <td style=""color: var(--accent-cyan);"">${mus.toFixed(0)}%</td>`;
        }

        row += `
          <td>${qual.toFixed(1)}%</td>`;

        if (isPeerExpanded) {
          row += `
            <td style=""color: var(--accent-purple);"">${jfid.toFixed(1)}/10</td>
            <td style=""color: var(--accent-purple);"">${jchr.toFixed(1)}/10</td>
            <td style=""color: var(--accent-purple);"">${jvid.toFixed(1)}/10</td>
            <td style=""color: var(--accent-purple);"">${jpac.toFixed(1)}/10</td>
            <td style=""color: var(--accent-purple);"">${jdlg.toFixed(1)}/10</td>
            <td style=""color: var(--accent-purple);"">${jmus.toFixed(1)}/10</td>`;
        }

        row += `
          <td>🏆 ${wins}</td>
          <td>${booksCell}</td>
        </tr>`;
        tbody.innerHTML += row;
      });
    }

    function formatTitle(slug) {
      if (!slug) return '';
      return slug
        .replace(/_/g, ' ')
        .replace(/-/g, ' - ')
        .replace(/\b\w/g, c => c.toUpperCase());
    }

    function initBookSelects() {
      const select = document.getElementById('book-select');
      const heatmapSelect = document.getElementById('heatmap-book-select');
      select.innerHTML = '';
      heatmapSelect.innerHTML = '';

      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      if (runs.length === 0) return;

      const slugs = [...new Set(runs.map(r => r.bookSlug || r.BookSlug))].filter(Boolean);

      slugs.forEach(slug => {
        const title = formatTitle(slug);

        const opt1 = document.createElement('option');
        opt1.value = slug;
        opt1.textContent = title;
        select.appendChild(opt1);

        const opt2 = document.createElement('option');
        opt2.value = slug;
        opt2.textContent = title;
        heatmapSelect.appendChild(opt2);
      });

      if (slugs.length > 0) {
        select.value = slugs[0];
        heatmapSelect.value = slugs[0];
        renderPerBookTable();
        renderHeatmap();
      }
    }

    function renderPerBookTable() {
      const select = document.getElementById('book-select');
      const slug = select.value;
      const tbody = document.getElementById('perbook-tbody');
      tbody.innerHTML = '';

      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      if (!slug || runs.length === 0) {
        tbody.innerHTML = '<tr><td colspan=""8"" style=""text-align: center; color: var(--text-muted);"">No book history found.</td></tr>';
        return;
      }

      const matchingRuns = runs.filter(r => (r.bookSlug || r.BookSlug) === slug);
      matchingRuns.forEach(r => {
        const date = r.timestamp || r.Timestamp || '—';
        const effort = r.reasoningEffort || r.ReasoningEffort || '';
        const effortLabel = effort ? effort : 'default';
        const temperature = getRunTemperatureKey(r);
        const promptVersion = getRunPromptVersionKey(r);
        const scores = r.modelScores || r.ModelScores || [];
        scores.forEach(m => {
          const modelId = m.modelId || m.ModelId || 'Unknown';
          const composite = (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0;
          const syntaxAudit = m.syntaxAudit || m.SyntaxAudit || {};
          const syntaxScore = (syntaxAudit.overallSyntaxScore !== undefined ? syntaxAudit.overallSyntaxScore : syntaxAudit.OverallSyntaxScore) || 0;
          const avgQual = (m.avgOverallQualitative !== undefined ? m.avgOverallQualitative : m.AvgOverallQualitative) || 0;
          const borda = (m.bordaPoints !== undefined ? m.bordaPoints : m.BordaPoints) || 0;
          const isFallback = !!(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback);

          const badgeClass = (composite < 0 || isFallback) ? 'score-mock' : 'score-mid';
          const compositeLabel = isFallback ? '⚠️ FALLBACK DRAFT' : (composite < 0 ? '⚠️ -1.0 (Failed)' : composite.toFixed(1));
          const modelLabel = isFallback ? `${modelId} <span title=""Live generation failed; this is a non-AI heuristic draft, not this model's real output"">⚠️</span>` : modelId;

          const row = `<tr>
            <td><strong style=""color: var(--accent-cyan);"">${date}</strong></td>
            <td><span class=""score-badge ${effort ? 'score-mid' : 'score-high'}"" title=""--reasoning-effort value this run was invoked with; runs at different effort levels are not directly comparable"">${effortLabel}</span></td>
            <td>${temperature}</td>
            <td>${promptVersionBadge(promptVersion)}</td>
            <td><strong>${modelLabel}</strong></td>
            <td><span class=""score-badge ${badgeClass}"">${compositeLabel}</span></td>
            <td>${syntaxScore.toFixed(1)}%</td>
            <td>${(avgQual * 10).toFixed(1)}%</td>
            <td>${borda} pts</td>
          </tr>`;
          tbody.innerHTML += row;
        });
      });
    }

    function renderHeatmap() {
      const container = document.getElementById('heatmap-container');
      const select = document.getElementById('heatmap-book-select');
      const slug = select ? select.value : null;
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      if (runs.length === 0) {
        container.innerHTML = '<p style=""color: var(--text-muted);"">No run data available for heatmap.</p>';
        return;
      }

      const matching = slug ? runs.filter(r => (r.bookSlug || r.BookSlug) === slug) : runs;
      const targetRun = matching.length > 0 ? matching[matching.length - 1] : runs[runs.length - 1];
      const matrix = targetRun ? (targetRun.judgeMatrix || targetRun.JudgeMatrix) : null;
      const modelScores = targetRun ? (targetRun.modelScores || targetRun.ModelScores || []) : [];

      if (!targetRun || !matrix || Object.keys(matrix).length === 0) {
        container.innerHTML = '<p style=""color: var(--text-muted);"">No heatmap matrix recorded for this book.</p>';
        return;
      }

      let html = '<table><thead><tr><th>Judge \\ Candidate</th>';
      const models = modelScores.map(m => m.modelId || m.ModelId);
      models.forEach(m => html += `<th>${m}</th>`);
      html += '</tr></thead><tbody>';

      Object.entries(matrix).forEach(([judge, ratings]) => {
        html += `<tr><td><strong>${judge}</strong></td>`;
        models.forEach(m => {
          const val = ratings[m] !== undefined ? ratings[m] : ratings[m.toLowerCase()];
          let label = 'N/A';
          if (val !== undefined) {
            label = val < 0 ? '⚠️ -1.0 (Failed)' : val.toFixed(1);
          }
          html += `<td>${label}</td>`;
        });
        html += '</tr>';
      });
      html += '</tbody></table>';
      container.innerHTML = html;
    }

    function renderJudgeLeaderboard() {
      const tbody = document.getElementById('judges-tbody');
      const leaderboard = (window.JUDGE_LEADERBOARD || []);
      tbody.innerHTML = '';

      if (leaderboard.length === 0) {
        tbody.innerHTML = '<tr><td colspan=""7"" style=""text-align: center; color: var(--text-muted);"">No live benchmark history runs recorded yet. Run a benchmark to populate the judge leaderboard.</td></tr>';
        return;
      }

      leaderboard.forEach((j, idx) => {
        const medal = idx === 0 ? '🥇 ' : idx === 1 ? '🥈 ' : idx === 2 ? '🥉 ' : (idx + 1) + '. ';
        const modelId = j.modelId || j.ModelId || 'Unknown';
        const reliability = (j.reliabilityRate !== undefined ? j.reliabilityRate : j.ReliabilityRate) || 0;
        const netBias = (j.avgNetSelfBias !== undefined ? j.avgNetSelfBias : j.AvgNetSelfBias) || 0;
        const absBias = (j.avgAbsSelfBias !== undefined ? j.avgAbsSelfBias : j.AvgAbsSelfBias) || 0;
        const booksJudged = (j.booksJudged !== undefined ? j.booksJudged : j.BooksJudged) || 0;
        const biasSamples = (j.selfBiasSampleCount !== undefined ? j.selfBiasSampleCount : j.SelfBiasSampleCount) || 0;

        const relClass = reliability >= 0.9 ? 'score-high' : reliability >= 0.6 ? 'score-mid' : 'score-mock';
        const biasSign = netBias > 0 ? '+' : '';

        const row = `<tr>
          <td><strong>${medal}</strong></td>
          <td><strong>${modelId}</strong></td>
          <td><span class=""score-badge ${relClass}"">${(reliability * 100).toFixed(0)}%</span></td>
          <td>${biasSign}${netBias.toFixed(2)}</td>
          <td>${absBias.toFixed(2)}</td>
          <td>${booksJudged}</td>
          <td>${biasSamples}</td>
        </tr>`;
        tbody.innerHTML += row;
      });
    }

    // Distinct real (non-'unknown') prompt versions seen in history, sorted oldest-first by the
    // earliest run that used each one.
    function getTrackedPromptVersions() {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      const seen = {};
      runs.forEach(r => {
        const pv = r.promptVersion || r.PromptVersion || '';
        if (!pv) return;
        const ts = r.timestamp || r.Timestamp || '';
        if (!seen[pv] || ts < seen[pv].firstSeen) {
          seen[pv] = seen[pv] || { version: pv, firstSeen: ts, books: new Set(), models: new Set() };
          seen[pv].firstSeen = seen[pv].firstSeen < ts ? seen[pv].firstSeen : ts;
        }
        seen[pv].books.add(r.bookSlug || r.BookSlug);
        (r.modelScores || r.ModelScores || []).forEach(m => {
          const isFallback = !!(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback);
          const composite = (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0;
          if (!isFallback && composite >= 0) seen[pv].models.add(m.modelId || m.ModelId);
        });
      });
      return Object.values(seen).sort((a, b) => a.firstSeen < b.firstSeen ? -1 : 1);
    }

    function promptVersionLabel(version) {
      if (!version || version === 'unknown') return 'unknown';
      const index = getTrackedPromptVersions().findIndex(v => v.version === version);
      return index < 0 ? version.slice(0, 7) : 'V' + index;
    }

    function promptVersionBadge(version) {
      const label = promptVersionLabel(version);
      if (label === 'unknown') return '<span class=""score-badge score-mid"">unknown</span>';
      const url = 'https://github.com/budcribar/PageToMovie/commit/' + encodeURIComponent(version);
      return '<a class=""score-badge score-mid"" href=""' + url + '"" target=""_blank"" rel=""noopener"" title=""Git commit: ' + version + ' — open on GitHub"">' + label + '</a>';
    }

    function renderPromptVersionsList() {
      const tbody = document.getElementById('promptversions-tbody');
      if (!tbody) return;
      const versions = getTrackedPromptVersions();
      if (versions.length === 0) {
        tbody.innerHTML = '<tr><td colspan=""4"" style=""text-align: center; color: var(--text-muted);"">No tracked prompt versions yet — this dashboard predates prompt-version tracking, or every run so far is untagged (\'unknown\').</td></tr>';
        return;
      }
      const curWorking = window.CURRENT_WORKING_TREE_PROMPT_VERSION;
      const curHead = window.CURRENT_GIT_HEAD_PROMPT_VERSION;
      tbody.innerHTML = versions.map(v => {
        const badges = [];
        if (curHead && v.version === curHead) badges.push('<span class=""score-badge score-high"" title=""Matches the prompt file as of the last git commit"">✅ committed (HEAD)</span>');
        if (curWorking && v.version === curWorking && v.version !== curHead) badges.push('<span class=""score-badge score-mid"" title=""Matches the prompt file on disk right now, but has not been committed"">📝 uncommitted</span>');
        if (badges.length === 0) badges.push('<span class=""score-badge score-mock"" title=""Neither the current working tree nor the last commit match this version"">🗄️ archived</span>');
        return `<tr>
          <td>${promptVersionBadge(v.version)}</td>
          <td>${v.firstSeen}</td>
          <td>${badges.join(' ')}</td>
          <td>${v.books.size} books × ${v.models.size} models</td>
        </tr>`;
      }).join('');
    }

    function initPromptComparisonPickers() {
      const versions = getTrackedPromptVersions();
      const fromSel = document.getElementById('pv-compare-from');
      const toSel = document.getElementById('pv-compare-to');
      if (!fromSel || !toSel) return;
      const optionsHtml = versions.map(v => `<option value=""${v.version}"">${promptVersionLabel(v.version)} (${v.firstSeen})</option>`).join('');
      fromSel.innerHTML = optionsHtml;
      toSel.innerHTML = optionsHtml;
      if (versions.length >= 2) {
        fromSel.value = versions[versions.length - 2].version;
        toSel.value = versions[versions.length - 1].version;
      } else if (versions.length === 1) {
        fromSel.value = toSel.value = versions[0].version;
      }
    }

    function getAllPromptVersionBookSlugs() {
      return getAllGlobalBookSlugs(); // same book universe as the Global Leaderboard tab
    }

    function getSelectedPromptVersionBookSlugs() {
      const checked = Array.from(document.querySelectorAll('#pv-book-filter input[type=checkbox]:checked'));
      return new Set(checked.map(c => c.value));
    }

    function initPromptVersionBookFilter() {
      const container = document.getElementById('pv-book-filter');
      if (!container) return;
      container.innerHTML = '';
      getAllPromptVersionBookSlugs().forEach(slug => {
        const label = document.createElement('label');
        label.style.cssText = 'display: flex; align-items: center; gap: 0.4rem; font-size: 0.85rem; cursor: pointer;';
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = slug;
        cb.checked = true;
        cb.onchange = renderPromptComparison;
        label.appendChild(cb);
        label.appendChild(document.createTextNode(formatTitle(slug)));
        container.appendChild(label);
      });
    }

    function setAllPromptVersionBooks(checked) {
      document.querySelectorAll('#pv-book-filter input[type=checkbox]').forEach(cb => { cb.checked = checked; });
      renderPromptComparison();
    }

    // Per-model score delta between two prompt versions, counting only (book) pairs where BOTH
    // versions have real (non-fallback, non-negative) data AND the book is in selectedBooks — a
    // gap on one side (e.g. a billing failure) must never silently skew the average toward the
    // side with more data.
    function computePromptComparison(fromVersion, toVersion, selectedBooks) {
      const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
      const modelId = m => m.modelId || m.ModelId || 'Unknown';
      const composite = m => (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0;
      const isFallback = m => !!(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback);
      const pvOf = r => r.promptVersion || r.PromptVersion || '';

      function scoresFor(version) {
        const out = {}; // modelId -> { bookSlug -> score }
        runs.filter(r => pvOf(r) === version).forEach(r => {
          const book = r.bookSlug || r.BookSlug;
          if (selectedBooks && !selectedBooks.has(book)) return;
          (r.modelScores || r.ModelScores || []).forEach(m => {
            if (isFallback(m) || composite(m) < 0) return;
            const id = modelId(m);
            out[id] = out[id] || {};
            out[id][book] = composite(m);
          });
        });
        return out;
      }

      const fromScores = scoresFor(fromVersion);
      const toScores = scoresFor(toVersion);
      const allModels = [...new Set([...Object.keys(fromScores), ...Object.keys(toScores)])];

      return allModels.map(id => {
        const fb = fromScores[id] || {};
        const tb = toScores[id] || {};
        const commonBooks = Object.keys(fb).filter(b => b in tb);
        const deltas = commonBooks.map(b => ({ book: b, delta: Math.round((tb[b] - fb[b]) * 10) / 10 }));
        const avgDelta = deltas.length > 0 ? deltas.reduce((a, d) => a + d.delta, 0) / deltas.length : null;
        return { modelId: id, commonBooks: commonBooks.length, avgDelta, deltas };
      }).filter(r => r.commonBooks > 0).sort((a, b) => (b.avgDelta || -999) - (a.avgDelta || -999));
    }

    function renderPromptComparison() {
      const tbody = document.getElementById('pv-compare-tbody');
      const fromSel = document.getElementById('pv-compare-from');
      const toSel = document.getElementById('pv-compare-to');
      if (!tbody || !fromSel || !toSel || !fromSel.value || !toSel.value) return;

      const selectedBooks = getSelectedPromptVersionBookSlugs();
      if (selectedBooks.size === 0) {
        tbody.innerHTML = '<tr><td colspan=""4"" style=""text-align: center; color: var(--text-muted);"">No books selected — check at least one book above.</td></tr>';
        return;
      }

      const rows = computePromptComparison(fromSel.value, toSel.value, selectedBooks);
      if (rows.length === 0) {
        tbody.innerHTML = '<tr><td colspan=""4"" style=""text-align: center; color: var(--text-muted);"">No models have comparable (non-fallback) data under both selected versions for the selected book(s).</td></tr>';
        return;
      }
      tbody.innerHTML = rows.map(r => {
        const cls = r.avgDelta > 0 ? 'score-high' : r.avgDelta < 0 ? 'score-mock' : 'score-mid';
        const sign = r.avgDelta > 0 ? '+' : '';
        return `<tr>
          <td><strong>${r.modelId}</strong></td>
          <td>${r.commonBooks}</td>
          <td><span class=""score-badge ${cls}"">${sign}${r.avgDelta.toFixed(2)}</span></td>
          <td style=""font-size: 0.82rem; color: var(--text-muted);"">${r.deltas.map(d => `${formatTitle(d.book)}: ${d.delta > 0 ? '+' : ''}${d.delta.toFixed(1)}`).join(', ')}</td>
        </tr>`;
      }).join('');
    }

    function parseHistTimestamp(ts) {
  if (!ts) return null;
  const iso = ts.replace(' UTC', 'Z').replace(' ', 'T');
  const d = new Date(iso);
  return isNaN(d.getTime()) ? null : d;
}

function computeProgressPoints() {
  const runs = (window.BENCHMARK_HISTORY && window.BENCHMARK_HISTORY.runs) || [];
  const modelId = m => m.modelId || m.ModelId || 'Unknown';
  const composite = m => (m.compositeScore !== undefined ? m.compositeScore : m.CompositeScore) || 0;
  const isFallback = m => !!(m.isGenerationFallback !== undefined ? m.isGenerationFallback : m.IsGenerationFallback);

  const groups = new Map();
  runs.filter(isLiveRunJs).forEach(r => {
    const effort = getRunEffortKey(r);
    const pv = getRunPromptVersionKey(r);
    const d = parseHistTimestamp(r.timestamp || r.Timestamp || '');
    if (!d) return;
    (r.modelScores || r.ModelScores || []).forEach(m => {
      if (isFallback(m) || composite(m) < 0) return;
      const temperature = getRunTemperatureKey(r);
      const key = JSON.stringify([modelId(m), effort, temperature, pv]);
      if (!groups.has(key)) groups.set(key, { modelId: modelId(m), effort, temperature, promptVersion: pv, scores: [], lastSeen: d });
      const g = groups.get(key);
      g.scores.push(composite(m));
      if (d > g.lastSeen) g.lastSeen = d;
    });
  });

  const points = Array.from(groups.values()).map(g => ({
    modelId: g.modelId,
    effort: g.effort,
    temperature: g.temperature,
    promptVersion: g.promptVersion,
    avgComposite: g.scores.reduce((a, b) => a + b, 0) / g.scores.length,
    bookCount: g.scores.length,
    date: g.lastSeen,
  }));
  points.sort((a, b) => a.date - b.date);

  let runningBest = -Infinity;
  points.forEach(p => {
    p.isRecord = p.avgComposite > runningBest + 1e-9;
    if (p.isRecord) runningBest = p.avgComposite;
  });

  return points;
}

function renderProgressChart() {
  const container = document.getElementById('progress-chart-container');
  if (!container) return;
  const points = computeProgressPoints();
  if (points.length === 0) {
    container.innerHTML = '<p style=""color: var(--text-muted);"">No live benchmark history yet.</p>';
    return;
  }

  const width = 900, height = 340;
  const padLeft = 50, padRight = 20, padTop = 20, padBottom = 50;
  const plotW = width - padLeft - padRight;
  const plotH = height - padTop - padBottom;

  const minDate = points[0].date.getTime();
  const maxDate = points[points.length - 1].date.getTime();
  const dateRange = Math.max(1, maxDate - minDate);

  const scores = points.map(p => p.avgComposite);
  const minScore = Math.max(0, Math.floor(Math.min(...scores) - 3));
  const maxScore = Math.min(100, Math.ceil(Math.max(...scores) + 3));
  const scoreRange = Math.max(1, maxScore - minScore);

  const xOf = d => padLeft + ((d.getTime() - minDate) / dateRange) * plotW;
  const yOf = s => padTop + plotH - ((s - minScore) / scoreRange) * plotH;

  let gridSvg = '';
  const tickCount = 5;
  for (let i = 0; i <= tickCount; i++) {
    const val = minScore + (scoreRange * i / tickCount);
    const y = yOf(val);
    gridSvg += '<line x1=""' + padLeft + '"" y1=""' + y + '"" x2=""' + (width - padRight) + '"" y2=""' + y + '"" stroke=""rgba(255,255,255,0.06)"" stroke-width=""1"" />';
    gridSvg += '<text x=""' + (padLeft - 8) + '"" y=""' + (y + 4) + '"" text-anchor=""end"" font-size=""10"" fill=""#9ca3af"">' + val.toFixed(0) + '</text>';
  }

  const fmtDate = d => d.toISOString().slice(0, 10);
  let xLabelSvg = '';
  [points[0], points[points.length - 1]].forEach((p, i) => {
    const x = xOf(p.date);
    xLabelSvg += '<text x=""' + x + '"" y=""' + (height - padBottom + 20) + '"" text-anchor=""' + (i === 0 ? 'start' : 'end') + '"" font-size=""10"" fill=""#9ca3af"">' + fmtDate(p.date) + '</text>';
  });

  const recordPoints = points.filter(p => p.isRecord);
  const nonRecordPoints = points.filter(p => !p.isRecord);
  const lineD = recordPoints.map((p, i) => (i === 0 ? 'M' : 'L') + ' ' + xOf(p.date).toFixed(1) + ' ' + yOf(p.avgComposite).toFixed(1)).join(' ');

  let dotsSvg = '';
  nonRecordPoints.forEach((p, i) => {
    dotsSvg += '<circle class=""progress-dot"" data-idx=""nr' + i + '"" cx=""' + xOf(p.date).toFixed(1) + '"" cy=""' + yOf(p.avgComposite).toFixed(1) + '"" r=""3.5"" fill=""#a855f7"" fill-opacity=""0.55"" style=""cursor:pointer;"" />';
  });
  recordPoints.forEach((p, i) => {
    dotsSvg += '<circle class=""progress-dot"" data-idx=""r' + i + '"" cx=""' + xOf(p.date).toFixed(1) + '"" cy=""' + yOf(p.avgComposite).toFixed(1) + '"" r=""5.5"" fill=""#fbbf24"" stroke=""#0b0f19"" stroke-width=""1.5"" style=""cursor:pointer;"" />';
  });

  container.innerHTML = '<svg viewBox=""0 0 ' + width + ' ' + height + '"" style=""width: 100%; height: auto;"">'
    + '<line x1=""' + padLeft + '"" y1=""' + padTop + '"" x2=""' + padLeft + '"" y2=""' + (height - padBottom) + '"" stroke=""rgba(255,255,255,0.15)"" stroke-width=""1"" />'
    + '<line x1=""' + padLeft + '"" y1=""' + (height - padBottom) + '"" x2=""' + (width - padRight) + '"" y2=""' + (height - padBottom) + '"" stroke=""rgba(255,255,255,0.15)"" stroke-width=""1"" />'
    + gridSvg + xLabelSvg
    + '<path d=""' + lineD + '"" fill=""none"" stroke=""#38bdf8"" stroke-width=""2"" />'
    + dotsSvg
    + '</svg>';

  const circles = container.querySelectorAll('circle.progress-dot');
  circles.forEach(circle => {
    const idx = circle.getAttribute('data-idx');
    const p = idx.startsWith('nr') ? nonRecordPoints[parseInt(idx.slice(2), 10)] : recordPoints[parseInt(idx.slice(1), 10)];
    circle.addEventListener('mouseenter', (e) => showProgressTooltip(e, p));
    circle.addEventListener('mousemove', (e) => positionProgressTooltip(e));
    circle.addEventListener('mouseleave', hideProgressTooltip);
  });
}

function showProgressTooltip(e, p) {
  const tooltip = document.getElementById('progress-tooltip');
  if (!tooltip) return;
  const promptLabel = promptVersionLabel(p.promptVersion);
  tooltip.innerHTML = '<div style=""font-weight:700; color:' + (p.isRecord ? '#fbbf24' : '#a855f7') + '; margin-bottom:0.3rem;"">' + (p.isRecord ? '🏆 Record at this point' : 'Not a record') + '</div>'
    + '<div><strong>' + p.modelId + '</strong></div>'
    + '<div style=""color:#9ca3af;"">Effort: <strong style=""color:#f3f4f6;"">' + p.effort + '</strong></div>'
    + '<div style=""color:#9ca3af;"">Temperature: <strong style=""color:#f3f4f6;"">' + p.temperature + '</strong></div>'
    + '<div style=""color:#9ca3af;"">Prompt: <strong style=""color:#f3f4f6;"">' + promptLabel + '</strong></div>'
    + '<div style=""color:#9ca3af; font-size:0.72rem;"">Git commit: <strong style=""color:#f3f4f6;"">' + p.promptVersion + '</strong></div>'
    + '<div style=""color:#9ca3af;"">Composite: <strong style=""color:#4ade80;"">' + p.avgComposite.toFixed(1) + '</strong> (' + p.bookCount + ' books)</div>'
    + '<div style=""color:#9ca3af; font-size:0.72rem; margin-top:0.2rem;"">' + p.date.toISOString().replace('T', ' ').slice(0, 19) + ' UTC</div>';
  tooltip.style.display = 'block';
  positionProgressTooltip(e);
}

function positionProgressTooltip(e) {
  const tooltip = document.getElementById('progress-tooltip');
  if (!tooltip || tooltip.style.display === 'none') return;
  const margin = 12;
  const gap = 16;
  const rect = tooltip.getBoundingClientRect();
  // At the right edge, put the tooltip on the left of the point. Clamping alone
  // can still obscure the point or leave the tooltip outside a narrow viewport.
  const preferredLeft = e.clientX + gap + rect.width <= window.innerWidth - margin
    ? e.clientX + gap
    : e.clientX - gap - rect.width;
  const left = Math.max(margin, Math.min(preferredLeft, window.innerWidth - rect.width - margin));
  // Keep the details panel in the chart's upper area. SVG pointer coordinates can
  // be offset in a docked-devtools/local-file view, which previously pinned this
  // tooltip to the bottom of the window for the final point.
  const chart = document.getElementById('progress-chart-container');
  const chartTop = chart ? chart.getBoundingClientRect().top : margin;
  const preferredTop = chartTop + margin;
  const top = Math.max(margin, Math.min(preferredTop, window.innerHeight - rect.height - margin));
  tooltip.style.left = left + 'px';
  tooltip.style.top = top + 'px';
}

function hideProgressTooltip() {
  const tooltip = document.getElementById('progress-tooltip');
  if (tooltip) tooltip.style.display = 'none';
}

    window.addEventListener('DOMContentLoaded', () => {
      updateHeaderDates();
      initGlobalBookFilter();
      initGlobalEffortFilter();
      initGlobalPromptVersionFilter();
      renderGlobalTable();
      initBookSelects();
      renderJudgeLeaderboard();
      renderPromptVersionsList();
      initPromptVersionBookFilter();
      initPromptComparisonPickers();
      renderPromptComparison();
      renderProgressChart();
    });
  </script>
</body>
</html>");

        return sb.ToString();
    }
}
