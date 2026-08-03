# Model-call inventory

Status: Active migration. See `MODEL_LIFECYCLE_MIGRATION_CHECKLIST.md` for the authoritative operation-level completion state. This inventory identifies code that can initiate or route paid/model-backed work before migration into the shared validated-operation lifecycle.

## Intended boundaries

| Namespace | Responsibility | May call a model? |
|---|---|---:|
| `PageToMovie.Engine.Deterministic` | Parsing, validation, normalization, estimation, and heuristic fallback | No |
| `PageToMovie.Engine.ModelBacked` | Feature-specific prompts, model requests, and response adapters | Yes, through `ModelExecution` |
| `PageToMovie.Engine.ModelExecution` | Transport retry, semantic correction, validation orchestration, provenance, and telemetry | Yes |

Namespaces are the first migration boundary, not a security boundary. The Phase 1 reflection test rejects declared model-client and HTTP dependencies from `Deterministic`. A later project/assembly split can enforce the rule at compile time.

## Model client surface

| Interface | Operation |
|---|---|
| `IVideoClient` | Submit and poll video generation |
| `IImageClient` | Generate and edit image variants |
| `IChatClient` | Text/model completion |
| `IVisionClient` | OCR, image classification, and multimodal completion |
| `IGeminiVideoAnalysisClient` | Native video analysis |
| `IAudioClient` | Music generation |
| `ILipSyncClient` | Video lip synchronization |
| `IVoiceCloneClient` | Voice cloning and speech synthesis |

## Provider and routing infrastructure

| Files | Current role | Phase 2/4 disposition |
|---|---|---|
| `AnthropicChatClient`, `GeminiChatClient`, `GrokChatClient`, `GrokVisionClient`, `FalVoiceCloneClient` | Provider transport and provider response handling | Keep provider adapters; route calls through `ModelExecution` |
| `MultiProviderChatClient`, `MultiProviderVisionClient` | Provider selection | Keep routing separate from validation and feature fallback |
| `CachingChatClient` | Raw chat-response caching | Include prompt/model/options/template versions in provenance and cache identity |
| `AiRetryPolicy` | Transient retries and batch coverage retries | Split transport retry from semantic correction; retain compatible primitives |
| `GenerationErrorLogger` | Generation failure telemetry | Feed standardized operation results and attempt metadata |

## Chat-backed feature operations

| Operation/file | Output shape | Current recovery pattern | Migration concern |
|---|---|---|---|
| `AiActionOverheadClassifier` | Single JSON classification | Inline parse/domain validation, then heuristic fallback | Phase 3 pilot; corrective request is missing |
| `AmbientSfxClassifier` | Batched JSON map | Parser plus coverage retry/fallback | Consolidate missing-ID validation |
| `BeatPacingClassifier` | Batched JSON map | Coverage retry and deterministic estimates | Preserve per-ID provenance |
| `CameraDirectorClassifier` | Batched JSON map | Coverage retry and defaults | Validate enum/range fields uniformly |
| `CharacterEmotionArcClassifier` | Batched JSON map | Coverage retry and defaults | Validate complete beat coverage |
| `CinematicLightingClassifier` | Structured JSON | Local parse/default behavior | Add corrective response path |
| `ColorPaletteGradingClassifier` | Structured JSON | Local parse/default behavior | Add schema/domain validator |
| `DepthOfFieldClassifier` | Batched JSON map | Coverage retry and defaults | Standardize coverage issues |
| `ExtendCutClassifier` | Per-clip classification | Repeated requests with local merge | Separate correction from transport retry |
| `NegativePromptClassifier` | Structured JSON | Local parse/default behavior | Validate allowed negative-prompt fields |
| `OnScreenCastClassifier` | Batched cast assignment | Retry-until-covered and deterministic cleanup | Preserve requested/returned IDs |
| `PlateRankClassifier` | Ranked plate selection | Local parsing and fallback | Validate candidate membership and ordering |
| `ShotPlanRefiningClassifier` | Refined shot plan | Inline parsing and exception fallback | Must reject dialogue/identity mutation |
| `SilentBeatActionClassifier` | Batched action labels | Configured retries then heuristics | Candidate for early Phase 4 migration |
| `SoundDesignComposerClassifier` | Batched sound design | Coverage retry and defaults | Validate allowed audio fields |
| `SpeciesKindClassifier` | Cast species classification | Inline attempts and deterministic fallback | Validate closed taxonomy |
| `WardrobeContinuityClassifier` | Batched wardrobe decisions | Coverage retry and defaults | Validate cast and scene references |
| `CastFromScreenplayService` | Cast JSON | Inline parsing/cleanup | Validate stable character keys and completeness |
| `CastVisualLiteralizeService` | Literal visual descriptions | Inline parse/fallback | Prevent names/text from leaking into visual locks |
| `BookToFountainConverter` | Fountain plus vision metadata | Multiple specialized repair calls | Express every repair as a validated operation |
| `ProjectVisionMeta` | Structured visual metadata | Parse/repair/default behavior | Adopt shared result provenance |
| `SceneMusicScoringService` | Music-scoring analysis | Separate prompts and local parsing | Consolidate validation and failure policy |
| `LearningProposalService` | Learning proposal JSON | Inline parsing and exception handling | Treat as advisory; never silently apply invalid output |
| `MovieAutoReviewService` | Review synthesis | Optional chat synthesis | Mark unavailable vs fallback explicitly |
| `Stage1Service` | Scene bible | Large structured generation | Requires schema, semantic, and coverage validation |
| `Stage2PlannerService` | Clip/shot plan | Large structured generation plus classifiers | Requires immutable dialogue and pronunciation annotations |
| `FilmJobService` | Pipeline orchestration | Delegates to model-backed services | Consume standardized results; should not parse raw model text |

