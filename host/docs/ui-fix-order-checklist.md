# UI fix-order checklist (fakes audit 2026-08-05+)

Legend: `[ ]` open · `[~]` partial · `[x]` done / verified

## P0 — Correctness / dead ends

- [ ] **Unknown routes blank main** — `/film`, `/billing` → Not Found or alias (`/film` → `/scenes`).
- [ ] **Cost length card when project id late** — always show card or “select project” empty state.
- [ ] **Agree & Continue vs CanScenes** — disable or show `ScenesBlockedReason`; do not navigate when Film is blocked on the strip.
- [x] **Create project empty name** — **verified:** Create button stays disabled for whitespace (pass 3). Optional: add helper text “Enter a name”.
- [ ] **Terms modal vs session** — first-run modal must stay; ensure accept persists so it does not reappear every refresh for same user (verify cookie/user id wiring for `local` / admin).

## P1 — Discoverability & sequence UX

- [ ] **Create project one-click** — show name field immediately on `home-new-project`.
- [ ] **Delete project visible path** — under Manage.
- [ ] **Pipeline stages in nav/strip** — Look / Embellish / Trim; Film → `/scenes`.
- [ ] **Deep link empty states** — clear CTA without project.

## P2 — Input & control-state

- [ ] **Film length boundaries** — `""`, `0`, `-1`, `1`, `180`, `181` → clamp 1–180 + feedback (test incomplete).
- [ ] **Busy/JobRunning** — Import/Generate/Save no double-submit.
- [ ] **Strip readiness matches page CTAs**.
- [ ] **Back-navigation staleness** after book change.

## P3 — Polish

- [ ] Console 404 resource.
- [ ] Home favicon `alt=""`.
- [ ] Docs: `/billing` → `/account/costs`.

## P4 — Fakes soaks

- [ ] E2E gen clips → review with real fixtures.
- [ ] Character thumb stability.
- [ ] Live cost refresh on length change.

## Sequence matrix

| # | Status | Notes |
|---|--------|--------|
| S1 No project deep links | [~] | Pages open; empty-state quality TBD |
| S2 Project, no book | [ ] | Blocked once by terms; retry after accept |
| S3–S5 readiness | [ ] | Not run |
| S6 Length boundaries | [ ] | Not run |
| S7 Create empty name | [x] | Button disabled for whitespace |
| S7 UI create valid name | [ ] | Interrupted |
| S8 Back-nav | [ ] | Not run |

## Shipped

- [x] Audit report + this checklist on `master`
- [x] Real MP4 fake fixtures + `FakeGrokVideoClient` enhancements
- [x] Pass 3 partial sequence findings (terms modal, create disabled)
