-- User/org provider configuration: API keys + selected voice model.
-- Secrets never leave the server unmasked.

create table if not exists ptm_secrets (
  id           text primary key,
  user_id      text not null,
  -- Logical name, e.g. ELEVENLABS_API_KEY, OPENAI_API_KEY
  key_name     text not null,
  -- Raw secret value (server-only). Prefer env inject in prod; DB for preview/config UI.
  key_value    text not null,
  -- Provider this key belongs to (elevenlabs, openai, …)
  provider_id  text not null default '',
  label        text,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now(),
  unique (user_id, key_name)
);

create index if not exists ptm_secrets_user_idx on ptm_secrets (user_id);

-- Per-user provider preferences (which model/provider is active)
create table if not exists ptm_provider_prefs (
  user_id              text primary key,
  voice_provider_id    text not null default 'mock',
  voice_model_id       text not null default 'mock-instant-clone',
  -- Optional free-form extras (temperature, language, …)
  extras_json          jsonb not null default '{}'::jsonb,
  updated_at           timestamptz not null default now()
);
