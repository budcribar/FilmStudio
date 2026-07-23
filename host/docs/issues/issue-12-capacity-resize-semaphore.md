# Issue 12 — Capacity resize disposes live SemaphoreSlim under load

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-12-capacity-resize-semaphore` |
| Related files | host/FilmStudio.Engine/WorkerPools.cs (~104-118, 172-183) |

## Problem

Capacity resize disposes the live SemaphoreSlim and replaces it. In-flight work may overshoot caps briefly, and waiters on the disposed semaphore can fault. Rare (admin config change under load) but multi-user-relevant.

## Suggested fix

Drain-then-replace, or only allow capacity changes when InFlight == 0.

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.