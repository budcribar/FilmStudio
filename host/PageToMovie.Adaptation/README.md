# PageToMovie.Adaptation

**Pure Stage‑1 library** — book text + knobs in; Fountain / vision meta / density / reports out.

## Boundary (do / don’t)

| Do | Don’t |
|----|--------|
| Accept book text, target minutes, model id, temperature | Touch `ProjectStore`, `projects/`, SQLite, git |
| Inject `PageToMovie.Core.Abstractions.IChatClient` | Construct HTTP / provider clients |
| Emit Fountain + `AdaptationVisionMeta` DTOs | Write vision_meta.json or extract_meta.json |
| Pure density / analyzer / natural-runtime math | OCR, PDF prepare, YouTube, Stage2, clips |
| Version whole module as `adaptation_sha` | Depend on Engine, Api, Web, Fakes |

## Project refs

- **Allowed:** `PageToMovie.Core` only
- **Forbidden:** Engine, Api, Web, Fakes

## Runtime boundary (Phase 3)

| Concern | Owner |
|---------|--------|
| Natural film minutes (density) | **Adaptation** — `NaturalRuntime` / `AdaptationService.EstimateNaturalRuntime` |
| Clamp + mode (`natural` / `reduced` / `custom`) | **Adaptation** — `NaturalRuntime.ClampMinutes` / `ResolveMode` |
| Persist target / mode on project | **Engine** — `FilmRuntime` reads/writes `pipeline_config` + `extract_meta` |
| Retarget UI (`FilmLengthCard`) | Web → API → Engine `FilmRuntime` (natural still from Adaptation) |

> **Retarget is Engine; natural math is Adaptation.**

## Public façade

`AdaptationService`:

| Method | Status |
|--------|--------|
| `AnalyzeBook` | Wired → `BookTextAnalyzer` |
| `EstimateNaturalRuntime` | Wired → `AdaptationDensity` + `NaturalRuntime` |
| `ResolveTargetMinutes` | Pure clamp over density (`NaturalRuntime.Resolve`) |
| `BuildSystemPromptAsync` | Wired → embedded `book_to_fountain.txt` |
| `ConvertHeuristic` | Wired → offline stub |
| `ConvertAsync` | Wired → `Conversion.BookToFountainConverter` |

Pure helpers (no façade instance required):

| Type | Role |
|------|------|
| `NaturalRuntime` | Clamp, mode, natural minutes from book text |
| `AdaptationDensity` | δ / speech×staging estimator |
| `BookTextAnalyzer` | Quality + kind + suggested minutes |

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
- Phase 3: `NaturalRuntime` pure math; Engine `FilmRuntime` storage-only; BookPrepare writes natural via Adaptation