## Vision and multimodal feature operations

| Operation/file | Model-backed work | Migration concern |
|---|---|---|
| `BookPrepareService` | Page transcription/OCR | Validate page ordering and missing text |
| `CharacterBookPlateService` | Character presence/classification | Validate candidate identities and page references |
| `CharacterDesignService` | Character image analysis | Validate identity/visual fields |
| `ClipAutoReviewService` | Generated-clip visual review | Keep reviewer uncertainty and evidence |
| `ClipDialogueVerificationService` | Dialogue transcription and comparison | Preserve expected/transcribed text and distinguish truncation |
| `JitBenchmarkService` | Optional vision benchmark judging | Record judge model, rubric, and artifact hashes |
| `MovieAutoReviewService` | Movie-level multimodal review | Separate observation from synthesized judgment |
| `SceneMusicCompositionService` | Scene analysis for composition | Validate requested scene and resulting musical intent |

## Media-generation operations

Media calls are also model calls even when they do not return JSON. Call sites using `IVideoClient`, `IImageClient`, `IAudioClient`, `ILipSyncClient`, and `IVoiceCloneClient` must ultimately return the same operation provenance: provider/model, parameters, attempt history, artifact hashes, and terminal status. Binary/media validation remains feature-specific and should not be conflated with JSON correction.

## Known Phase 1 gaps

- Most existing engine types remain in the root `PageToMovie.Engine` namespace until their behavior is separated and migrated.
- Several services mix deterministic parsing with model invocation; moving the whole class would incorrectly label part of its behavior.
- The architecture test catches declared dependencies but cannot inspect arbitrary method bodies. A separate deterministic assembly is the eventual hard boundary.
- API endpoints and tool projects can initiate model work indirectly; Phase 4 must inventory registrations and orchestration outside the engine before declaring migration complete.

## Completion checklist

- [x] Namespace conventions selected and created.
- [x] Deterministic dependency rule added.
- [x] Chat, vision, media, provider, and routing surfaces inventoried.
- [x] Model-backed classifier family moved behind an explicit namespace boundary.
- [x] Reference classifier split and migrated to the validated-operation lifecycle.
- [ ] Large mixed operations split as their individual schemas are versioned.
- [ ] All remaining calls routed through the shared validated-operation lifecycle.
- [ ] Deterministic code moved into a separately enforceable assembly or equivalent compile-time boundary.
