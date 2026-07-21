# Classifier benchmarks — history

Updated: 2026-07-21 21:12:48Z

Open **`reports/history.html`** for interactive charts (model / prompt / task over time).

## Latest runs

| When (UTC) | Run | Task | Model | Prompt | Temp | Metric | Baseline | AI | Winner | n |
|------------|-----|------|-------|--------|------|--------|----------|----|--------|---|
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v1_product` | 0 | mean_token_jaccard | 0.742 | 0.850 | **AI** | 30 |
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v1_product` | 0.2 | mean_token_jaccard | 0.742 | 0.761 | **tie** | 30 |
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | 0 | mean_token_jaccard | 0.742 | 0.814 | **AI** | 30 |
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | 0.2 | mean_token_jaccard | 0.742 | 0.853 | **AI** | 30 |
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v2_grounded` | 0 | mean_token_jaccard | 0.742 | 0.872 | **AI** | 30 |
| 2026-07-21 21:09:15Z | `20260721T210915Z_acb202` | ambient_sfx | `grok-4.5` | `v2_grounded` | 0.2 | mean_token_jaccard | 0.742 | 0.897 | **AI** | 30 |
| 2026-07-21 20:52:32Z | `20260721T205232Z_9f5c1e` | ambient_sfx | `grok-4.5` | `v1_product` | 0 | mean_token_jaccard | 0.742 | 0.739 | **tie** | 30 |
| 2026-07-21 20:52:32Z | `20260721T205232Z_9f5c1e` | species_kind | `grok-4.5` | `v1_product` | 0 | accuracy | 0.490 | 0.863 | **AI** | 51 |
| 2026-07-21 20:44:53Z | `20260721T204453Z_dd5b72` | species_kind | `grok-4.5` | `v1_product` | 0 | accuracy | 0.490 | 0.863 | **AI** | 51 |
| 2026-07-21 20:43:08Z | `20260721T204308Z_22056f` | ambient_sfx | `grok-4.5` | `v1_product` | 0 | mean_token_jaccard | 0.742 | 0.786 | **AI** | 30 |
| 2026-07-21 20:43:08Z | `20260721T204308Z_22056f` | ambient_sfx | `grok-4.5` | `v1_no_speech_sfx` | 0 | mean_token_jaccard | 0.742 | 0.800 | **AI** | 30 |

## AI score trend by task (newest first)

### `ambient_sfx`

| Run | Model | Prompt | Temp | AI | Baseline |
|-----|-------|--------|------|----|----------|
| `20260721T210915Z_acb202` | `grok-4.5` | `v1_product` | 0 | 0.850 | 0.742 |
| `20260721T210915Z_acb202` | `grok-4.5` | `v1_product` | 0.2 | 0.761 | 0.742 |
| `20260721T210915Z_acb202` | `grok-4.5` | `v1_no_speech_sfx` | 0 | 0.814 | 0.742 |
| `20260721T210915Z_acb202` | `grok-4.5` | `v1_no_speech_sfx` | 0.2 | 0.853 | 0.742 |
| `20260721T210915Z_acb202` | `grok-4.5` | `v2_grounded` | 0 | 0.872 | 0.742 |
| `20260721T210915Z_acb202` | `grok-4.5` | `v2_grounded` | 0.2 | 0.897 | 0.742 |
| `20260721T205232Z_9f5c1e` | `grok-4.5` | `v1_product` | 0 | 0.739 | 0.742 |
| `20260721T204308Z_22056f` | `grok-4.5` | `v1_product` | 0 | 0.786 | 0.742 |
| `20260721T204308Z_22056f` | `grok-4.5` | `v1_no_speech_sfx` | 0 | 0.800 | 0.742 |

### `species_kind`

| Run | Model | Prompt | Temp | AI | Baseline |
|-----|-------|--------|------|----|----------|
| `20260721T205232Z_9f5c1e` | `grok-4.5` | `v1_product` | 0 | 0.863 | 0.490 |
| `20260721T204453Z_dd5b72` | `grok-4.5` | `v1_product` | 0 | 0.863 | 0.490 |
