# UI fix-order checklist (fakes audit 2026-08-05+)

Legend: `[ ]` open · `[~]` partial · `[x]` verified

## Product

- [x] **`/demo` is public — terms not required** (docs)
- [ ] **Code:** do not show Terms modal on `/demo`

## P0 — Confirmed failures (pass 5)

- [ ] **Demo without terms** — modal must not appear on `/demo`
- [ ] **Not Found** — `/film`, `/billing`, unknown paths show Not Found (not blank `main`)
- [ ] **API terms** — `POST /api/projects` without terms → 4xx
- [ ] **Cost length card** — number input with active project (after import still missing)

## P0 — Other

- [~] Agree & Continue vs CanScenes (soft empty on `/scenes`)
- [x] Create empty name disabled
- [x] Terms blocks studio chrome
- [x] Terms accept persists on reload
- [x] S9 Delete visible under Manage

## P1–P4

- [ ] Create one-click name field; Look/Embellish/Trim discoverability
- [ ] Length boundaries once card visible; JobRunning double-submit; back-nav
- [ ] Console 404; favicon alt; docs `/billing` → `/account/costs`
- [ ] E2E fake gen → review; character thumbs; live cost

## Testing still needed

| ID | Status |
|----|--------|
| T3 demo no modal | FAIL confirmed |
| T1 API terms | FAIL confirmed |
| N1 Not Found | FAIL confirmed |
| S2b length card | FAIL confirmed |
| S3–S5 readiness after real screenplay approve | Pending (need successful convert/approve) |
| S6 length boundaries | Blocked until length card shows |
| S8 book change re-gate | Pending |
| S9 UI delete **click** (confirm dialog + gone) | Control visible only |
| T2 other mutating APIs without terms | Pending |
| Full fake gen soak | Pending |

## Shipped

- [x] Docs + fake MP4 fixtures on `master`
- [x] Pass 5 continue-tests report under `artifacts/ui-audit/`
