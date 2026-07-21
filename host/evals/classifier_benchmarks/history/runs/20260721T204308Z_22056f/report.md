# Benchmark run `20260721T204308Z_22056f`

- **UTC:** 2026-07-21 20:43:08Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5
- **Prompts:** v1_product, v1_no_speech_sfx
- **Tasks:** ambient_sfx
- **Note:** curated ambient gold; AI declared winner from blind rounds

| Task | Model | Prompt | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|--------|---|----------|----|--------|---------|------|
| ambient_sfx | `grok-4.5` | `v1_product` | mean_token_jaccard | 30 | 0.742 | 0.786 | **AI** | 30970ms | curated |
| ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | mean_token_jaccard | 30 | 0.742 | 0.800 | **AI** | 37017ms | curated |

## Prompt compare — `ambient_sfx` / `grok-4.5`

| Prompt | AI score | vs best | Winner vs baseline |
|--------|----------|---------|--------------------|
| `v1_no_speech_sfx` | 0.800 | best | AI |
| `v1_product` | 0.786 | -0.014 | AI |

Per-sample details: `details.json` in this run folder.
