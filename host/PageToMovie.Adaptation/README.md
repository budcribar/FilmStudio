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
| `BuildSystemPromptAsync` | Wired → embedded `book_to_fountain.txt` |
| `ConvertHeuristic` | Wired → offline stub |
| `ConvertAsync` | Wired → `Conversion.BookToFountainConverter` |

## Chat interface

`IChatClient` + `ChatCallModes` live in **`PageToMovie.Core.Abstractions`**
(`host/PageToMovie.Core/Abstractions/IChatClient.cs`) so Adaptation never references Engine.

## Contracts

See `Contracts/`:

- `AdaptationRequest` / `AdaptationResult`
- `NaturalRuntimeEstimate` / `BookAnalysisResult`
- `AdaptationVisionMeta` (+ `AdaptationVisionMetaStatus`)

Engine maps `AdaptationVisionMeta` ↔ `ProjectVisionMeta.Document` at the orchestration boundary
(`Engine.BookToFountainConverter` thin façade + `ScreenplayService.CreateDraftFromBookAsync`).

## Conversion (Phase 2)

- `Conversion/BookToFountainConverter.cs` — full Stage‑1 convert (moved from Engine)
- `Conversion/Stage1ChatExecutor.cs` — primary + correction + fallback without ModelExecution
- `Conversion/AdaptationPromptPack.cs` — embedded prompt load
- `Conversion/AdaptationVisionMetaParser.cs` — pure VISION_META JSON parse

**Remaining Engine-side orchestration (not in this module):** ProjectStore save, book registry cache,
`GenerationErrorLogger` (mapped via callback), Stage2, `FountainParser` (Engine still uses it elsewhere).

## Plan

See `host/docs/adaptation-module-implementation-plan.md`.

## Phase contents

- Phase 0–1: contracts, density, analyzer, façade stubs
- Phase 2: converter + prompts + `ConvertAsync` + ScreenplayService wiring
