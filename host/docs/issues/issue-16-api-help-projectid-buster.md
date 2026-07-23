# Issue 16 — API help hardcodes projectId=Buster

| Field | Value |
|-------|-------|
| Severity | nit |
| Status | open |
| Branch | `fix/issue-16-api-help-projectid-buster` |
| Related files | host/FilmStudio.Api/Program.cs (error/help example text) |

## Problem

Error or help example hardcodes projectId=Buster in API surface text. Minor north-star consistency issue: product code should not use a sample title as the canonical example.

## Suggested fix

Use a generic example (projectId=MyStory).

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.