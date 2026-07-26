# Issue 9 — Spoofable identity and open spend endpoints

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | partially fixed |
| Branch | `fix/issue-9-spoofable-user-spend-gates` |
| Related files | host/PageToMovie.Api/Auth/UserContext.cs; Program.cs job start / project mutators |

## Problem

Identity defaults to spoofable X-User-Id or "local". Expensive endpoints (gen-scene, gen-batch, stage1, character variants, remux) are not JWT-gated. Admin endpoints check IsAdmin, but spend paths do not require auth. Capacity and cast gates limit accidental waste, but there is no hard spend cap — the cost ledger is observational only.

## Partial fix (2026-07-26)

As part of adding real terms-acceptance enforcement (`AuthGate.RequireTermsAcceptedAsync`, which composes
`RequireLogin`), `/api/jobs/gen-scene`, `/api/jobs/gen-batch`, `/api/jobs/stage1`, and `/api/jobs/stage2` now
require a real signed-in user (or admin) — they previously took no `IUserContext` at all. **Character variants
and remux endpoints are still ungated** — this issue stays open for those, plus the broader "hard spend cap"
ask below.

## Suggested fix

For multi-user: require JWT or a shared secret for job starts; optional per-user/project daily USD gate via CostReportService before submit. If single-operator LAN-only is intentional, document that assumption explicitly.

## Notes

Tracked from the PageToMovie.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.