# Cast extract gold corpus

Release-gate fixtures for **closed cast membership** (who gets a `Character_*` seed).

## Failure mode we guard

Picture-book / title heroes (e.g. **Buster**) appear in book text and action lines but never as dialogue cues. A dialogue-biased model returns only Mom + Narrator; plate sort then attaches dog art to Mom.

## What each case contains

| File | Purpose |
|------|---------|
| `expected_keys.json` | Required `Character_*` keys after speaker-only model + deterministic backfill |
| `book.txt` | Book excerpt (names + looks) |
| `screenplay.fountain` | Optional local Fountain; else `fountain_from_package` points at BookToFountainPackage |

## How tests use them

For every case folder:

1. Build **speaker-only** seeds from Fountain character cues (simulates a broken model).
2. Run `CollectCastNameHints` + `EnsureSeedsForNameHints` (production backfill).
3. Assert every `required_keys` entry is covered.

No live Grok calls — free, deterministic CI.
