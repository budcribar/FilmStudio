import { getSql } from "@/lib/db";

export type SecretRow = {
  id: string;
  user_id: string;
  key_name: string;
  key_value: string;
  provider_id: string;
  label: string | null;
  created_at: string;
  updated_at: string;
};

export type ProviderPrefsRow = {
  user_id: string;
  voice_provider_id: string;
  voice_model_id: string;
  extras_json: unknown;
  updated_at: string;
};

function newId(prefix: string) {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`;
}

export function maskSecret(value: string): string {
  if (!value) return "";
  if (value.length <= 8) return "••••••••";
  return `${value.slice(0, 3)}••••${value.slice(-4)}`;
}

export async function listSecretsMeta(userId: string): Promise<
  Array<{
    keyName: string;
    providerId: string;
    label: string | null;
    configured: boolean;
    masked: string;
    updatedAt: string;
  }>
> {
  const sql = await getSql();
  const rows = await sql<SecretRow>`
    select * from ptm_secrets where user_id = ${userId} order by key_name
  `;
  return rows.map((r) => ({
    keyName: r.key_name,
    providerId: r.provider_id,
    label: r.label,
    configured: true,
    masked: maskSecret(r.key_value),
    updatedAt: String(r.updated_at),
  }));
}

export async function getSecretValue(
  userId: string,
  keyName: string,
): Promise<string | null> {
  const sql = await getSql();
  const rows = await sql<SecretRow>`
    select * from ptm_secrets
    where user_id = ${userId} and key_name = ${keyName}
    limit 1
  `;
  return rows[0]?.key_value ?? null;
}

/** DB secret first, then process.env, then null */
export async function resolveSecret(
  userId: string,
  keyName: string,
): Promise<{ value: string | null; source: "db" | "env" | "none" }> {
  const fromDb = await getSecretValue(userId, keyName);
  if (fromDb?.trim()) return { value: fromDb.trim(), source: "db" };
  const fromEnv =
    typeof process !== "undefined" ? process.env[keyName]?.trim() : undefined;
  if (fromEnv) return { value: fromEnv, source: "env" };
  return { value: null, source: "none" };
}

export async function upsertSecret(input: {
  userId: string;
  keyName: string;
  keyValue: string;
  providerId: string;
  label?: string;
}): Promise<void> {
  const sql = await getSql();
  const id = newId("sec");
  await sql`
    insert into ptm_secrets (
      id, user_id, key_name, key_value, provider_id, label, updated_at
    ) values (
      ${id},
      ${input.userId},
      ${input.keyName},
      ${input.keyValue},
      ${input.providerId},
      ${input.label ?? null},
      now()
    )
    on conflict (user_id, key_name) do update set
      key_value = excluded.key_value,
      provider_id = excluded.provider_id,
      label = excluded.label,
      updated_at = now()
  `;
}

export async function deleteSecret(userId: string, keyName: string): Promise<boolean> {
  const sql = await getSql();
  const rows = await sql<{ id: string }>`
    delete from ptm_secrets
    where user_id = ${userId} and key_name = ${keyName}
    returning id
  `;
  return rows.length > 0;
}

export async function getProviderPrefs(userId: string): Promise<ProviderPrefsRow | null> {
  const sql = await getSql();
  const rows = await sql<ProviderPrefsRow>`
    select * from ptm_provider_prefs where user_id = ${userId} limit 1
  `;
  return rows[0] ?? null;
}

export async function upsertProviderPrefs(input: {
  userId: string;
  voiceProviderId: string;
  voiceModelId: string;
  extras?: Record<string, unknown>;
}): Promise<ProviderPrefsRow> {
  const sql = await getSql();
  const rows = await sql<ProviderPrefsRow>`
    insert into ptm_provider_prefs (
      user_id, voice_provider_id, voice_model_id, extras_json, updated_at
    ) values (
      ${input.userId},
      ${input.voiceProviderId},
      ${input.voiceModelId},
      ${JSON.stringify(input.extras ?? {})},
      now()
    )
    on conflict (user_id) do update set
      voice_provider_id = excluded.voice_provider_id,
      voice_model_id = excluded.voice_model_id,
      extras_json = excluded.extras_json,
      updated_at = now()
    returning *
  `;
  return rows[0]!;
}
