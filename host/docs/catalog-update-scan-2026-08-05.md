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

## What is probed today
- **xAI video**: docs for max extension, max clip duration, max reference images
- **OpenAI / xAI chat**: model id present on `GET /v1/models` when API key is set
- **New models**: candidates from OpenAI/xAI model lists not already in the catalog
- Other providers: yellow “no probe” (manual review)

Extend `CatalogUpdateProbeService` when adding new vendor parsers.
