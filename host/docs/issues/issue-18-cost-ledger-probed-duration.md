# Issue 18 — Cost ledger records requested duration not probed length

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-18-cost-ledger-probed-duration` |
| Related files | host/FilmStudio.Engine/FilmJobService.cs (RecordVideoGenerationAsync); ClipDurationEstimator |

## Problem

Estimated/API-requested duration is used both for the video API duration and the cost ledger. Post-silence-trim actual length often differs; the ledger records requested duration, not the probed final length (sidecar is updated, cost is not). Minor cost-report drift.

## Suggested fix

After silence trim + sidecar write, record probed seconds in the cost event when available.

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.