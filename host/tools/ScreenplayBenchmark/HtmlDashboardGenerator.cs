using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ScreenplayBenchmark;

public static class HtmlDashboardGenerator
{
    public static string GenerateHtmlDashboard(HistoricalStoreContainer historyContainer)
    {
        var globalLeaderboard = BenchmarkHistoryStore.ComputeGlobalCompositeLeaderboard(historyContainer);
        var judgeLeaderboard = BenchmarkHistoryStore.ComputeJudgeLeaderboard(historyContainer);
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
        var historyJson = JsonSerializer.Serialize(historyContainer, jsonOpts);
        var globalJson = JsonSerializer.Serialize(globalLeaderboard, jsonOpts);
        var judgeJson = JsonSerializer.Serialize(judgeLeaderboard, jsonOpts);

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

    function renderGlobalTable() {
      const thead = document.querySelector('#global-table thead');
      const tbody = document.getElementById('global-tbody');
      const leaderboard = (window.GLOBAL_LEADERBOARD || []).slice();

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
        let colSpan = 7;
        if (isSyntaxExpanded) colSpan += 5;
        if (isPeerExpanded) colSpan += 6;
        tbody.innerHTML = `<tr><td colspan=""${colSpan}"" style=""text-align: center; color: var(--text-muted);"">No live benchmark history runs recorded yet. Run a benchmark to populate the leaderboard.</td></tr>`;
        return;
      }

      leaderboard.forEach((m, idx) => {
        const medal = idx === 0 ? '🥇 ' : idx === 1 ? '🥈 ' : idx === 2 ? '🥉 ' : (idx + 1) + '. ';
        const modelId = m.modelId || m.ModelId || 'Unknown';
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
        tbody.innerHTML = '<tr><td colspan=""6"" style=""text-align: center; color: var(--text-muted);"">No book history found.</td></tr>';
        return;
      }

      const matchingRuns = runs.filter(r => (r.bookSlug || r.BookSlug) === slug);
      matchingRuns.forEach(r => {
        const date = r.timestamp || r.Timestamp || '—';
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

    window.addEventListener('DOMContentLoaded', () => {
      updateHeaderDates();
      renderGlobalTable();
      initBookSelects();
      renderJudgeLeaderboard();
    });
  </script>
</body>
</html>");

        return sb.ToString();
    }
}
