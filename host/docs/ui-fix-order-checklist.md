# UI fix-order checklist (from fakes audit 2026-08-05)

Priority order for product UI bugs exposed by the audit. Does **not** include Grok-loop / Imagine long-dialogue work.

## P0 — Correctness / dead ends

- [ ] **Unknown routes blank main** — `/film`, `/billing` (and other typos) show empty chrome instead of Not Found. Wire Router NotFound so users see `NotFound.razor` (or redirect `/film` → `/scenes` if intentional alias).
- [ ] **Cost length card missing when project id late** — `FilmLengthCard` only renders when `_projectId` set; ensure Estimate always shows length controls or an explicit “select project” empty state after activate.

## P1 — Discoverability

- [ ] **Create project one-click** — `+ New` / `home-new-project` should reveal the name field immediately (or inline form), not only after Manage expands.
- [ ] **Delete project visible path** — document or surface delete under Manage; audit only verified API delete.
- [ ] **Pipeline stages in nav/strip** — Look / Embellish / Trim exist as routes but are easy to miss; align `StudioProcessStrip` + sidebar labels with Film = `/scenes`.

## P2 — Polish

- [ ] **Console 404** — identify missing static/API resource from Home/Import network tab.
- [ ] **Home favicon `alt=""`** — decorative OK; confirm intentional.
- [ ] **Docs: `/billing` vs `/account/costs`** — fix any remaining links to dead `/billing`.

## P3 — Deeper fakes coverage (follow-up soaks)

- [ ] End-to-end: create → import fountain → screenplay → cast → estimate → gen scene clips → review → delete (with enhanced video fixtures).
- [ ] Character thumb stability on switch (Mary regression) under fakes images.
- [ ] Live cost refresh when target minutes change.

## Shipped with this change

- [x] UI audit report committed under `host/docs/ui-audit-report-2026-08-05.*`
- [x] Real MP4 fake fixtures (1s / 5s / 10s / scene-colored 3s)
- [x] `FakeGrokVideoClient` duration-aware fixtures + optional ffmpeg extend-concat + accurate duration sidecars
