# UI fix-order checklist (fakes audit 2026-08-05+)

Legend: `[ ]` open · `[~]` partial · `[x]` done / verified · code deferred until tests/go-ahead

## Product decisions (documentation)

- [x] **`/demo` is public — terms not required.** Gallery must work with no Terms modal.
- [ ] **Fix:** stop showing `TermsAgreementModal` on `/demo` (and any other public routes). Pass 4 observed modal on demo → bug.
- [ ] Broader code fixes — after remaining tests or explicit go-ahead

## P0 — Correctness / dead ends

- [ ] **Demo without terms** — `/demo` must not show Terms modal; content usable while logged out / terms not accepted.
- [ ] **Unknown routes blank main** — `/film`, `/billing` → Not Found or `/film` → `/scenes`.
- [ ] **Cost length card missing** — active project + terms, length input still absent.
- [ ] **API terms enforcement (studio only)** — mutating studio routes (e.g. `POST /api/projects`) require terms; **not** public demo GETs.
- [~] **Agree & Continue vs CanScenes** — soft navigate to `/scenes`; prefer disable or clearer not-ready UX.
- [x] **Create empty name** — Create stays disabled for whitespace.
- [x] **Terms modal blocks studio chrome** — verified (Home, Cost, Adaptation, etc.).
- [x] **Terms accept persists on reload** — verified.

## P1 — Discoverability

- [ ] Create project one-click name field.
- [ ] Delete project under Manage.
- [ ] Look / Embellish / Trim discoverable.
- [ ] Deep-link empty CTAs without project.

## P2 — Input & control-state

- [ ] Film length boundaries once card visible.
- [ ] Busy/JobRunning double-submit.
- [ ] Strip vs page CTA parity.
- [ ] Back-nav after book change.

## P3 — Polish

- [ ] Console 404.
- [ ] Home favicon alt.
- [ ] Docs links `/billing` → `/account/costs`.

## P4 — Fakes soaks

- [ ] E2E gen → review with real fixtures.
- [ ] Character thumbs.
- [ ] Live cost on length change.

## Testing still needed

### A. Sequence matrix (terms accepted — **studio** paths only)

| # | Test |
|---|------|
| S2b | Cost after book/import — does length card appear? |
| S3 | Book, screenplay not approved → Cast / Estimate / Film |
| S4 | Screenplay OK, no shots → Estimate vs Generate |
| S5 | Ready to film → Generate once; job disables re-entry |
| S6 | Length boundaries once input visible |
| S8 | Change book after estimate → re-gate |
| S9 | Delete project via UI (Manage) |

### B. Terms / public vs studio

| # | Test |
|---|------|
| T1 | `POST /api/projects` without terms → should 4xx after fix |
| T2 | Other **studio** mutating APIs without terms |
| T3 | **`/demo` with no terms accept → no modal, gallery usable** (regression) |
| T4 | Fresh user on studio Home → modal; on `/demo` → no modal |
| T5 | After terms accept, studio works; demo still works |

### C. Not Found & navigation

| # | Test |
|---|------|
| N1 | `/film`, `/billing`, junk → Not Found (not blank) |
| N2 | Strip Film → `/scenes` |

### D. Optional later (P4)

- Full fake clip gen → Review  
- Character image switch  
- Cost $ on length change  

## Shipped

- [x] Audit + checklist on `master` (**demo is public / no terms** decision corrected)
- [x] Real MP4 fake fixtures + FakeGrokVideoClient improvements
