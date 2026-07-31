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
| 3 · Voice | Optional add-on; **mic** or **upload** (audio/video) | Done (simulated mic + file label) |
| 4 · Estimate | Runtime range, picture vs voice credit split, classic savings | Done (heuristic) |
| 5 · Confirm | Free sample scene + full generate (credits) | Done (simulated) |
| 6 · Movie | Play shots, re-render, edit cast/voice | Done (storyboard player) |
| Wallet | Credit packs (demo purchase) | Done (localStorage) |
| Shelf | Home resume, Projects list | Done |

**Non-goals of the demo:** real MP4 export, real payments, real face swap, real TTS/clone, cloud project sync.

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

## Phase 0 — Stabilize (this branch, 1–2 days)

- [x] Git: `main` snapshot of current UI  
- [x] Branch for integrations  
- [ ] Smoke checklist script: book → cast → skip voice → estimate → sample → full (with pack buy)  
- [ ] Persist schema version on Zustand (`version: 2`) + migrate old projects  
- [ ] Empty-state + error toasts when PDF extract fails  
- [ ] Mobile pass on cast/voice (390px) without horizontal overflow  

**Exit:** typecheck + browser smoke green; no UX change unless bugfix.

---

## Phase 1 — Real ingest & estimate accuracy

| Piece | Approach | UI impact |
|-------|----------|-----------|
| PDF/text | Keep `pdfjs` client-side or move to server for large files | Studio drop zone only |
| Classic catalog CMS | JSON/CDN list with `characters[]`, `shots[]`, `cached: true` | Studio cards |
| Estimate engine | Rules + optional LLM “page → scenes” for custom only | Estimate step numbers |

**Exit:** custom long PDF doesn’t crash; classic list can grow without code deploy.

---

## Phase 2 — Voice clone (optional add-on → real)

Already modeled: mic **or** upload, per-role, priced add-on.

| Task | Detail |
|------|--------|
| Capture | Real `MediaRecorder` for mic; upload audio/video → extract audio (ffmpeg server or client) |
| Provider | Instant clone API (e.g. ElevenLabs / Cartesia / Fish-class): 10–30s sample, no fixed script |
| Storage | Encrypted object storage; retain only with consent; delete-on-request |
| TTS | Synthesize screenplay lines per character → mix into timeline |
| Pricing | Keep `VOICE_ADDON_BASE + per role`; reconcile with provider unit cost |

**UI:** same Voice panel; swap simulate record for real recorder; show “clone ready” from job poll.

**Exit:** one character sample → sample scene audio in personal voice (or labeled fallback).

---

## Phase 3 — Face / identity on picture

Two tracks (can ship A first):

### A. Face swap on cached shots (classics) — preferred MVP

1. Generate or use stock shot plates (no personal face).  
2. Per personalized cast: call **video/image face-swap API** (Akool, PiAPI, VModel-class) with user photo.  
3. Stitch short clips → player / download.

Cheaper for classics: screenplay + board already cached.

### B. Identity-conditioned generation

Image-to-video / character-ref models per shot (higher cost, less “classic reuse”).

| Task | Detail |
|------|--------|
| Photo quality gate | Frontal, lighting, consent checkbox |
| Per-shot jobs | Queue + progress labels already in store |
| Multi-face | Map cast roles → face IDs in provider |
| Credits | +N per personalized face × shot (or flat “identity pack”) |

**UI:** Cast photos already exist; film player swaps in result URLs when jobs complete.

**Exit:** Alice classic + kid photo → sample shot shows kid’s face (even if short/low-res).

---

## Phase 4 — True “movie out”

| Piece | Detail |
|-------|--------|
| Timeline assemble | Shots + VO + optional BGM → MP4 (Remotion, ffmpeg server, or provider stitch) |
| Download / share | Export button on ready state |
| Free sample | First shot only, watermark optional |
| Re-render | Half-price path already in wallet logic — wire real re-job |

**UI:** keep shot player; add “Download MP4” when `status === ready`.

---

## Phase 5 — Accounts, payments, library

| Piece | Detail |
|-------|--------|
| Auth | Workspace already has auth scaffolding under `src/lib/auth` — gate Projects + wallet |
| Credits | Stripe (or similar) for real packs; keep demo packs behind flag |
| Cloud projects | Replace/augment Zustand persist with user-scoped DB |
| Gallery | Wire `demo` page to approved public renders |
| Moderatio | Block non-consensual celebrity/child misuse; age gate |

---

## Suggested file layout for integrations

```text
src/lib/ptm/
  store.ts              # orchestration only
  providers/
    face-swap.ts        # interface + demo + akool/piapi adapters
    voice-clone.ts      # interface + demo + real adapter
    tts.ts
    render.ts           # stitch / job status
  estimate.ts           # keep pure
  voice.ts / characters.ts
```

Server routes (TanStack Start server functions) hold secrets; client only polls job IDs.

---

## Risk register

| Risk | Mitigation |
|------|------------|
| Provider latency blanks UI | Always show progress labels; never block wizard navigation while generating |
| Credit mismatch | Charge only after job accepted; refund on provider fail |
| Breaking local projects | Schema version + normalize() migrations (already partially there) |
| Mobile mic permissions | Graceful fallback to upload-only |
| Minor / consent | Hard stop without guardian confirmation flag |

---

## Near-term implementation order (recommended)

1. **Phase 0** smoke + store versioning (no user-visible risk)  
2. **Real MediaRecorder + upload persistence** (Phase 2 capture only, still demo clone)  
3. **Face-swap adapter on sample shot** for one classic (Phase 3A)  
4. **Voice clone provider on one line of dialogue** (Phase 2 finish)  
5. **Stitch + download** (Phase 4)  
6. **Payments + auth** (Phase 5)

Each step should land as a small PR into this branch, then merge to `main` only when smoke still passes with providers **off**.

---

## Merge policy

- `main` = always-runnable demo (no required API keys).  
- Feature flags default **off**.  
- Do not rewrite wizard step IDs without a migrate path for `localStorage` key `page-to-movie-projects`.  
- Prefer additive UI (new buttons/badges) over reordering the funnel without product sign-off.
