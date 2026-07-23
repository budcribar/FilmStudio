# Issue 14 — Cross-species style scrub rewrites toward human adult

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-14-species-scrubber-human-bias` |
| Related files | host/FilmStudio.Engine/CharacterVisualTextScrubber.cs (~103-109) |

## Problem

Cross-species "matching X CG look" rewrites always to human adult medium language. For animal-to-animal style matching this can force "human adult — not an animal" into animal seed prose (wrong medium/species). The logic is general but biased.

## Suggested fix

Rewrite to neutral shared-medium phrasing without assuming human (e.g. same stylized picture-book soft-3D medium as the film) unless age_band/description already indicates human.

## Notes

Tracked from the FilmStudio.Api / Core / Engine code review (2026-07). This branch documents the problem only; implementation is follow-up work on this branch.