# Catalog update scan (2026-08-05)

Admin → Models catalog → **Scan for updates**

## Color legend
| Color | Status | Meaning |
|-------|--------|---------|
| **Green** | `unchanged` | Live probe found the value; matches catalog |
| **Yellow** | `not_found` / `error` | No probe, parse miss, missing API key, or fetch error |
| **Red** | `changed` | Live value differs from catalog |

## Actions
- **Accept live** — patches the draft table field with the live value (then **Save**)
- **Accept as LAB** — adds a discovered model with `labMode: true`

## Probes (P0 / P1)

### P0 — fal list prices
- `GET https://api.fal.ai/v1/models/pricing?endpoint_id=…`
- Auth: `Authorization: Key $FAL_KEY` (or `FAL_API_KEY`)
- Maps `unit_price` → `imageCostPerImage` or video base / per-sec fields
- Requires key; without it → yellow

### P1 — xAI pricing from docs
- Fetches model docs HTML on `docs.x.ai` (Imagine video/image, grok-4.x)
- Parses Input/Output `$/1M`, `$/image`, resolution `$/sec` tiers
- Still runs duration/ref probes for video capability pages

### Also
- OpenAI / xAI `GET /v1/models` when API keys present (id existence + new models)
- Other providers: yellow “no probe”

## Notes
- List prices only — not usage/invoice APIs
- Nested JSON fields (e.g. `videoCostPerSecondByResolution.720p`) may need Raw JSON edit after Accept if the simple patch is insufficient
