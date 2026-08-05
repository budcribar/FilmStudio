# UI fix-order checklist (fakes audit 2026-08-05+)

Legend: `[ ]` open · `[~]` partial · `[x]` done / verified · **no code until tests listed below are done or explicitly waived**

## Product decisions (documentation)

- [x] **`/demo` requires terms** — intentional; same modal as studio. Do not special-case demo to skip terms.
- [ ] Code fixes — start only after remaining tests (or explicit go-ahead)

## P0 — Correctness / dead ends

- [ ] **Unknown routes blank main** — `/film`, `/billing` → Not Found or `/film` → `/scenes`.
- [ ] **Cost length card missing** — active project + terms accepted, length number input still absent.
- [ ] **API terms enforcement** — mutating routes (at least `POST /api/projects`) must require terms accepted; UI already blocks.
- [~] **Agree & Continue vs CanScenes** — navigates to `/scenes` with soft blocked hint; strip Film disabled. Prefer disable Agree or clearer not-ready UX.
- [x] **Create empty name** — Create stays disabled for whitespace.
- [x] **Terms modal blocks studio + demo** — verified.
- [x] **Terms accept persists on reload** — verified.

## P1 — Discoverability

- [ ] Create project one-click name field.
- [ ] Delete project under Manage.
- [ ] Look / Embellish / Trim discoverable in strip/nav.
- [ ] Deep-link empty CTAs without project.

## P2 — Input & control-state

- [ ] Film length boundaries once card is visible.
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

## Testing still needed (before / while fixing code)

### A. Finish sequence matrix (fakes, terms accepted first)

| # | Test | Why |
|---|------|-----|
| S2b | Cost with project **after** import/book meta exists | Length card may only appear when runtime API returns data |
| S3 | Book present, screenplay not approved | Cast / Estimate / Film button + strip states |
| S4 | Screenplay approved, no shot plan | Estimate vs Film / Generate |
| S5 | Ready to film | Generate enabled once; disabled while `JobRunning`; no double-submit |
| S6 | Length `0` / `-1` / `181` / `""` | Only after length input is visible (or after P0 length-card fix) |
| S8 | Change book after estimate | CanScenes / Generate must re-gate |
| S9 | Delete project via **UI** (Manage) | Only API delete verified so far |

### B. Terms / API policy tests

| # | Test | Why |
|---|------|-----|
| T1 | `POST /api/projects` without terms → **4xx** after fix | Today allows 200 |
| T2 | Other mutating APIs without terms (import, gen, delete) | Same policy surface |
| T3 | UI still blocked on `/demo` until terms | Regression guard (product: demo requires terms) |
| T4 | Fresh profile / other user id | Modal shows again; accept is per-user |

### C. Not Found & navigation

| # | Test | Why |
|---|------|-----|
| N1 | `/film`, `/billing`, garbage path | Must show Not Found (or alias), not blank main |
| N2 | Strip “Film” always targets `/scenes` | Label vs route consistency |

### D. Optional later (P4)

- Full fake clip gen → Review → delete clips  
- Character image switch stability  
- Cost $ updates when target minutes saved  

## Shipped

- [x] Audit + checklist docs on `master` (including demo-requires-terms decision)
- [x] Real MP4 fake fixtures + FakeGrokVideoClient improvements
