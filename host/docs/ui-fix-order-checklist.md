# UI fix-order checklist (fakes audit 2026-08-05+)

Priority order for product UI bugs. Does **not** include Grok-loop / Imagine long-dialogue work.

Legend: `[ ]` open · `[~]` partial / needs browser confirm · `[x]` done

## P0 — Correctness / dead ends

- [ ] **Unknown routes blank main** — `/film`, `/billing` show empty chrome instead of Not Found. Prefer `NotFound.razor`, or alias `/film` → `/scenes`.
- [ ] **Cost length card when project id late** — always show `FilmLengthCard` or explicit “select project” empty state.
- [ ] **Agree & Continue vs CanScenes** — do not navigate to Film when shot plan / scenes readiness is false; disable or show `ScenesBlockedReason`.
- [ ] **Create project empty name** — block submit and show “name required” (today: silent no-op).

## P1 — Discoverability & sequence UX

- [ ] **Create project one-click** — `home-new-project` should show name field immediately (not only after Manage expands).
- [ ] **Delete project visible path** — surface under Manage; audit only verified API delete.
- [ ] **Pipeline stages in nav/strip** — Look / Embellish / Trim discoverable; Film label → `/scenes`.
- [ ] **Deep link empty states** — `/cost`, `/scenes`, `/characters`, `/adaptation/import` without project: clear CTA to create/select.

## P2 — Input & control-state (explicit tests required)

- [ ] **Film length boundaries** — `""`, `0`, `-1`, `1`, `180`, `181`, non-numeric → clamp 1–180 + visible feedback.
- [ ] **Primary buttons vs Busy/JobRunning** — Import, Convert, Generate, Save: disabled and no double-submit.
- [ ] **Strip readiness matches page CTAs** — same `CanEstimate` / `CanScenes` / `CanCharacters` on strip and page buttons.
- [ ] **Back-navigation staleness** — change book after estimate; Film/Generate must re-gate.

## P3 — Polish

- [ ] **Console 404** — identify missing static/API resource.
- [ ] **Home favicon `alt=""`** — confirm decorative.
- [ ] **Docs links** — `/billing` → `/account/costs`.

## P4 — Deeper fakes soaks

- [ ] E2E: create → import fountain → screenplay → cast → estimate → gen clips → review → delete (real fixtures).
- [ ] Character thumb stability on switch (Mary) under fakes images.
- [ ] Live cost refresh when target minutes change.

## Sequence test matrix (explicit browser)

| # | Setup | Assert |
|---|--------|--------|
| S1 | No project | Import/Cost/Scenes/Characters primaries disabled or CTA-only |
| S2 | Project, no book | Import enabled; Cast/Estimate/Film blocked with reasons |
| S3 | Book, no screenplay approve | Cast/Estimate/Film per `ActiveProjectState` |
| S4 | Screenplay OK, no shots | Estimate may open; Film/Generate blocked |
| S5 | Ready to film | Generate enabled once; disabled while job runs |
| S6 | Length card | Boundary values clamp; estimate refreshes |
| S7 | Create project | Empty/spaces rejected with message; double-click safe |
| S8 | Back-nav after book change | Stale CanScenes false until ready again |

## Shipped

- [x] UI audit report `host/docs/ui-audit-report-2026-08-05.*`
- [x] Real MP4 fake fixtures + smarter `FakeGrokVideoClient` (`0bf913a`)
- [x] Docs updated for sequence/button/input gap (this revision)
