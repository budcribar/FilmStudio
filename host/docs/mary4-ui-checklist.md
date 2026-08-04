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

- [x] **Cost page split** (Track B, commit b54df54e)
  - [x] Current project estimate (focused) — `/cost` trimmed to active project
  - [x] All projects / account cost overview — new `/account/costs` + account-menu entry
- [ ] Live cost update when target length changes (smoke; OnChanged→LoadAsync already wired)

## Open — Pipeline structure

- [x] Promote **Look & medium** to its own stage (Track C1, commit c6b5f548) — `/adaptation/look`
      in the Book-pipeline strip (Book → Look → Screenplay → Cast → Shot plan)
- [x] **Fountain→fountain re-skin** on medium change (Track C2, commit 1ffbea54) — change the look
      without a re-import; `POST /adaptation/reskin`, descriptive layer only, scene-count guardrail
- [x] **Scene Embellishment** stage (Track D1, ac77f581) — `/adaptation/embellish`, descriptive enrich only,
      scene-count guardrail
- [x] **Max fountain → embellish → trim → edit → cast** rework (plan Track D) — all landed:
  - [x] D0 generate at natural/max + persist `screenplay.max.fountain` base (9eb10ca3)
  - [x] D2 Fit-length (Trim) stage derives working draft from the base (4f05db1b)
  - [x] D5 cast reflects final screenplay via the dirty/re-approve cascade (no sign-off refactor)
  - [x] D6 fork reuse: base rides the project copy; `HasMaxBase()` + fork note
  - [ ] D6 auto-land forkers on Fit-length — deferred (needs fork-state modeling + live smoke)
  - [ ] **Live end-to-end smoke** of the new pipeline (Book→Look→Screenplay→Enrich→Fit length→Cast) — pending

## Open — Later (handoff / quality)

- [ ] Mary quality loop: review Fountain + `adaptation_report.json` after v4 + medium
- [ ] Structured self-diagnostics on other prompts only if Mary report proves useful
- [ ] Benchmark dual mode (natural vs reduced) for longer books
