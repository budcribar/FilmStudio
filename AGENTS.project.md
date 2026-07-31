# Page to Movie — Project instructions for agents

Read this **before** adding features. Extend existing architecture; do **not** invent parallel systems.

**Product:** AI film studio — drop PDF/text (or pick a classic) → cast → optional voice → estimate → generate movie.  
**North Star:** drop a file, get a movie out; edit afterward.  
**Branch:** `feature/page-to-movie-integrations`  
**Deep map:** [`docs/CODEBASE.md`](docs/CODEBASE.md) · DB: [`docs/SERVER_DB.md`](docs/SERVER_DB.md) · Roadmap: [`docs/INTEGRATION_PLAN.md`](docs/INTEGRATION_PLAN.md)

---

## Non-negotiable architecture

| Concern | Source of truth | Never |
|---------|-----------------|-------|
| Project / scene / cast / voice **metadata** | **Server** Postgres (PGLite in preview) | Client-only project JSON as SoT |
| MP3 / MP4 / capture **binaries** | **Client** IndexedDB (`ptm-client-media`) | Server blob storage / base64 in Zustand |
| Model catalog (voice, video, chat, …) | **One file** `src/data/models/models.json` | Parallel `*-models.json` files |
| API keys | Server `ptm_secrets` or env (`resolveSecret`: DB → env) | Client localStorage / models.json |
| Credits (today) | Client `wallet.ts` (demo) | Assume server ledger is live (tables exist, unwired) |
| Auth for PTM server fns | `ptmAuthMiddleware` | Strict `authMiddleware` for anonymous preview |

### Client vs server (short)

```
Client:  UI · Zustand store · IndexedDB blobs · wallet · FFmpeg/stitch (mock today)
Server:  projects, scenes, cast, voice rows, locks, prefs, secrets · ElevenLabs proxy
Wire:    createServerFn + media *ids* only in DTOs
```

---

## Layout (where things live)

| Path | Role |
|------|------|
| `src/routes/` | File routes: `/`, `/demo`, `/projects`, `/settings`, `/studio`, `/studio/$projectId` |
| `src/components/` | `layout/shell`, `casting-panel`, `voice-panel`, `credits-dialog`, `ui/*` |
| `src/data/classics.ts` | Cached classics + public demos |
| `src/data/models/models.json` | **Single** AI model catalog (`capability` field) |
| `src/lib/ptm/store.ts` | Client project store; hydrates/saves via server API |
| `src/lib/ptm/types.ts` | `FilmProject`, wizard steps, resume helpers |
| `src/lib/ptm/media/` | Client media store + stitch + mock MP3 |
| `src/lib/ptm/capture/` | Mic / upload → client media refs |
| `src/lib/ptm/providers/voice-clone.ts` | Orchestrates mock or live clone/TTS |
| `src/lib/ptm/models/catalog.ts` | Load/filter `models.json` |
| `src/lib/ptm/server/api.ts` | Project list/get/save/delete + edit locks |
| `src/lib/ptm/server/settings-api.ts` | Catalog, prefs, secrets |
| `src/lib/ptm/server/voice-api.ts` | Clone/TTS server proxies |
| `src/lib/ptm/server/*-repo.ts` | SQL |
| `migrations/0001_auth.sql` | Better Auth — **do not hand-edit** |
| `migrations/0002_ptm_projects.sql` | Projects, scenes, cast, voice, locks, wallet tables |
| `migrations/0003_ptm_settings.sql` | Secrets + provider prefs |
| `scripts/smoke-wizard.mjs` | E2E wizard smoke |
| `startup.sh` | Revive dev server on `:8080` |

---

## Product wizard (do not break)

```
/studio (book) → cast → voice? → estimate → confirm → generate → edit
```

1. **Book** — classic (cached, cheaper) or custom PDF/text  
2. **Cast** — personalize roles (child/spouse photo)  
3. **Voice** — optional; mic or upload; consent required  
4. **Estimate** — credit range  
5. **Confirm** — free sample or full generate  
6. **Edit** afterward  

`WizardStep`: `cast | voice | estimate | confirm | done`  
`ProjectStatus`: `setup | sample | generating | ready`

---

## Models & providers

- Catalog: `src/data/models/models.json` only.  
- Each model: `id`, `capability`, `providerId`, `apiKeyEnv`, pricing, …  
- Settings (`/settings`) picks capability → provider → model; keys server-side.  
- Voice generate: `resolveVoiceRuntime` → mock if no key.  
- **Add** providers as rows in the same JSON + server proxy (see `elevenlabs.ts`).  
- **Do not** create a second catalog file.

---

## Media pipeline

1. Capture → `putMediaBlobSafe` → `mediaId`  
2. Project rows store **ids only**  
3. Live TTS: client → server proxy → MP3 base64 → client store  
4. Stitch: `client-stitch.ts` (mock-concat; FFmpeg planned)  

Never put large base64 into `localStorage` or persisted Zustand.

---

## Server conventions

- PTM server fns: `ptmAuthMiddleware` → `context.userId`  
- Preview without Neon: `dev-user`  
- Scene PK: `${projectId}__${localShotId}`  
- Secrets: never return raw keys to client  

---

## When changing X, touch Y

| Change | Touch (in order) |
|--------|------------------|
| New project field | `types.ts` → migration → repo → hydrate/sync-map → store |
| New AI provider | `models.json` → Settings → server proxy → runtime |
| New route | `src/routes/*.tsx` (auto route tree) |
| Wizard UX | `studio/*`, casting/voice panels |
| Classic book | `src/data/classics.ts` |
| Credits | `estimate.ts`, `voice.ts`, `wallet.ts` |

---

## Agent gotchas

1. Bind **`0.0.0.0:8080`**; keep `startup.sh`.  
2. **No `.env` files.**  
3. **No** `src/routes/auth/popup.tsx`.  
4. Nitro only on **build**.  
5. Screenshots → `/workspace/screenshots/`.  
6. Generate is partly **simulated**; voice can be live.  
7. Credits = client wallet demo for now.  
8. **Extend**, don’t re-scaffold or parallel systems.  
9. Verify with Playwright/smoke inside the sandbox.  

---

## Verify before “done”

```bash
npm run typecheck
node scripts/smoke-wizard.mjs http://127.0.0.1:8080
```
