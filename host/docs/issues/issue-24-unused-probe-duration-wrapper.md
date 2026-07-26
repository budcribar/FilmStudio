# Issue 24 — ClientVideoStitchService.ProbeDurationAsync has no callers

| Field | Value |
|-------|-------|
| Severity | nit |
| Status | open |
| Branch | `fix/issue-24-unused-probe-duration-wrapper` |
| Related files | `host/PageToMovie.Web/Services/ClientVideoStitchService.cs:162-174`; `host/PageToMovie.Web/wwwroot/js/pagetomovie-ffmpeg.js` (`probeDurationAsync`, `_probeDurationUnlockedAsync`) |

## Problem

`ClientVideoStitchService.ProbeDurationAsync` (and the JS `PageToMovieFfmpeg.probeDurationAsync` it wraps) has no call sites anywhere in the Razor pages — found while auditing the ffmpeg migration for unreachable code (same pattern as the `TrimAsync`/`extractTailAsync` dead code removed earlier). Unlike that case, `_probeDurationUnlockedAsync` (the underlying JS primitive) is still a plausible building block, not obviously abandoned mid-refactor.

## Suggested fix

Low priority. Either wire it up (e.g. a duration display before a clip is saved) or delete the public wrapper. Left as-is for now since it's a smaller/more ambiguous case than the earlier trim dead code and not worth a snap judgment.

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25).
