# UI fix-order checklist (fakes audit 2026-08-05+)

Legend: `[ ]` open · `[~]` partial · `[x]` done / verified in browser (fakes)

## P0 — Correctness / dead ends

- [ ] **Unknown routes blank main** — `/film`, `/billing` → Not Found or `/film` → `/scenes`.
- [ ] **Cost length card missing** — with active project after terms, number input still absent (pass 4). Fix binding / empty state.
- [ ] **API terms enforcement** — `POST /api/projects` (and other mutating routes) should require terms accepted, same as UI modal. Today UI blocks, API allows.
- [~] **Agree & Continue vs CanScenes** — navigates to `/scenes`; strip Film disabled; page shows blocked hint. Prefer disable Agree or explicit “not ready” instead of landing on empty Film.
- [x] **Create project empty name** — Create button stays disabled for whitespace.
- [x] **Terms modal blocks studio chrome** — nav + New project intercepted until accept.
- [x] **Terms accept persists across reload** (same browser session / user).

## P1 — Discoverability

- [ ] Create project one-click name field.
- [ ] Delete project visible under Manage.
- [ ] Pipeline stages Look / Embellish / Trim discoverable.
- [ ] Deep-link empty CTAs without project.

## P2 — Input & control-state

- [ ] Film length boundaries once card is visible (`0`, `-1`, `181` → clamp).
- [ ] Busy/JobRunning double-submit.
- [ ] Strip vs page CTA parity (Agree vs Film step).
- [ ] Back-nav after book change.

## P3 — Polish

- [ ] Console 404.
- [ ] Home favicon alt.
- [ ] Docs `/billing` → `/account/costs`.
- [ ] **Demo / public gallery vs terms** — pass 4 showed terms on `/demo`; confirm product intent (public demo may should skip terms).

## P4 — Fakes soaks

- [ ] E2E gen → review with real fixtures.
- [ ] Character thumbs.
- [ ] Live cost on length change.

## Sequence matrix

| # | Status |
|---|--------|
| Pre-terms UI block | [x] |
| Pre-terms API block | [ ] FAIL — create allowed |
| Terms accept + reload | [x] |
| S1 deep links | [x] |
| S2 Agree / strip Film | [~] |
| S6 length | [ ] blocked by missing input |
| S7 empty + valid create | [x] |
| S3–S5, S8 | [ ] |

## Shipped docs / fakes

- [x] Audit reports + this checklist on `master`
- [x] Real MP4 fixtures + FakeGrokVideoClient improvements
