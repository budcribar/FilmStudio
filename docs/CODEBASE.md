# Page to Movie — Codebase map

Agent-oriented map of the running app. **Start with** [`AGENTS.project.md`](../AGENTS.project.md) for rules; this file is the deep inventory.

**Stack:** React 19 · TanStack Start/Router · Vite 8 · Tailwind v4 · Zustand · Postgres/PGLite · Better Auth (lib present; PTM uses soft `ptmAuthMiddleware`)

---

## Top-level

| Path | Purpose |
|------|---------|
| `AGENTS.md` | Sandbox / platform contract (8080, startup, no `.env`) |
| `AGENTS.project.md` | **Project rules for agents** (read first) |
| `docs/` | This file, `SERVER_DB.md`, `INTEGRATION_PLAN.md` |
| `migrations/` | `0001_auth`, `0002_ptm_projects`, `0003_ptm_settings` |
| `scripts/` | `migrate.mjs`, `browser-smoke.mjs`, `smoke-wizard.mjs` |
| `src/` | Application source |
| `startup.sh` | Idempotent revive of dev server |
| `screenshots/` | Agent QA images |

---

## Routes

| File | URL | Role |
|------|-----|------|
| `src/routes/__root.tsx` | shell | AuthProvider + AppShell |
| `src/routes/index.tsx` | `/` | Home, resume cards, classics CTAs |
| `src/routes/demo.tsx` | `/demo` | Public gallery |
| `src/routes/projects.tsx` | `/projects` | Server-backed project list |
| `src/routes/settings.tsx` | `/settings` | Models + API keys + voice runtime |
| `src/routes/studio/index.tsx` | `/studio` | Book pick (classic / custom file) |
| `src/routes/studio/$projectId.tsx` | `/studio/$projectId` | Wizard cast→…→generate |

Nav: `src/components/layout/shell.tsx` — hydrates projects on mount.

---

## Domain modules (`src/lib/ptm`)

| Module | Role |
|--------|------|
| `store.ts` | Zustand: create/hydrate/save projects; generate sample/full; voice prep |
| `types.ts` | `FilmProject`, wizard/status, resume labels |
| `characters.ts` | Cast model + classic/custom helpers |
| `estimate.ts` | Credit/runtime quote |
| `voice.ts` | Voice add-on samples + credit extras |
| `wallet.ts` | Demo credits (localStorage) |
| `extract-source.ts` | PDF (pdfjs) / text import |
| `capture/audio-capture.ts` | Mic/upload → client media |
| `media/client-media-store.ts` | IndexedDB `ptm-client-media` |
| `media/client-stitch.ts` | Audio concat (mock-concat today) |
| `media/mock-mp3.ts` | Synthetic MP3 for demos |
| `models/catalog.ts` | Single catalog loader |
| `models/voice-models.ts` | Voice filter helpers |
| `providers/voice-clone.ts` | Client orchestration; live via server |
| `server/api.ts` | Projects CRUD + locks |
| `server/projects-repo.ts` | SQL |
| `server/hydrate.ts` / `sync-map.ts` | DTO ↔ rows |
| `server/ptm-auth.ts` | Soft auth middleware |
| `server/settings-api.ts` / `settings-repo.ts` | Prefs + secrets |
| `server/voice-api.ts` / `elevenlabs.ts` | Live voice proxy |

### Happy path

```
UI → useProjects → saveMyProject (debounced)
                 → prepareVoiceClones → runVoicePipeline
                      → IndexedDB capture
                      → mock MP3 OR server clone/TTS → IndexedDB line MP3s
                      → stitch → stitchedVoMediaId
                 → simulatePipeline + wallet.spend
                 → persist status / unlockedShots
```

`forServer()` strips `photoDataUrl` and blobs; only media ids cross the wire.

---

## Media (client)

| Item | Value |
|------|--------|
| DB | `ptm-client-media` v1 store `blobs` |
| Ref shape | `MediaRef` id + mime + size (+ optional role/projectId) |
| Capture limits | ~2–20s, max ~8MB (see `audio-capture.ts`) |
| Stitch | `method: "mock-concat"` until FFmpeg.wasm is added |

---

## Database

See [`SERVER_DB.md`](SERVER_DB.md).

| Migration | Tables |
|-----------|--------|
| 0001 | Better Auth |
| 0002 | `ptm_projects`, `ptm_scenes`, `ptm_cast`, `ptm_voice_samples`, `ptm_project_locks`, `ptm_wallets`, `ptm_credit_ledger` |
| 0003 | `ptm_secrets`, `ptm_provider_prefs` |

Scene IDs: `${projectId}__${shotId}`.

---

## Models catalog

**File:** `src/data/models/models.json` only.

```json
{
  "defaults": { "voice": "mock-instant-clone", "video": null, "chat": null, ... },
  "models": [
    { "id": "...", "capability": "voice", "providerId": "mock|elevenlabs|...", ... }
  ]
}
```

Current voice models: `mock-instant-clone`, `elevenlabs-instant-ivc` (`ELEVENLABS_API_KEY`).

Secrets resolve: user DB row → `process.env[KEY]` → none. Client sees masks only.

---

## Wizard & classics

Classics in `src/data/classics.ts`: `tell-tale-heart`, `alice`, `romeo` (cached screenplay/shots/characters).

Deep link: `/studio?classic=alice`.

Wizard steps in `types.ts` + `$projectId.tsx`. Generate is simulated picture lock + real/mock voice.

---

## Scripts

| Command | Use |
|---------|-----|
| `npm run dev` | `0.0.0.0:8080` |
| `npm run typecheck` | tsc |
| `npm run build` | Vite + migrate |
| `node scripts/smoke-wizard.mjs` | Wizard E2E |
| `node scripts/browser-smoke.mjs` | Generic page smoke + PNG |

---

## Architecture sketch

```
┌── CLIENT ──────────────────────────────────────┐
│ Shell → hydrateFromServer                        │
│ useProjects · useWallet                          │
│ IndexedDB blobs · voice-clone · stitch           │
└──────────────┬───────────────────▲───────────────┘
               │ server fns         │ FilmProject DTO
               ▼                    │ (ids only)
┌── SERVER ──────────────────────────────────────┐
│ ptmAuthMiddleware                                │
│ api · settings-api · voice-api                   │
│ PGLite (preview) / Neon (DATABASE_URL)           │
└──────────────────────────────────────────────────┘
```
