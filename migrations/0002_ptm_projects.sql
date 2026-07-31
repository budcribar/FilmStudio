-- Page to Movie domain schema (server of record for project / scene / lock state).
--
-- Binaries (MP3/MP4/capture) stay on the CLIENT media store (IndexedDB).
-- This DB holds structured project data + lock flags + media *refs* (ids only).
--
-- user_id is TEXT (matches Better Auth / preview 'dev-user'), not UUID.

-- ---------------------------------------------------------------------------
-- Projects
-- ---------------------------------------------------------------------------
create table if not exists ptm_projects (
  id              text primary key,
  user_id         text not null,
  title           text not null,
  author          text not null default '',
  genre           text not null default '',
  source_kind     text not null check (source_kind in ('classic', 'custom')),
  classic_id      text,
  source_text     text not null default '',
  screenplay      text not null default '',

  -- Pipeline position
  stage           text not null default 'source'
                    check (stage in ('source', 'screenplay', 'storyboard', 'film')),
  status          text not null default 'setup'
                    check (status in ('setup', 'sample', 'generating', 'ready')),
  wizard_step     text not null default 'cast'
                    check (wizard_step in ('cast', 'voice', 'estimate', 'confirm', 'done')),
  progress        integer not null default 0,
  progress_label  text not null default '',
  unlocked_shots  integer not null default 0,
  stars           integer not null default 0,
  casting_confirmed boolean not null default false,

  -- Stage / content locks (product freezes — not the same as edit sessions)
  screenplay_locked  boolean not null default false,
  cast_locked        boolean not null default false,
  voice_locked       boolean not null default false,
  estimate_locked    boolean not null default false,
  picture_locked     boolean not null default false,
  -- True once user accepts estimate / spends credits path
  generation_locked  boolean not null default false,

  -- Estimate snapshot (JSON) — small, queryable metadata
  estimate_json   jsonb,
  voice_json      jsonb not null default '{}'::jsonb,

  -- Client media refs only (no blobs)
  stitched_vo_media_id text,
  output_media_id      text,

  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now()
);

create index if not exists ptm_projects_user_id_idx on ptm_projects (user_id);
create index if not exists ptm_projects_user_updated_idx on ptm_projects (user_id, updated_at desc);
create index if not exists ptm_projects_status_idx on ptm_projects (status);

-- ---------------------------------------------------------------------------
-- Scenes / storyboard shots (ordered per project)
-- ---------------------------------------------------------------------------
create table if not exists ptm_scenes (
  id              text primary key,
  project_id      text not null references ptm_projects (id) on delete cascade,
  scene_number    integer not null,
  heading         text not null default '',
  visual          text not null default '',
  dialogue        text,
  duration_sec    integer not null default 5,
  palette         text,
  -- Client media for rendered plate / face-swapped clip
  plate_media_id  text,
  render_media_id text,
  -- Per-scene lock: free sample may unlock scene 1 only
  locked          boolean not null default true,
  sort_order      integer not null default 0,
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now(),
  unique (project_id, scene_number)
);

create index if not exists ptm_scenes_project_idx on ptm_scenes (project_id, sort_order);

-- ---------------------------------------------------------------------------
-- Cast
-- ---------------------------------------------------------------------------
create table if not exists ptm_cast (
  id              text primary key,
  project_id      text not null references ptm_projects (id) on delete cascade,
  role_in_story   text not null,
  display_name    text not null default '',
  relation        text not null default 'custom',
  selected        boolean not null default false,
  notes           text,
  -- Client-side photo (data URL was demo-only; prefer media ref)
  photo_media_id  text,
  sort_order      integer not null default 0,
  created_at      timestamptz not null default now(),
  updated_at      timestamptz not null default now()
);

create index if not exists ptm_cast_project_idx on ptm_cast (project_id, sort_order);

-- ---------------------------------------------------------------------------
-- Voice sample slots (metadata + client media ids)
-- ---------------------------------------------------------------------------
create table if not exists ptm_voice_samples (
  id                 text primary key,
  project_id         text not null references ptm_projects (id) on delete cascade,
  cast_id            text not null references ptm_cast (id) on delete cascade,
  enabled            boolean not null default false,
  has_sample         boolean not null default false,
  consent            boolean not null default false,
  source             text check (source is null or source in ('mic', 'upload')),
  sample_label       text,
  capture_media_id   text,
  clone_output_media_id text,
  line_media_id      text,
  model_id           text default 'mock-instant-clone',
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  unique (project_id, cast_id)
);

create index if not exists ptm_voice_samples_project_idx on ptm_voice_samples (project_id);

-- ---------------------------------------------------------------------------
-- Project edit locks (session / concurrency)
-- e.g. "Walker is editing cast" — soft lease with expiry
-- ---------------------------------------------------------------------------
create table if not exists ptm_project_locks (
  id              text primary key,
  project_id      text not null references ptm_projects (id) on delete cascade,
  user_id         text not null,
  -- What is locked: project | screenplay | cast | voice | estimate | generate
  lock_kind       text not null default 'project'
                    check (lock_kind in (
                      'project', 'screenplay', 'cast', 'voice',
                      'estimate', 'generate', 'render'
                    )),
  -- Human-readable holder (optional display name)
  holder_label    text,
  acquired_at     timestamptz not null default now(),
  expires_at      timestamptz not null,
  -- Soft metadata (tab id, device)
  client_token    text,
  unique (project_id, lock_kind)
);

create index if not exists ptm_project_locks_project_idx on ptm_project_locks (project_id);
create index if not exists ptm_project_locks_expires_idx on ptm_project_locks (expires_at);

-- ---------------------------------------------------------------------------
-- Wallet / credits (server-side; client demo wallet can mirror later)
-- ---------------------------------------------------------------------------
create table if not exists ptm_wallets (
  user_id         text primary key,
  credits         integer not null default 0,
  updated_at      timestamptz not null default now()
);

create table if not exists ptm_credit_ledger (
  id              text primary key,
  user_id         text not null,
  project_id      text references ptm_projects (id) on delete set null,
  delta           integer not null,
  reason          text not null,
  created_at      timestamptz not null default now()
);

create index if not exists ptm_credit_ledger_user_idx on ptm_credit_ledger (user_id, created_at desc);
