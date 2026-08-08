# North-star checklists

Two tracks the user asked to keep tracked across sessions. Checklist A (AI-call feedback loop) is
the active, highest-priority track. Checklist B (pre-UI-consolidation) is on hold per the user's own
priority call — items here are not resumed without the user asking.

## Checklist A — AI-Call Feedback Loop (highest priority)

Design: `AiCallAnalyticsService` + admin `/admin/ai-calls` page. Full plan: the "AI-Call Feedback Loop"
design-doc artifact (5 moves A–E: one contract / one record+sink / one outcome taxonomy / enforce it /
analyzer+loop).

| # | Item | Status |
|---|------|--------|
| — | Design doc + admin analytics page | ✅ Done, shipped, deployed |
| — | Fakes emit telemetry (chat) | ✅ Done |
| — | Fakes emit telemetry (image/video/vision) | ✅ Done — chat/image/image_edit/video/video-extend/vision/review, plus transcribe_page/classify_characters added 2026-08-07 |
| — | Style-gate override + reason capture (3-chip: AI wrong / my preference / other) | ✅ Done end-to-end, incl. reason-breakdown surfaced in admin analytics (2026-08-07) |
| — | AI-calls analytics reads `user_api_calls` (SQLite), not JSONL scan | ✅ Done (2026-08-07) — dual-write to JSONL continues, but the admin page and all aggregation now query the DB |
| — | Admin Generation Errors page (`generation_errors` table) | ✅ Done (2026-08-07) — API endpoint existed, had no UI until now |
| — | Transient retry (429/5xx/network) on `GrokVisionClient` (style gate, dialogue-verify, cast-on-image, transcribe, classify) | ✅ Done (2026-08-07) — was the one client of the four (Grok/Anthropic/Gemini chat+vision) with no retry at all |
| — | Transient retry on `GrokImageClient` (generate + edit) | ✅ Done (2026-08-07) — user call: image gen is cheap enough that the small double-generation risk is acceptable, unlike video |
| — | `Attempt` field reflects real retry count (not misc counters) across all chat/vision/image clients | ✅ Done (2026-08-07) — was never set at all on Grok/Anthropic/Gemini chat+vision, and was silently repurposed as *variant index* in `GrokImageClient.EditVariantsAsync`, both of which broke the "retried" analytics stat |
| — | `Retry-After` header honored on 429s, quadratic-backoff cap raised 4s→15s | ✅ Done (2026-08-07) — `ChatHttpStatusException.FromResponse(resp, msg)` factory, used at all provider-client throw sites; coverage-retry cap (beat classifiers) deliberately left untouched, different concern |
| — | Transient retry + typed job outcome on `GrokVideoClient`/`GeminiVideoClient` | ✅ Done (2026-08-07) — user's call, reversing the earlier "known gap" below: submit-response loss is unrecoverable either way (no request_id to find the job), so auto-retry is no riskier than a human's manual retry; poll-call retry is pure upside (idempotent GET, zero billing risk, previously one blip abandoned an already-paying job). New `Kind="video_job"` summary row (`ok`/`ok_after_retry`/`provider_failed`/`expired`/`timed_out`/`poll_failed`) logged once per job at the point poll resolves — the typed-outcome/provenance piece, since submit+poll's async shape doesn't fit `ValidatedModelOperation`'s single-request contract. Retry-attempt logging pulled out of 4 near-duplicate private methods into one shared `GenerationErrorLoggerExtensions.LogRetryAttemptAsync` extension (`GenerationRetryTelemetry.cs`) reused by GrokVisionClient/GrokImageClient/GrokVideoClient/GeminiVideoClient — user: "don't mirror - reuse". Fakes parity: `FakeGrokVideoClient` logs the same `video_job` row. |
| 0 | Canonical outcome taxonomy | ✅ Done (2026-08-07), scoped per user direction — see below |
| 1 | Migrate bespoke vision gates onto `ValidatedModelOperation` | 🟡 Partial — **portrait style gate migrated 2026-08-07** (pilot, see below); dialogue-verify and cast-on-image gates still bespoke |
| 2 | Migrate ~15 beat classifiers | ✅ **Already done** (discovered 2026-08-07, not new work) — every coverage-retry classifier already routes through `ValidatedModelOperation` via `AiRetryPolicy.RunWithCoverageRetryAsync` → `ValidatedCoverageOperation.ExecuteAsync`. Full transport retry, parse/validate, corrective re-ask on missing ids, deterministic fallback, and `ModelOperationTraceScope` provenance — this must have landed as an infra refactor without the design doc being updated. See "Beat classifiers vs. video/image" below. |
| 3 | Enforcement test (no raw client calls outside the wrapper) | ✅ Done (2026-08-08) — allowlist-based, not blocking: see below |
| 4 | `AiCallAnalyzer` CLI + replay regression | ⬜ Not started — beat classifiers are already replay-ready (provenance trace exists); vision/video/image are not (video's new `video_job` outcome row is telemetry, not a `ModelOperationTraceScope` provenance trace — replay still doesn't reach video/image) |
| 5 | Close the loop into learning | ⬜ Not started |

