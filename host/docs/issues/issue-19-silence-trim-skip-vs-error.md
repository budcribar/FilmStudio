# Issue 19 — Silence-trim "skip" message doesn't distinguish no-op from failure

| Field | Value |
|-------|-------|
| Severity | nit |
| Status | open |
| Branch | `fix/issue-19-silence-trim-skip-vs-error` |
| Related files | `host/PageToMovie.Web/Services/ClientMediaFolderService.cs:189-247`; `host/PageToMovie.Web/wwwroot/js/pagetomovie-ffmpeg.js` (`analyzeSilenceAsync`, `encodeSliceAsync`) |

## Problem

`SilenceTrimAsync` returns the same `"skip: ..."` message shape whether there was genuinely nothing to trim (clip too short, no silence found — lines 204, 232) or something actually broke (ffmpeg.wasm load/SRI failure, exec exception — lines 202, 240, 246). A save never fails because of this (by design — trimming is best-effort), but if the CDN SRI hash ever mismatches or ffmpeg.wasm starts crashing for everyone, every save will silently report "skip: ..." indistinguishable from the normal "nothing to trim" case. `LastStatus` in `ClientMediaFolderService` is the only place this surfaces, and nothing aggregates or alerts on it.

## Suggested fix

Add a `skipReason` field (`"no-op"` vs `"error"`) to the tuple/JS results so an admin surface (or just a console warning threshold) can tell "nothing to trim today" apart from "trimming is broken."

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25), after moving silence-trim decision logic from JS into `ClipSilenceTrimmer` (Core).
