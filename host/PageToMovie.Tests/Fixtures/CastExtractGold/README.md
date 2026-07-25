# Cast extract gold corpus

Offline fixtures for **cast source coverage** (not regex name inventing).

## Policy

**Cast membership is decided by the model** reading Fountain + book.  
We do **not** guess Character_* keys with ALL-CAPS / proper-name heuristics (that created Kitchen/Backyard “cast”).

Offline CI checks:

1. Required heroes are **mentioned** in book and/or fountain (model can see them).
2. Book prompt sampling uses full text or spine samples — **no** forced name-hint list.
3. Look enrichment only fills stubs for keys the model already returned — never adds cast.

Live Grok coverage: `LiveApi/CastExtractLiveApiTests` (real extract).

## What each case contains

| File | Purpose |
|------|---------|
| `expected_keys.json` | Required `Character_*` keys for source-mention checks; optional `forbidden_key_substrings` |
| `book.txt` | Book excerpt (names + looks) |
| `screenplay.fountain` | Optional local Fountain; else `fountain_from_package` points at BookToFountainPackage |
