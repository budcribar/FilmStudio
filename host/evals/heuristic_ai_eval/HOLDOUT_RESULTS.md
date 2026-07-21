# Holdout results — `The_Jungle_Book`

Generated: 2026-07-21 20:07:50Z  
**Ambient gold revised:** 2026-07-21 (see `host/evals/classifier_benchmarks`)

Gold notes:
- **Portraits:** 10 real locked refs under `assets/characters` (Mowgli, Baloo, Akela, Father Wolf, Mother Wolf, Tabaqui, Kaa, Narrator, Gray Brother, Buldeo). Bagheera / Shere Khan blocked by content moderation; replacements used. Remaining cast still mock Playwright cards (~110KB).
- **Plates rank eval:** ranks basenames (filename slug match). With named files (`character_<slug>_ref.png`), baseline and AI both hit recall@3 = 1.0 — expected tie; not a vision bake-off of plate pixels.
- **Species gold:** curated from cast (AI wins 82% vs 51%).
- **Ambient/SFX gold:** **curated** from blind rounds (30 samples). On curated gold AI wins (~0.79–0.80 vs baseline ~0.74). Canonical suite: `host/evals/classifier_benchmarks` (history + prompt/model matrix).
- Cast / extend gold still heuristic-proxy (baseline-advantaged) until curated.

| Task | Metric | Baseline heuristic | AI | Winner |
|------|--------|--------------------|----|--------|
| 1 Ambient/SFX (curated) | mean token Jaccard | ~0.74 | ~0.79–0.80 | **AI** |
| 1 Ambient/SFX (old proxy gold) | mean token Jaccard | 1.00 | 0.73 | baseline *(invalid self-score)* |
| 2 On-screen cast | mean set F1 | 1.00 | 0.86 | **baseline** *(proxy gold)* |
| 3 Extend/hard-cut | accuracy | 24/24 (100%) | 23/24 (96%) | **baseline** *(proxy gold)* |
| 4 Species kind | accuracy | 26/51 (51%) | 42/51 (82%) | **AI** |
| 5 Plate rank | recall@3 | 1.00 | 1.00 | **tie** |

## Product wiring
| Task | Service | Stage2 / plates |
|------|---------|-----------------|
| Ambient/SFX | `AmbientSfxClassifier` | Stage2 enrich |
| On-screen cast | `OnScreenCastClassifier` | Stage2 enrich → clip cast |
| Extend/cut | `ExtendCutClassifier` | Stage2 `cut_decision` → ForceNone |
| Species | `SpeciesKindClassifier` | Stage2 seed field `species_kind` |
| Plate rank | `PlateRankClassifier` | CharacterBookPlateService re-rank |

Policy for all: **AI preferred → retry → heuristic fallback** (not when AI merely disagrees).
