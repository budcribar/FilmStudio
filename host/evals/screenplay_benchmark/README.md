# Screenplay Generation & Blind Peer-Evaluation Benchmark Guide

Step-by-step operator guide for evaluating AI models on screenplay adaptation, scene budgeting, character age-disambiguation, and background music scoring.

---

## 🎯 Overview & 8 Evaluation Dimensions

The benchmark tool (`ScreenplayBenchmark`) evaluates candidate Chat models across **8 comprehensive dimensions**:

1. **Fountain Syntax & Format Compliance** *(Deterministic C# Parser — 0–100%)*
2. **Scene Budget & Granularity Efficiency** *(Deterministic C# Parser — Penalizes excessive micro-scenes or unbroken monologue walls)*
3. **Dialogue Word-Count Bounds & Pacing** *(Deterministic C# Parser — Ensures speech beats fit 5–10s video clip limits)*
4. **Character Description & Age Disambiguation** *(C# Audit + LLM Judges — Audits age-split naming like `YOUNG NICK` vs `ADULT NICK`)*
5. **Sound Design & Background Music Scoring** *(C# Audit + LLM Judges — Validates instrumental tags, tempo, and scene audio beds)*
6. **AI Video Directibility ("Show, Don't Tell")** *(LLM Judges — Evaluates camera-observable actions vs unfilmable prose)*
7. **Adaptation Fidelity & Source Completeness** *(LLM Judges — Verifies plot/character translation from book text)*
8. **Dramatic Pacing & Rhythm** *(LLM Judges — Evaluates dramatic tension, structure, and scene flow)*

---

## 📋 Step-by-Step Operator Instructions

### Step 1: Populate or Update `models_catalog.json`

When adding a new model or updating an existing model version:

1. Open `host/PageToMovie.Core/config/models_catalog.json`.
2. Add or update the model entry under the `"models"` array:
   ```json
   {
     "id": "grok-5",
     "displayName": "xAI Grok 5",
     "capability": "Chat",
     "provider": "Xai",
     "apiBase": "https://api.xai.com/v1",
     "endpointPath": "chat/completions",
     "requiredEnvKeys": ["XAI_API_KEY"],
     "enabled": true,
     "maxInputTokens": 131072
   }
   ```
3. **Retiring Older Model Versions:** To stop paying API costs for older model versions to judge new screenplays, set `"enabled": false` on older entries (e.g. `"enabled": false` for `grok-4`). Disabled models will **never** make paid API calls as judges or generators, but their historical scores stay intact on the leaderboard!

---

### Step 2: Set Provider API Keys

Ensure environment API keys are exported in your terminal for the provider models being tested:

```bash
# xAI / Grok
export XAI_API_KEY="your-xai-key"

# Anthropic / Claude
export ANTHROPIC_API_KEY="your-anthropic-key"

# Google Gemini
export GEMINI_API_KEY="your-gemini-key"

# OpenAI / GPT
export OPENAI_API_KEY="your-openai-key"
```

---

### Target runtime (production parity)

By default the benchmark uses **`BookTextAnalyzer.ResolveStage1RuntimeMinutes`** — the same
algorithm as production Stage 1 (`SuggestedTotalMinutes`, clamp 3–180). It no longer hard-codes
10 minutes. Override only when needed:

```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --book books/MaryHadALittleLamb.txt --target-runtime-minutes 5
```

### Step 3: Run the Benchmark CLI

Run `ScreenplayBenchmark` using one of the commands below depending on your workflow:

#### Option A: Evaluate ONLY a New Model (Incremental Mode — Recommended for New Models)
To evaluate a newly added model without re-generating screenplays for existing models:
```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --models grok-5
```
*✨ Reuses existing cached `.fountain` screenplays for active peers and makes API calls ONLY for the new model.*

#### Option B: Run Default 5-Book Benchmark Suite
To run the full benchmark across the curated 5-book suite (`Nick_and_Me`, `The_Tell-Tale_Heart`, `The_Velveteen_Rabbit`, `A_Christmas_Carol`, `The_Call_of_the_Wild`):
```bash
dotnet run --project host/tools/ScreenplayBenchmark
```

#### Option C: Run Benchmark for a Single Book
```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --book books/A_Christmas_Carol.txt
```

#### Option D: Run Dry-Run (Simulate Harness without Paid API Calls)
```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --dry-run
```

#### Option E: View Historical Console Leaderboard Only
```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --leaderboard
```

#### Option F: Run the Zero-Cost Harness Self-Test

This is the canonical final-verification command. It reads recorded in-process fixtures only,
makes no provider requests, and exits nonzero when any check fails:

```bash
dotnet run --project host/tools/ScreenplayBenchmark -- --self-test
```

Do not use `--dry-run` as a substitute for `--self-test`: dry-run exercises a different CLI path,
while self-test asserts the structured extraction, cast/location recovery, and complete
Fountain + `VISION_META` judge package invariants.

---

### Step 4: View & Inspect Benchmark Dashboard

After every run, persistent history and dashboard visualizers are updated automatically:

1. **Interactive HTML Dashboard:**
   - Open `evals/benchmark_dashboard.html` in any web browser.
   - Features dynamic tabs for **Global Multi-Book Leaderboard**, **Per-Book Score History**, **Peer Judge Heatmap Matrix**, and **Diagnostic Audit Warnings**.
2. **Markdown Reports:**
   - Inspect individual run reports at `evals/results/screenplay_benchmark_<timestamp>/<book_slug>/benchmark_report.md`.
3. **Persistent History Storage:**
   - Saved in `evals/benchmark_history.json`.

---

## 💡 Pro-Tips for Operators

- **Screenplay Disk Caching:** Screenplay drafts generated during runs are stored under `evals/cache/<book_slug>/<model_id>.fountain`. If you rerun a benchmark, cached screenplays are reused automatically to prevent wasted spend.
- **Fair Blind Cross-Evaluation:** Candidate screenplays are anonymized as `Screenplay A`, `Screenplay B`, etc., with random label assignment per judge to prevent self-preference bias.
