# 05 — Book plate ranking fill

## Status: **SHIPPED** + mock-plate eval on The_Jungle_Book

## Product
- Class: `PlateRankClassifier`
- Used from `CharacterBookPlateService.HeuristicPicksRankedAsync`

## Mock plates (Playwright)
```bash
cd host/playwright
npm install && npx playwright install chromium
npm run jungle-plates   # node make-jungle-plates.mjs
```
Writes:
- `projects/The_Jungle_Book/source/book_images/` (22 PNGs)
- `projects/The_Jungle_Book/assets/characters/` (49 PNGs)

**Note:** These are HTML screenshot “book plates” (name + description cards), not illustrated PDF pages. Useful for ranking/filename eval only.

## Holdout The_Jungle_Book (2026-07-21)
| Metric | Baseline | AI |
|--------|----------|-----|
| mean recall@3 (12 cast) | 1.00 | 1.00 |

**Winner: tie** — gold files are named with character slugs, so filename heuristic is perfect. AI matches but does not beat.

Harder eval would need ambiguous filenames (page_03.png without names).
