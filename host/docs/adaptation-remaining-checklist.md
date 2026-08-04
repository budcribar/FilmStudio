# Adaptation / Stage‑1 remaining checklist

Last updated: 2026-08-03 (post v4 prompt + visual medium UI)

## Done (recent)

- [x] **book_to_fountain v4** live at `prompts/book_to_fountain.txt` (VISION_META + ADAPTATION_REPORT; rebuild Adaptation embed or set `PAGETOMOVIE_PROMPTS_DIR`)
- [x] **All prompt tokens resolved** (`AdaptationPromptTokens` + `ApplyPromptTokens`; throws if any `{{TOKEN}}` left)
- [x] **Visual medium UI** (`VisualMediumCard` on Import + Screenplay → `GET/PUT …/visual-medium` → `AdaptationRequest.VisualMedium`)
- [x] Engine cast shim removed; `ProjectAdaptationConversionResult` / `ProjectVisionMetaStatus` naming
- [x] Stage commits, film_build EDL, convert manifest, ADAPTATION_REPORT parse/store
- [x] Default Stage‑1 runtime = **unlimited** (retarget only when user sets minutes)
- [x] Adaptation module Phases 0–5 + Engine mapping-only façade (`MapVision` stays in Engine)
- [x] Stage‑1 via `AdaptationService` only + golden fixtures / fake-chat tests
- [x] xAI file_id + Responses multi-turn + admin book cache
- [x] `cast_kind: group` + hidden on Characters UI
- [x] Deterministic speaker ⊆ cast + membership scores on leaderboard
- [x] Dirty Adaptation-source benchmark gate

## Open — next (after Mary $ run)

### Mary quality loop
- [ ] Generate Mary with v4 + medium preference; review Fountain + `source/adaptation_report.json`
- [ ] Judge **spec_feedback / issues** — if useful, add structured self-diagnostics to other major prompts (cast, stage2, clip review)
- [ ] Prompt tighten only if report/Fountain still invents named groups or pads length

### Runtime / length (product)
- [ ] Film length card already exists; confirm natural length surfaces after import end-to-end
- [ ] Benchmark dual mode (natural vs reduced) for longer books (not Mary-first)

### Observability (mostly done; polish later)
- [ ] Optional: single Stage‑1 attempt timeline UI (telemetry + generation_errors + manifest already exist in DB/files)

### Later
- [ ] Learning / critic loop (FFmpeg clip metadata, hash, post-YouTube analysis)
- [ ] Stage‑2 / clip consistency judge
- [ ] Cross-prompt self-diagnostics convention (if Mary report proves actionable)

## Rules agents must not forget

1. **Stage‑1 logic only in `PageToMovie.Adaptation`.** Engine maps vision + persists; no second Fountain converter.
2. **Prompt tokens:** never ship unresolved `{{…}}`. Add new tokens to `AdaptationPromptTokens` / `ApplyPromptTokens` first.
3. **Models catalog is SSoT** — no hardcoded provider/model assumptions (see `AGENTS.md`).
4. **Do not auto-start paid benchmarks** without explicit user go-ahead.
5. **Rebuild** after changing `prompts/book_to_fountain.txt` so the embedded resource updates (or use `PAGETOMOVIE_PROMPTS_DIR`).

## Quick pointers

| Item | Code / path |
|------|-------------|
| Live Stage‑1 prompt | `prompts/book_to_fountain.txt` (v4) |
| Token substitution | `AdaptationPromptPack.ApplyPromptTokens` |
| Visual medium API | `/api/projects/{id}/visual-medium` |
| Medium UI | `VisualMediumCard.razor` |
| Convert manifest | `source/stage1_convert_manifest.json` |
| Adaptation report | `source/adaptation_report.json` |
| Speaker ⊆ cast | `Adaptation.Validation.CastPackageCrossCheck` |
| Fake chat Stage‑1 | `AdaptationFakeChatTests.cs` |

---

## Mary4 UI follow-up

See **[mary4-ui-checklist.md](./mary4-ui-checklist.md)** for Estimate continue, runtime floor, screenplay/cast UI items (2026-08-03).
