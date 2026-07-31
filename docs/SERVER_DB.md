# Server database (Page to Movie)

## Source of truth

| Data | Where |
|------|--------|
| **Projects, scenes, cast, voice metadata, locks** | **Server Postgres** |
| **API keys + voice provider prefs** | **Server** `ptm_secrets`, `ptm_provider_prefs` |
| **MP3 / MP4 / capture binaries** | **Client IndexedDB only** |
| Media references | Server `*_media_id` columns |

## Migrations

| File | Contents |
|------|----------|
| `0001_auth.sql` | Better Auth |
| `0002_ptm_projects.sql` | Projects, scenes, cast, voice samples, locks, wallet tables |
| `0003_ptm_settings.sql` | Secrets + provider prefs |

## Settings (keys + dynamic provider)

| Piece | Path |
|-------|------|
| Repo | `src/lib/ptm/server/settings-repo.ts` |
| Server fns | `src/lib/ptm/server/settings-api.ts` |
| Voice proxy | `src/lib/ptm/server/voice-api.ts`, `elevenlabs.ts` |
| Catalog | `src/data/models/voice-models.json` (add providers here) |
| UI | `/settings` |

**Resolve order for keys:** DB row for user → `process.env[KEY_NAME]` → none.  
Client never receives raw secrets (masked only).

**Runtime:** `resolveVoiceRuntime(userId)` → prefs + key → ElevenLabs or mock.

## Projects API

`src/lib/ptm/server/api.ts` — list/get/save/delete + edit locks.  
Auth: `ptmAuthMiddleware` (session, or `dev-user` on PGLite preview).
