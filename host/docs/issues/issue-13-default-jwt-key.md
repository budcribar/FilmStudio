# Issue 13 — Default JWT signing key committed for production use

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-13-default-jwt-key` |
| Related files | host/FilmStudio.Core/Options/FilmStudioOptions.cs; host/FilmStudio.Api/appsettings.json |

## Problem

Default JWT signing key is a committed dev constant (FilmStudio-Dev-Only-Change-Me-32chars!!). Production without FILMSTUDIO_JWT_KEY accepts forged admin tokens if the key is known.

## Suggested fix

Refuse to start with the default key outside Development; require an env override.

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.