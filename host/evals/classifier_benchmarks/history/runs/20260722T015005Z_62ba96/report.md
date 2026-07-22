# Benchmark run `20260722T015005Z_62ba96`

- **UTC:** 2026-07-22 01:50:05Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v1_product
- **Tasks:** species_kind
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| species_kind | `claude-haiku-4-5-20251001` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.882 | **AI** | 9697ms | curated |
| species_kind | `claude-fable-5` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.863 | **AI** | 15164ms | curated |

Per-sample details: `details.json` in this run folder.