**Phase 3 — enforcement test (2026-08-08).** `PageToMovie.Tests/RawModelClientEnforcementTests.cs`: source-scans
`PageToMovie.Engine` (same pattern as the pre-existing `AdaptationModuleBoundaryTests`) for direct calls to
`CompleteAsync`/`CompleteWithImagesAsync`/`ClassifyCharactersOnImageAsync`/`TranscribePageAsync`. Doesn't block
on Phase 1 finishing first — it's allowlist-based: a call site is fine if it's inside a sanctioned wrapper file
(`ModelBacked/*` operations, or a coverage-retry classifier's own `callChat` lambda) **or** explicitly listed in
`KnownBespokeDebt` with a reason; it only fails on **new, undocumented** drift. `KnownBespokeDebt` is the honest,
complete inventory of remaining bespoke call sites (9, not the 3 the design doc originally named) — this audit
itself is new information:
- `ClipDialogueVerificationService.cs` (vision) — dialogue-verify gate
- `CharacterBookPlateService.cs` (vision, `ClassifyCharactersOnImageAsync`) — cast-on-image gate
- `SceneMusicCompositionService.cs` (vision) — music-supervisor scoring prompts
- `SceneMusicScoringService.cs`, `LearningProposalService.cs`, `ProjectVisionMeta.cs`, `PlateRankClassifier.cs` (chat) — not previously named in the design doc's "3 vision gates" framing
- `BookPrepareService.cs` (vision, `TranscribePageAsync`) — book-page OCR
- `JitBenchmarkService.cs` (vision) — calibration benchmark, not the live per-generation pipeline

A second test (`KnownBespokeDebt_entries_are_still_accurate`) fails if a listed file stops calling a raw
client (migration landed, entry went stale) or stops existing — so the allowlist can't silently rot in either
direction. **Caught a real gap on its first run**: `MultiProviderChatClient`/`MultiProviderVisionClient`
(model-id routing) and `CachingChatClient` (a caching decorator) also call the raw interface methods — these
are `IChatClient`/`IVisionClient` *implementations* (infrastructure, same category as `GrokChatClient.cs`),
not bespoke callers bypassing the wrapper; added to the sanctioned list once identified.

**Phase 0 — canonical outcome taxonomy (2026-08-07):** user's scoping call — no real production data exists
yet, so there's no migration/backfill story to design around, and no value in keeping a second (legacy)
classification path alive "just in case." Rejected the original design doc's literal ask (rename
`ApiCallTelemetry`→`AiCallRecord`, touch all ~15+ write call sites) in favor of the much cheaper equivalent:
`AiCallOutcome` enum (the same 12 values: ok/ok_after_retry/fallback/coverage_gap/validation_reject/
vision_blind/parse_error/schema_invalid/rate_limited/timeout/provider_refusal/cancelled) added as a field on
the *existing* `ApiCallTelemetry`, classified **once, centrally**, inside `ProjectTelemetryService.LogApiCallAsync`
— the single chokepoint every call site already funnels through — instead of touching each site individually.
`AiCallAnalyticsService.ClassifyFailure` (read-time string-guessing on `Error` text, e.g. checking for the
substring "blind") is **deleted outright**, not kept as a fallback — no dual system. New `outcome` column on
`user_api_calls` (`EnsureColumn`, same idiom used all session). **Known scope boundary, not a gap:** the
central classifier only sees transport-level signals (HTTP status, exception type, attempt count), so it
can only reliably produce `ok`/`ok_after_retry`/`rate_limited`/`timeout`/`provider_refusal`/`parse_error`/
`cancelled`. The semantic-only values (`fallback`/`coverage_gap`/`validation_reject`/`vision_blind`/
`schema_invalid`) need a caller with business-logic context to set `ApiCallTelemetry.Outcome` explicitly
before logging — nothing does this yet. Deliberately did NOT wire this into `CharacterDesignService`'s plain
(non-override) style-gate rejection this pass — that call site has no `ProjectTelemetryService` dependency
today, and adding one is a real DI change with its own blast radius, not something to fold in opportunistically
while already mid-batch on the taxonomy itself.

