# Issue 22 — Clip-auto-review frame payload only bounded after decode

| Field | Value |
|-------|-------|
| Severity | nit |
| Status | open |
| Branch | `fix/issue-22-clip-auto-review-body-size-limit` |
| Related files | `host/PageToMovie.Api/Program.cs:278` (`MaxRequestBodySize`), `:2924`, `:2945` (`/api/jobs/clip-auto-review`, `-batch`); `host/PageToMovie.Engine/ClipAutoReviewService.cs:581` (`MaterializeClientFrames`) |

## Problem

`MaterializeClientFrames` bounds frames to 8 count / 2.5MB each, but only *after* the JSON body is fully deserialized (and each frame's base64 fully decoded to bytes) — before that, the only ceiling is the app-wide `MaxRequestBodySize = 512MB` (`Program.cs:278`). A malformed or malicious request to this route can still make the server allocate well beyond what 8 real JPEG frames would ever need before the per-frame check discards the excess.

## Suggested fix

A tight `[RequestSizeLimit]` (a few MB is generous for 8 JPEGs at typical review-frame resolution) on the `clip-auto-review` and `clip-auto-review-batch` routes specifically, rather than relying on the global cap.

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25). Low risk for authenticated normal use; cheap insurance if addressed.
