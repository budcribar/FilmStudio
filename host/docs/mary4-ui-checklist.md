# Mary4 UI / pipeline checklist

Last updated: 2026-08-03 (commit on master)

## Done

- [x] **Agree & Continue** button on Estimate (`Cost.razor`) — advances to Film
- [x] **Target length floor** — `NaturalRuntime.MinMinutes = 1`; density micro/picture-book clamps lowered; `FilmLengthCard` min=1
- [x] **FilmRuntimeTests** updated for min=1 (11/11)
- [x] **Screenplay editability** — `setReadOnly(false)` after init/load when not busy
- [x] **Re-draft from book** — admin-only; helper text removed
- [x] **Book scene tooltip** — “Show source passage”
- [x] **Character list thumbs** — stable `@key` so looks do not drop on switch
- [x] **Character card** — middle name-strip thumbnail removed
- [x] **Unlock** moved inside **Look & voice** card header
- [x] **Empty voice UI** hidden; “Add voice…” escape hatch; animal/no-voice hint kept

## Open — High

- [ ] Live **Mary smoke**: confirm cast images stay visible on character switch
- [ ] Live **Mary smoke**: confirm natural length surfaces end-to-end (not a stale default)

## Open — Medium

- [ ] **Cost page split**
  - [ ] Current project estimate (focused)
  - [ ] All projects / account cost overview (Account / Billing / Costs)
- [ ] Live cost update when target length changes (smoke; OnChanged→LoadAsync may already work)

## Open — Pipeline structure

- [ ] Promote **Look & medium** to its own stage (Book → Look & medium → Screenplay → …)
- [ ] **Scene Embellishment** stage (after Screenplay, before Cast) — descriptive enrich only; no dialogue/beat/structure change

## Open — Later (handoff / quality)

- [ ] Mary quality loop: review Fountain + `adaptation_report.json` after v4 + medium
- [ ] Structured self-diagnostics on other prompts only if Mary report proves useful
- [ ] Benchmark dual mode (natural vs reduced) for longer books
