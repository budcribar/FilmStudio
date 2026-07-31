# Server database (Page to Movie)

## Source of truth

| Data | Where |
|------|--------|
| **Projects, scenes, cast, voice metadata, locks, credits ledger** | **Server Postgres** (Neon / PGLite) |
| **MP3 / MP4 / capture binaries** | **Client only** (`ptm-client-media` IndexedDB) |
| Media references | Server columns `*_media_id` (string ids) |

The client Zustand store is an **in-memory cache** that hydrates from / saves to the server. It does **not** persist projects in `localStorage`.

## Schema

- `migrations/0001_auth.sql` — Better Auth  
- `migrations/0002_ptm_projects.sql` — domain tables  

## Locks

### Content locks (`ptm_projects` booleans)

`screenplay_locked`, `cast_locked`, `voice_locked`, `estimate_locked`, `picture_locked`, `generation_locked`

### Edit session locks (`ptm_project_locks`)

Soft TTL leases per `lock_kind` for concurrent editors.

## API

`src/lib/ptm/server/api.ts` — `listMyProjects`, `getMyProject`, `saveMyProject`, `deleteMyProject`, lock acquire/release.  
All use `authMiddleware` and scope by `context.userId`.

## Client flow

1. `AppShell` → `hydrateFromServer()`  
2. Mutations optimistic → debounced `saveMyProject`  
3. Generate flushes to server after voice mock pipeline updates media ids  
