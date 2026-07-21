# Benchmark run `20260721T210915Z_acb202`

- **UTC:** 2026-07-21 21:09:15Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5
- **Prompts:** v1_product, v1_no_speech_sfx, v2_grounded
- **Tasks:** ambient_sfx
- **Note:** v2_grounded + temp matrix; matched-color baseline chart

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| ambient_sfx | `grok-4.5` | `v1_product` | 0 | mean_token_jaccard | 30 | 0.742 | 0.850 | **AI** | 29072ms | curated |
| ambient_sfx | `grok-4.5` | `v1_product` | 0.2 | mean_token_jaccard | 30 | 0.742 | 0.761 | **tie** | 36552ms | curated |
| ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | 0 | mean_token_jaccard | 30 | 0.742 | 0.814 | **AI** | 37388ms | curated |
| ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | 0.2 | mean_token_jaccard | 30 | 0.742 | 0.853 | **AI** | 38499ms | curated |
| ambient_sfx | `grok-4.5` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.872 | **AI** | 37386ms | curated |
| ambient_sfx | `grok-4.5` | `v2_grounded` | 0.2 | mean_token_jaccard | 30 | 0.742 | 0.897 | **AI** | 33760ms | curated |

## Compare — `ambient_sfx` / `grok-4.5`

| Prompt | Temp | AI score | vs best | Winner vs baseline |
|--------|------|----------|---------|--------------------|
| `v2_grounded` | 0.2 | 0.897 | best | AI |
| `v2_grounded` | 0 | 0.872 | -0.025 | AI |
| `v1_no_speech_sfx` | 0.2 | 0.853 | -0.044 | AI |
| `v1_product` | 0 | 0.850 | -0.047 | AI |
| `v1_no_speech_sfx` | 0 | 0.814 | -0.083 | AI |
| `v1_product` | 0.2 | 0.761 | -0.136 | tie |

Per-sample details: `details.json` in this run folder.
