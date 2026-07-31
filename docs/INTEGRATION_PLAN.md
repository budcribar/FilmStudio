# Page to Movie — integration plan

Branch: `feature/page-to-movie-integrations`  
Baseline: `main` (UI wizard + simulated pipeline, local-only)

This document describes **what already ships in the UI** (do not regress) and a **phased plan** for real backends. Prefer thin adapters behind the existing store so the wizard UX stays stable.

---

## Already in the UI (keep working)

| Step | UI surface | Status |
|------|------------|--------|
| 1 · Book | Studio: classics (cached) + custom PDF/text/paste | Done (demo) |
| 2 · Cast | Toggle roles, name, relation, photo | Done (local) |
| 3 · Voice | Optional add-on; **mic** or **upload** + consent | **Capture real (Phase 2 partial)** |
| 4 · Estimate | Runtime range, picture vs voice credit split, classic savings | Done (heuristic) |
| 5 · Confirm | Free sample scene + full generate (credits) | Done (simulated) |
| 6 · Movie | Play shots, re-render, edit cast/voice | Done (storyboard player) |
| Wallet | Credit packs (demo purchase) | Done (localStorage) |
| Shelf | Home resume, Projects list | Done |

**Non-goals of the demo:** real MP4 export, real payments, real face swap, live TTS/clone provider, cloud project sync.

---

## Architecture guardrails (don’t break the UI)

1. **Wizard is the product contract**  
   `wizardStep`: `cast → voice → estimate → confirm → done`  
   Only add steps *between* existing ones if copy/estimate still make sense.

2. **Store is the single integration seam**  
   Put providers behind `src/lib/ptm/` adapters called from `store.ts` (`runFreeSample`, `runFullGenerate`, `runRerender`).  
   Routes/components should not call third-party APIs directly.

3. **Estimate stays honest**  
   Any real cost (face swap seconds, voice clone seats) must update `estimateProduction` / `buildEstimate` so Confirm still matches the charge.

4. **Feature-flag real providers**  
   Env vars e.g. `VITE_FACE_SWAP_PROVIDER`, `VOICE_CLONE_API_KEY` (server-only).  
   Demo mode remains the default when keys are missing.

5. **Consent first for identity**  
   Photos + voice of people (especially minors) need explicit consent UI before any upload to a provider.

---

## Phase 0 — Stabilize

- [x] Git: `main` snapshot of current UI  
- [x] Branch for integrations  
- [x] Smoke checklist script (`scripts/smoke-wizard.mjs`)  
- [x] Persist schema version on Zustand (`version: 3`) + migrate  
- [ ] Empty-state + error toasts when PDF extract fails  
- [ ] Mobile pass on cast/voice (390px) without horizontal overflow  

---

## Phase 1 — Real ingest & estimate accuracy

| Piece | Approach | UI impact |
|-------|----------|-----------|
| PDF/text | Keep `pdfjs` client-side or move to server for large files | Studio drop zone only |
| Classic catalog CMS | JSON/CDN list with `characters[]`, `shots[]`, `cached: true` | Studio cards |
| Estimate engine | Rules + optional LLM “page → scenes” for custom only | Estimate step numbers |

---

## Phase 2 — Voice (capture + clone)

### 2a Capture — **implemented on this branch**

| Piece | Location | Notes |
|-------|----------|--------|
| Mic | `src/lib/ptm/capture/audio-capture.ts` + `VoicePanel` | Real `MediaRecorder`, elapsed UI, stop/cancel, min length |
| Upload | same | Audio **or** video → `VoiceCaptureAsset` (data URL, duration, size) |
| Limits | `CAPTURE_MAX_BYTES` 3MB, min ~2s | Keeps localStorage roughly viable |
| Preview | Voice panel | Play/pause captured sample |
| Consent | per role checkbox | Required for `voiceRolesReady` |
| Persist | sample.`asset` on project voice | May drop huge payloads if quota exceeded |
| Provider seam | `src/lib/ptm/providers/voice-clone.ts` | Demo only; called from generate |

### 2b Clone / TTS — **not done**

| Task | Detail |
|------|--------|
| Provider | Instant clone API (ElevenLabs / Cartesia / Fish-class) |
| Storage | Encrypted object storage; not data URLs in localStorage |
| TTS | Synthesize screenplay lines per character → mix |
| Pricing | Reconcile `VOICE_*` credits with unit cost |

**Exit for 2b:** one character sample → sample scene audio in personal voice.

### Capture design notes

```
[Mic]  getUserMedia → MediaRecorder → Blob → dataURL → VoiceCaptureAsset
[Upload] File → validate mime/size → dataURL → probe duration → VoiceCaptureAsset
                    ↓
            VoiceSample { asset, consent, source }
                    ↓
         prepareVoiceClones() → VoiceCloneProvider (demo | live)
```

**Follow-ups before live provider:**
1. Move assets to IndexedDB or server upload (localStorage quota).  
2. Server route holds API keys; client only gets job ids.  
3. Strip video to audio server-side when mime is video/*.  
4. Optional scripted read for higher-quality clone tier.

---

## Phase 3 — Face / identity on picture

### A. Face swap on cached shots (classics) — preferred MVP

1. Stock shot plates (no personal face).  
2. Face-swap API with user photo.  
3. Stitch short clips → player / download.

### B. Identity-conditioned generation

Image-to-video / character-ref models per shot.

---

## Phase 4 — True “movie out”

Timeline assemble, download MP4, watermark free sample.

---

## Phase 5 — Accounts, payments, library

Auth scaffolding exists under `src/lib/auth`; Stripe + cloud projects later.

---

## Suggested file layout

```text
src/lib/ptm/
  capture/audio-capture.ts   # Phase 2a ✓
  providers/voice-clone.ts   # Phase 2b seam ✓ (demo)
  providers/face-swap.ts     # Phase 3
  providers/render.ts        # Phase 4
  store.ts
```

---

## Near-term order

1. ~~Phase 2a capture~~ **done**  
2. IndexedDB asset store (before large multi-role captures)  
3. Face-swap adapter on one classic sample shot (Phase 3A)  
4. Live voice clone provider (Phase 2b)  
5. Stitch + download (Phase 4)  
6. Payments + auth (Phase 5)

---

## Merge policy

- `main` = always-runnable demo (no required API keys).  
- Feature flags default **off**.  
- Do not rewrite wizard step IDs without a migrate path for `page-to-movie-projects`.  
- Prefer additive UI over reordering the funnel without product sign-off.
