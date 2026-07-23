# Issue 15 — SupportedModelCatalog comments lag multi-provider wiring

| Field | Value |
|-------|-------|
| Severity | nit |
| Status | open |
| Branch | `fix/issue-15-catalog-stale-comments` |
| Related files | host/FilmStudio.Core/Models/SupportedModelCatalog.cs (ModelProviderFamily + Google/Anthropic constants) |

## Problem

Comments still say Google/Anthropic "reserved; not fully wired" / "No client wired yet" while Gemini*Client / AnthropicChatClient and multi-provider dispatch exist. Stale docs mislead operators and agents.

## Suggested fix

Update catalog comments to match actual wiring and remaining gaps (OCR vision Grok-only, Veo no extend / no refs).

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.