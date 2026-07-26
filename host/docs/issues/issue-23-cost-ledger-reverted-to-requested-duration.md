# Issue 23 — Cost ledger silently reverted from probed to requested duration (regresses issue-18)

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-23-cost-ledger-reverted-to-requested-duration` |
| Related files | `host/PageToMovie.Engine/FilmJobService.cs:2596-2621` (cost recording), `:2710-2736` (`EnsureClipDurationSidecarAsync`, now unreachable) |

## Problem

Issue 18 (`issue-18-cost-ledger-probed-duration.md`) fixed the cost ledger to record **probed** clip length instead of requested/estimated duration, since silence trim can shorten the final file. The "server keeps hashes only, client owns the media folder" migration (`2db206e feat: client media folder for gen clips; server stores hashes only.`) removed the server-side file that made probing possible at that point in the flow — `FilmJobService.cs:2596-2597` now reads:

```csharp
// Cost uses requested duration (no server file to probe until client registers).
var costDurationSec = (double)duration;
```

This is a deliberate, commented trade-off, not an oversight in the moment — but it silently re-opens the exact gap issue-18 closed: silence-trimmed clips are once again costed at their pre-trim requested duration. `EnsureClipDurationSidecarAsync` (`FilmJobService.cs:2710`), the method issue-18's fix relied on, has zero remaining callers — it's dead code left over from the old flow.

## Suggested fix

The client now knows the final (possibly trimmed) duration at save time — `ClientMediaFolderService.SilenceTrimAsync` gets `afterSec`-equivalent info from `encodeSliceAsync`, and `MediaRegisterRequest` already carries `SizeBytes`/`Sha256` back to the server per clip. Consider adding a `durationSec` field to that same registration call so the server can correct/backfill the cost ledger entry once the client reports the real post-trim length, instead of leaving it permanently at the requested value. Either delete `EnsureClipDurationSidecarAsync` if this path won't be revived, or repurpose it for that backfill.

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25). Worth a deliberate decision rather than leaving it silently reverted — the original issue-18 motivation (silence trim shortens clips, cost should reflect that) still applies.
