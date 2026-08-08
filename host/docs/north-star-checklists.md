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
| 0 | Canonical `AiCallRecord` + one `ai_calls.jsonl` + outcome taxonomy | ⬜ Not started — admin page reads the existing ad-hoc `ApiCallTelemetry` shape as a stopgap; DB switch made the *read* side better without unifying the *record* shape |
| 1 | Migrate bespoke vision gates onto `ValidatedModelOperation` | 🟡 Partial — the specific gap that motivated this (missing retry+telemetry on GrokVisionClient) is now closed; the gates still call `CompleteWithImagesAsync` directly rather than going through the `ValidatedModelOperation` contract |
| 2 | Migrate ~15 beat classifiers | ⬜ Not started — confirmed NOT a prerequisite for 0/1, explicitly deferred by the user |
| 3 | Enforcement test (no raw client calls outside the wrapper) | ⬜ Not started — blocked on 1 (would fail immediately against the still-bespoke gates) |
| 4 | `AiCallAnalyzer` CLI + replay regression | ⬜ Not started |
| 5 | Close the loop into learning | ⬜ Not started |

**Known gap, deliberately not touched:** `GrokImageClient`/`GrokVideoClient`/`GeminiVideoClient` also have no
transient retry on submission. Not fixed alongside the vision-client fix because a naive retry on a
video/image *generation submit* risks double-submitting (and double-billing) if the first request actually
succeeded server-side but the response was lost — needs an idempotency approach, not a blind wrap. Flagged
for a future task, not attempted opportunistically.

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
