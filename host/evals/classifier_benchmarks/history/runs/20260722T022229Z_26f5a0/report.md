# Benchmark run `20260722T022229Z_26f5a0`

- **UTC:** 2026-07-22 02:22:29Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_product
- **Tasks:** silent_beat_action
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| silent_beat_action | `grok-4.5` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.850 | **AI** | 302372ms | curated |
| silent_beat_action | `claude-sonnet-5` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.769 | **AI** | 132943ms | curated |
| silent_beat_action | `claude-haiku-4-5-20251001` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.782 | **AI** | 63650ms | curated |
| silent_beat_action | `claude-fable-5` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.850 | **AI** | 164242ms | curated |

Per-sample details: `details.json` in this run folder.