**Portrait style gate migration (2026-08-07, pilot for Phase 1):** `CharacterDesignService.RunPortraitStyleGateAsync`
now runs through `ValidatedModelOperation<PortraitStyleGateInput, string, PortraitStyleGateResult>` instead of a raw
`CompleteWithImagesAsync` + manual parse. New `PageToMovie.Engine/ModelBacked/PortraitStyleGateOperation.cs`:
`PortraitStyleGateOperation` (vision-flavored sibling of `Stage2DirectiveOperation` — same corrective-retry prompt
injection, calls `IVisionClient.CompleteWithImagesAsync` instead of `IChatClient.CompleteAsync`),
`PortraitStyleGateResponseParser` (adapts the existing, still-unit-tested `ParsePortraitStyleGateResponse`),
`PortraitStyleGateValidator` (new — rejects an unrecognized `medium`, e.g. a hallucinated value; nothing checked
this before). `PortraitStyleGateResult` changed from a `readonly record struct` to a `sealed record` (`TResult`
must be a class). Generalized `DirectiveTerminalFallback<T>` → `DirectiveTerminalFallback<TInput, TResult>` (was
hardcoded to `Stage2DirectiveInput`) so the vision gate could reuse it verbatim instead of forking a duplicate —
updated its 3 existing callers (`NegativePromptClassifier`/`ColorPaletteGradingClassifier`/
`CinematicLightingClassifier`) + 2 test call sites to the two-type-param form. `TransportMaxAttempts` set to 1
explicitly — `CompleteWithImagesAsync` already retries transiently inside itself; leaving the outer default (3)
would have multiplied attempts the same way the earlier beat-classifier near-miss would have. Gains: corrective
re-ask on malformed JSON or an invalid medium (previously an immediate hard failure), schema validation, and
provenance/reproducibility tracing via `ModelOperationTraceScope`. Dialogue-verify and cast-on-image gates not
yet migrated — dialogue-verify's response shape is materially more complex (many field-name aliases, computed
accuracy fallback, speaker-name post-processing) and deserves its own pass rather than copy-pasting this one.

**Beat classifiers vs. video/image clients — architecture comparison (2026-08-07, superseded by the row above
for the retry/billing point specifically — kept for the provenance/fallback comparison, which still holds):**
the beat classifiers are still more architecturally mature than `GrokImageClient`/`GrokVideoClient`/
`GeminiVideoClient` in two ways even after today's video work:
1. **Centralized vs. duplicated** — classifiers share one pipeline; video/image still hand-roll telemetry at
   each call site (now DRY'd up for retry-logging via the shared extension, but not unified into one
   pipeline the way `ValidatedCoverageOperation` unifies the classifiers).
2. **No provenance/replay** on video/image — no reproducibility hash, no `ModelOperationTraceScope` entry;
   the new `video_job` row is real typed-outcome telemetry but not the same thing as a replayable trace.
The retry/billing asymmetry that WAS point 3 here no longer applies to video specifically — see the row above.
**No deterministic fallback** still holds for video/image (a failed generation just throws) and reasonably
so — there's no way to return "half a video."

## Checklist B — Pre-UI-Consolidation (ON HOLD — do not resume without being asked)

| Item | Status |
|------|--------|
| A-1 E2E through Scenes (+ varied fixtures) | ✅ Done |
| A-1b Clip generation → Scenes/Review unlock | ✅ Done |
| A-3 Characters operator flow (looks/lock/voice) | ✅ Done |
| A-2 Review page depth | ⬜ Not started |
| A-4 Configuration depth | ⬜ Not started |
| A-5 Home depth | ⬜ Not started |
| B: bug-fix-first (jargon audit) | 🔵 Superseded — folded into the localization backlog |
| C: RCL decision / extraction order / Scenes component boundaries | 🟡 In progress, unmerged — branch `refactor/blazor-components`: RCL `PageToMovie.Components` created, 5 presentational components moved, `ConfirmModal` built + applied to Scenes delete dialogs, verified (build clean, 1570 tests, fakes-browser smoke). Not merged — needs the user's visual review. Open decision queued: full component extraction (slow, regression-prone, "right" structure) vs. code-behind split (`@code` → `.razor.cs`, near-zero-risk, halves file sizes, but a bigger restructure than asked for) |
