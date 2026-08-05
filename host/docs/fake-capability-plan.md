# Fake multi-capability plan (UITestingBranch)

Goal: several model profiles (catalog flags) under fakes; UI and jobs react correctly.

- [x] **1. Catalog-aware fake video client** — enforce continue / max refs / duration from `SupportedModelCatalog` for the requested `model` id (`FakeGrokVideoClient.ValidateAgainstCatalog`)
- [x] **2. Unit tests for video feature flags** — `FakeVideoCatalogCapabilityTests` (10/10): grok continue+refs, wan short, veo no-ref / allowed durations
- [ ] **3. Fake audio CanSing (if needed)** — honor catalog vocal flag for Scenes UI path
- [ ] **4. API combo smoke** — set project config to each video profile; stage2 bounds / gen accept-reject
- [ ] **5. UI combo checks** — Configuration lists by capability; Scenes/extend/refs reflect selected model (Playwright fakes)
- [ ] **6. Docs** — matrix of combos + how to run under UseFakes

## Profiles (existing catalog ids)

| Id | Continue | Max refs | Duration |
|----|----------|----------|----------|
| `grok-imagine-video` | yes | 7 | 1–15s |
| `fal-ai/wan-2.1` | no | 1 | 5–6s |
| `veo-3.1` | no | 0 | 4/6/8 only |
