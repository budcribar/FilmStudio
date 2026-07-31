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

1. **Book** — classic (cached screenplay/storyboard, cheaper) or custom PDF/text/phone file  
2. **Cast** — personalize roles (child/spouse photo); classics pre-fill characters  
3. **Voice** — optional paid add-on; mic **or** upload; consent required  
4. **Estimate** — credit range (classic cache discount + voice extras)  
5. **Confirm** — free sample scene **or** full generate with credits  
6. **Edit** afterward (locks exist for stage freezes)

`WizardStep`: `cast | voice | estimate | confirm | done`  
`ProjectStatus`: `setup | sample | generating | ready`

---

## Models & providers

- Catalog: `src/data/models/models.json` only.  
- Each model: `id`, `capability` (`voice` | `video` | `chat` | `image` | `face_swap`), `providerId`, pricing, `apiKeyEnv`, etc.  
- Settings UI (`/settings`) picks capability → provider → model; stores keys server-side.  
- Voice generate uses prefs via `resolveVoiceRuntime` → mock if no key / mock provider.  
- **Add** video/chat by new rows in the same JSON + server proxy pattern (`elevenlabs.ts` is the template).  
- **Do not** create `voice-models.json` or a second catalog.

---

## Media pipeline

1. Capture/upload → `putMediaBlobSafe` → `mediaId`  
2. Project/voice rows store **ids only**  
3. Live TTS: client sends sample bytes to server → provider → MP3 base64 → client store again  
4. Stitch: `client-stitch.ts` (currently mock byte-concat; real FFmpeg.wasm planned, not installed)  
5. Final cut intended to stay client-side (CPU/storage off server)

Never put large base64 into `localStorage` or persisted Zustand.

---

## Server conventions

- All PTM server fns: `ptmAuthMiddleware` → `context.userId`  
- Preview without Neon: shared `dev-user` so wizard works unsigned-in  
- Deployed Neon without session: fail closed  
- Scene PK: `${projectId}__${localShotId}` (classics reuse local shot ids) — see `sync-map.ts`  
- `saveMyProject` order: header → scenes → cast → voice (cast replace clears voice FK)  
- Secrets: never return raw `key_value` to client (use `listSecretsMeta` / mask)

---

## When changing X, touch Y

| Change | Touch (in order) |
|--------|------------------|
| New project field | `types.ts` → migration → `server/types` → `projects-repo` → `hydrate` / `sync-map` → `store` |
| New AI provider | `models.json` → Settings (auto) → `*-api` + provider module → wire runtime |
| New route | `src/routes/*.tsx` (dev regenerates `routeTree.gen.ts` — don’t hand-edit) |
| Wizard UX | `studio/index.tsx`, `studio/$projectId.tsx`, casting/voice panels |
| Classic book | `src/data/classics.ts` |
| Credits math | `estimate.ts`, `voice.ts`, `wallet.ts` |
| Nav / shell | `src/components/layout/shell.tsx` |

---

## Agent gotchas

1. Bind **`0.0.0.0:8080`** only; keep `startup.sh` correct.  
2. **Do not create `.env`** files; platform injects secrets.  
3. **Do not** create `src/routes/auth/popup.tsx` (Vite plugin owns it).  
4. Nitro only on **build**, not dev (breaks single-port preview).  
5. Screenshots → `/workspace/screenshots/`, not `/tmp`.  
6. Generate UI is partly **simulated** (storyboard frames); voice can be live.  
7. Server wallet tables exist but **client wallet** is live demo SoT for now.  
8. Prefer **edit in place** over re-scaffold; leave dev server up.  
9. Verify with Playwright/smoke inside sandbox — user has no terminal.  
10. Prefer **reuse** repos/APIs/catalog over new parallel modules.

---

## Verify before “done”

```bash
npm run typecheck
node scripts/smoke-wizard.mjs http://127.0.0.1:8080
# Settings + keys: open /settings in Playwright if touching providers
```

Leave `npm run dev` / `startup.sh` serving the preview.
