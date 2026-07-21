# Benchmark run `20260721T205232Z_9f5c1e`

- **UTC:** 2026-07-21 20:52:32Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5
- **Prompts:** v1_product
- **Tasks:** ambient_sfx, species_kind
- **Note:** full suite ambient+species

| Task | Model | Prompt | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|--------|---|----------|----|--------|---------|------|
| ambient_sfx | `grok-4.5` | `v1_product` | mean_token_jaccard | 30 | 0.742 | 0.739 | **tie** | 29964ms | curated |
| species_kind | `grok-4.5` | `v1_product` | accuracy | 51 | 0.490 | 0.863 | **AI** | 24306ms | curated |

Per-sample details: `details.json` in this run folder.
