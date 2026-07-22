# Benchmark run `20260722T022355Z_c5b399`

- **UTC:** 2026-07-22 02:23:55Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v1_product
- **Tasks:** species_kind
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| species_kind | `grok-4.5` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.863 | **AI** | 26948ms | curated |
| species_kind | `claude-sonnet-5` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.882 | **AI** | 15061ms | curated |
| species_kind | `claude-haiku-4-5-20251001` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.882 | **AI** | 5973ms | curated |
| species_kind | `claude-fable-5` | `v1_product` | 0 | accuracy | 51 | 0.490 | 0.882 | **AI** | 16168ms | curated |

Per-sample details: `details.json` in this run folder.
