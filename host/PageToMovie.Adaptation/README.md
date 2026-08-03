# PageToMovie.Adaptation

**Pure Stage‑1 library** — book text + knobs in; Fountain / vision meta / density / reports out.

## Boundary (do / don’t)

| Do | Don’t |
|----|--------|
| Accept book text, target minutes, model id, temperature | Touch `ProjectStore`, `projects/`, SQLite, git |
| Inject `PageToMovie.Core.Abstractions.IChatClient` | Construct HTTP / provider clients |
| Emit Fountain + `AdaptationVisionMeta` DTOs | Write vision_meta.json or extract_meta.json |
| Pure density / analyzer math | OCR, PDF prepare, YouTube, Stage2, clips |
| Version whole module as `adaptation_sha` | Depend on Engine, Api, Web, Fakes |

## Project refs

- **Allowed:** `PageToMovie.Core` only
- **Forbidden:** Engine, Api, Web, Fakes

## Public façade

`AdaptationService`:

| Method | Status |
|--------|--------|
| `AnalyzeBook` | Wired → `BookTextAnalyzer` |
| `EstimateNaturalRuntime` | Wired → `AdaptationDensity` |
| `ResolveTargetMinutes` | Pure clamp over density |
| `ConvertAsync` | Phase 2 (throws until BookToFountainConverter moves) |

## Chat interface

`IChatClient` + `ChatCallModes` live in **`PageToMovie.Core.Abstractions`**
(`host/PageToMovie.Core/Abstractions/IChatClient.cs`) so Adaptation never references Engine.

## Contracts

See `Contracts/`:

- `AdaptationRequest` / `AdaptationResult`
- `NaturalRuntimeEstimate` / `BookAnalysisResult`
- `AdaptationVisionMeta` (+ `AdaptationVisionMetaStatus`)

Engine maps `AdaptationVisionMeta` ↔ `ProjectVisionMeta.Document` at the orchestration boundary.

Related analysis types (same assembly, not under Contracts/):

- `BookTextAnalyzer` / `BookTextAnalysis`
- `AdaptationDensity` / `AdaptationDensity.Estimate`
- `TextMetrics`

## Plan

See `host/docs/adaptation-module-implementation-plan.md`.


## Phase 1 contents

- `Analysis/AdaptationDensity.cs` — natural film minutes (δ, τ)
- `Analysis/BookTextAnalyzer.cs` — quality, book kind, Stage‑1 runtime resolve
- `Analysis/TextMetrics.cs` — pure word/syllable counting (shared with Engine clip estimator wrappers)
- `ClipDurationEstimator` remains in Engine (Stage 2 / video model bounds)
