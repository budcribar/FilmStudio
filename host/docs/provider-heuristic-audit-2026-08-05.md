# Provider-ID heuristic audit (should be model capabilities)

**Branch:** UITestingBranch · **Date:** 2026-08-05

Principle: **feature affordances** (can sing, can continue, max refs) belong on the **model catalog entry**. **Provider id** is for routing (which HTTP client / which API key), not for “does this model support X?”

---

## P0 — Feature gated by provider family (fix)

### 1. Audio “CanSing” / vocals

**There is no `supportsVocals` (or similar) in `models_catalog.json`.**

| Location | Heuristic |
|----------|-----------|
| `Scenes.razor` `SelectedAudioModelCanSing` | `Provider is "suno" or "aimusicapi" or "elevenlabs"` |
| `FilmJobService.RunSceneMusicGenAsync` | `entry.Provider is Suno or AiMusicApi or ElevenLabs` |
| `FakeAudioClient.ValidateVocalRequest` | same provider id / id-prefix list |

**Why wrong:** A new Fal or other model that *can* sing would stay disabled until code changes. A future Suno instrumental-only model would incorrectly get the Sing toggle.

**Fix:** Add catalog field e.g. `supportsVocals: true|false` on Audio models; map to `SupportedModelEntry.SupportsVocals`; UI + FilmJobService + FakeAudioClient read **only** that flag.

Suggested catalog values:

| Model | supportsVocals |
|-------|----------------|
| suno-v5-5, aimusicapi-suno, elevenlabs-music | true |
| fal-ai/musicgen, udio, minimax/music, stable-audio-2.0 | false |

---

## P1 — Provider fallback when catalog should be enough

### 2. `ImageApiLimits.MaxReferenceImages`

Prefers `entry.MaxReferenceImages` when set, then **falls back** to hard-coded provider constants:

- Grok → 3  
- Gemini/Google → 14  
- else → 3  

**Why weak:** Caps live in two places; a catalog model with missing `maxReferenceImages` silently gets provider defaults. GeminiImageClient already **requires** catalog max (fail loud).

**Fix:** Require `maxReferenceImages` on every enabled Image model in catalog; remove provider switch (or keep only as last-resort with a logged warning in tests that all image models have the field).

### 3. `ImageApiLimits` constants still used as product truth

`GrokMaxReferenceImages` / `GeminiMaxReferenceImages` encode capability outside the catalog. Prefer catalog-only once all image rows are complete.

---

## OK — Provider used for routing (not feature capability)

These are appropriate uses of provider id / family:

| Area | Why OK |
|------|--------|
| `MultiProviderVideoClient` / Vision / Chat | Choose which implementation to call |
| `VoiceApply/*Strategy` | Strategy matches provider implementation |
| `UserContext` / API key resolution | Keys are per provider |
| `Configuration.razor` key save → `EnsureMusicModelForProvider` | Picking a default model after adding a key (UX), not hiding a feature flag |
| `EnsureVideoProviderConfiguredAsync` env keys | Provider credential requirements from catalog `requiredEnvKeys` |

Video **continue / refs / duration** already use catalog flags (`supportsVideoContinue`, `maxReferenceImages`, duration fields) — including under fakes after `ValidateAgainstCatalog`.

---

## Gap summary

| Capability | Catalog field today? | Gated by provider heuristic? |
|------------|----------------------|------------------------------|
| Video continue | `supportsVideoContinue` | No (good) |
| Video max refs | `maxReferenceImages` | No (good) |
| Video duration | min/max/allowed | No (good) |
| Image max refs | `maxReferenceImages` (optional) | **Yes — fallback** |
| **Audio vocals / sing** | **Missing** | **Yes — only heuristic** |
| Voice clone vs TTS | `isVoiceCloneStep` | No (good) |

---

## Recommended work order

1. Add `supportsVocals` to catalog + `SupportedModelEntry` + JSON load/save  
2. Wire Scenes UI, FilmJobService, FakeAudioClient to the flag only  
3. Unit tests: CanSing true/false per model id, not provider  
4. Tighten ImageApiLimits to catalog-only (fail or test-guard missing max refs)  
