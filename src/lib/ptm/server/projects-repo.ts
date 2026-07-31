/**
 * Server repository — projects / scenes / cast / voice metadata / locks.
 * Binaries never stored here — only media id strings.
 */
import { getSql } from "@/lib/db";
import type {
  DbCastRow,
  DbLockKind,
  DbProjectLockRow,
  DbProjectRow,
  DbSceneRow,
  DbVoiceSampleRow,
  ProjectContentLocks,
} from "./types";

function newId(prefix: string) {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}`;
}

export async function listProjectsForUser(userId: string): Promise<DbProjectRow[]> {
  const sql = await getSql();
  return sql<DbProjectRow>`
    select * from ptm_projects
    where user_id = ${userId}
    order by updated_at desc
  `;
}

export async function getProject(
  projectId: string,
  userId: string,
): Promise<DbProjectRow | null> {
  const sql = await getSql();
  const rows = await sql<DbProjectRow>`
    select * from ptm_projects
    where id = ${projectId} and user_id = ${userId}
    limit 1
  `;
  return rows[0] ?? null;
}

export async function listScenes(projectId: string): Promise<DbSceneRow[]> {
  const sql = await getSql();
  return sql<DbSceneRow>`
    select * from ptm_scenes
    where project_id = ${projectId}
    order by sort_order asc, scene_number asc
  `;
}

export async function listCast(projectId: string): Promise<DbCastRow[]> {
  const sql = await getSql();
  return sql<DbCastRow>`
    select * from ptm_cast
    where project_id = ${projectId}
    order by sort_order asc
  `;
}

export async function listVoiceSamples(projectId: string): Promise<DbVoiceSampleRow[]> {
  const sql = await getSql();
  return sql<DbVoiceSampleRow>`
    select * from ptm_voice_samples
    where project_id = ${projectId}
  `;
}

export function contentLocksFromRow(row: DbProjectRow): ProjectContentLocks {
  return {
    screenplayLocked: row.screenplay_locked,
    castLocked: row.cast_locked,
    voiceLocked: row.voice_locked,
    estimateLocked: row.estimate_locked,
    pictureLocked: row.picture_locked,
    generationLocked: row.generation_locked,
  };
}

export async function upsertProjectHeader(input: {
  id: string;
  userId: string;
  title: string;
  author?: string;
  genre?: string;
  sourceKind: "classic" | "custom";
  classicId?: string | null;
  sourceText?: string;
  screenplay?: string;
  stage?: string;
  status?: string;
  wizardStep?: string;
  progress?: number;
  progressLabel?: string;
  unlockedShots?: number;
  stars?: number;
  castingConfirmed?: boolean;
  locks?: Partial<ProjectContentLocks>;
  estimateJson?: unknown;
  voiceJson?: unknown;
  stitchedVoMediaId?: string | null;
  outputMediaId?: string | null;
}): Promise<DbProjectRow> {
  const sql = await getSql();
  const locks = input.locks ?? {};
  const estimate =
    input.estimateJson === undefined || input.estimateJson === null
      ? null
      : JSON.stringify(input.estimateJson);
  const voiceJson = JSON.stringify(input.voiceJson ?? {});

  const rows = await sql<DbProjectRow>`
    insert into ptm_projects (
      id, user_id, title, author, genre, source_kind, classic_id,
      source_text, screenplay, stage, status, wizard_step,
      progress, progress_label, unlocked_shots, stars, casting_confirmed,
      screenplay_locked, cast_locked, voice_locked, estimate_locked,
      picture_locked, generation_locked,
      estimate_json, voice_json, stitched_vo_media_id, output_media_id,
      updated_at
    ) values (
      ${input.id},
      ${input.userId},
      ${input.title},
      ${input.author ?? ""},
      ${input.genre ?? ""},
      ${input.sourceKind},
      ${input.classicId ?? null},
      ${input.sourceText ?? ""},
      ${input.screenplay ?? ""},
      ${input.stage ?? "film"},
      ${input.status ?? "setup"},
      ${input.wizardStep ?? "cast"},
      ${input.progress ?? 0},
      ${input.progressLabel ?? ""},
      ${input.unlockedShots ?? 0},
      ${input.stars ?? 0},
      ${input.castingConfirmed ?? false},
      ${locks.screenplayLocked ?? false},
      ${locks.castLocked ?? false},
      ${locks.voiceLocked ?? false},
      ${locks.estimateLocked ?? false},
      ${locks.pictureLocked ?? false},
      ${locks.generationLocked ?? false},
      ${estimate},
      ${voiceJson},
      ${input.stitchedVoMediaId ?? null},
      ${input.outputMediaId ?? null},
      now()
    )
    on conflict (id) do update set
      title = excluded.title,
      author = excluded.author,
      genre = excluded.genre,
      source_kind = excluded.source_kind,
      classic_id = excluded.classic_id,
      source_text = excluded.source_text,
      screenplay = excluded.screenplay,
      stage = excluded.stage,
      status = excluded.status,
      wizard_step = excluded.wizard_step,
      progress = excluded.progress,
      progress_label = excluded.progress_label,
      unlocked_shots = excluded.unlocked_shots,
      stars = excluded.stars,
      casting_confirmed = excluded.casting_confirmed,
      screenplay_locked = excluded.screenplay_locked,
      cast_locked = excluded.cast_locked,
      voice_locked = excluded.voice_locked,
      estimate_locked = excluded.estimate_locked,
      picture_locked = excluded.picture_locked,
      generation_locked = excluded.generation_locked,
      estimate_json = excluded.estimate_json,
      voice_json = excluded.voice_json,
      stitched_vo_media_id = excluded.stitched_vo_media_id,
      output_media_id = excluded.output_media_id,
      updated_at = now()
    where ptm_projects.user_id = ${input.userId}
    returning *
  `;
  const row = rows[0];
  if (!row) throw new Error("upsertProjectHeader failed (wrong user or DB error)");
  return row;
}

export async function replaceScenes(
  projectId: string,
  scenes: Array<{
    id?: string;
    sceneNumber: number;
    heading: string;
    visual: string;
    dialogue?: string;
    durationSec?: number;
    palette?: string;
    plateMediaId?: string | null;
    renderMediaId?: string | null;
    locked?: boolean;
  }>,
): Promise<void> {
  const sql = await getSql();
  await sql`delete from ptm_scenes where project_id = ${projectId}`;
  for (let i = 0; i < scenes.length; i++) {
    const s = scenes[i]!;
    const id = s.id ?? newId("sc");
    await sql`
      insert into ptm_scenes (
        id, project_id, scene_number, heading, visual, dialogue,
        duration_sec, palette, plate_media_id, render_media_id,
        locked, sort_order, updated_at
      ) values (
        ${id},
        ${projectId},
        ${s.sceneNumber},
        ${s.heading},
        ${s.visual},
        ${s.dialogue ?? null},
        ${s.durationSec ?? 5},
        ${s.palette ?? null},
        ${s.plateMediaId ?? null},
        ${s.renderMediaId ?? null},
        ${s.locked ?? true},
        ${i},
        now()
      )
    `;
  }
}

export async function replaceCast(
  projectId: string,
  cast: Array<{
    id: string;
    roleInStory: string;
    displayName?: string;
    relation?: string;
    selected?: boolean;
    notes?: string;
    photoMediaId?: string | null;
  }>,
): Promise<void> {
  const sql = await getSql();
  // Voice samples FK cast — delete voice first then cast
  await sql`delete from ptm_voice_samples where project_id = ${projectId}`;
  await sql`delete from ptm_cast where project_id = ${projectId}`;
  for (let i = 0; i < cast.length; i++) {
    const c = cast[i]!;
    await sql`
      insert into ptm_cast (
        id, project_id, role_in_story, display_name, relation,
        selected, notes, photo_media_id, sort_order, updated_at
      ) values (
        ${c.id},
        ${projectId},
        ${c.roleInStory},
        ${c.displayName ?? ""},
        ${c.relation ?? "custom"},
        ${c.selected ?? false},
        ${c.notes ?? null},
        ${c.photoMediaId ?? null},
        ${i},
        now()
      )
    `;
  }
}

export async function replaceVoiceSamples(
  projectId: string,
  samples: Array<{
    id?: string;
    castId: string;
    enabled?: boolean;
    hasSample?: boolean;
    consent?: boolean;
    source?: "mic" | "upload" | null;
    sampleLabel?: string;
    captureMediaId?: string | null;
    cloneOutputMediaId?: string | null;
    lineMediaId?: string | null;
    modelId?: string;
  }>,
): Promise<void> {
  const sql = await getSql();
  await sql`delete from ptm_voice_samples where project_id = ${projectId}`;
  for (const s of samples) {
    const id = s.id ?? newId("vs");
    await sql`
      insert into ptm_voice_samples (
        id, project_id, cast_id, enabled, has_sample, consent, source,
        sample_label, capture_media_id, clone_output_media_id, line_media_id,
        model_id, updated_at
      ) values (
        ${id},
        ${projectId},
        ${s.castId},
        ${s.enabled ?? false},
        ${s.hasSample ?? false},
        ${s.consent ?? false},
        ${s.source ?? null},
        ${s.sampleLabel ?? null},
        ${s.captureMediaId ?? null},
        ${s.cloneOutputMediaId ?? null},
        ${s.lineMediaId ?? null},
        ${s.modelId ?? "mock-instant-clone"},
        now()
      )
    `;
  }
}

export async function deleteProject(projectId: string, userId: string): Promise<boolean> {
  const sql = await getSql();
  const rows = await sql<{ id: string }>`
    delete from ptm_projects
    where id = ${projectId} and user_id = ${userId}
    returning id
  `;
  return rows.length > 0;
}

export async function setContentLocks(
  projectId: string,
  userId: string,
  locks: Partial<ProjectContentLocks>,
): Promise<DbProjectRow | null> {
  const existing = await getProject(projectId, userId);
  if (!existing) return null;
  const merged = { ...contentLocksFromRow(existing), ...locks };
  const sql = await getSql();
  const rows = await sql<DbProjectRow>`
    update ptm_projects set
      screenplay_locked = ${merged.screenplayLocked},
      cast_locked = ${merged.castLocked},
      voice_locked = ${merged.voiceLocked},
      estimate_locked = ${merged.estimateLocked},
      picture_locked = ${merged.pictureLocked},
      generation_locked = ${merged.generationLocked},
      updated_at = now()
    where id = ${projectId} and user_id = ${userId}
    returning *
  `;
  return rows[0] ?? null;
}

const DEFAULT_LOCK_TTL_MS = 5 * 60 * 1000;

export async function acquireProjectLock(input: {
  projectId: string;
  userId: string;
  lockKind?: DbLockKind;
  holderLabel?: string;
  clientToken?: string;
  ttlMs?: number;
}): Promise<{ ok: true; lock: DbProjectLockRow } | { ok: false; reason: string; heldBy?: string }> {
  const sql = await getSql();
  const kind = input.lockKind ?? "project";
  const ttl = input.ttlMs ?? DEFAULT_LOCK_TTL_MS;
  const expires = new Date(Date.now() + ttl).toISOString();

  await sql`
    delete from ptm_project_locks
    where project_id = ${input.projectId}
      and lock_kind = ${kind}
      and expires_at < now()
  `;

  const existing = await sql<DbProjectLockRow>`
    select * from ptm_project_locks
    where project_id = ${input.projectId} and lock_kind = ${kind}
    limit 1
  `;
  const cur = existing[0];
  if (cur && cur.user_id !== input.userId) {
    return {
      ok: false,
      reason: "Project is locked by another editor",
      heldBy: cur.holder_label ?? cur.user_id,
    };
  }

  if (cur) {
    const rows = await sql<DbProjectLockRow>`
      update ptm_project_locks set
        expires_at = ${expires},
        holder_label = ${input.holderLabel ?? cur.holder_label},
        client_token = ${input.clientToken ?? cur.client_token}
      where id = ${cur.id}
      returning *
    `;
    return { ok: true, lock: rows[0]! };
  }

  const id = newId("lk");
  const rows = await sql<DbProjectLockRow>`
    insert into ptm_project_locks (
      id, project_id, user_id, lock_kind, holder_label, expires_at, client_token
    ) values (
      ${id},
      ${input.projectId},
      ${input.userId},
      ${kind},
      ${input.holderLabel ?? null},
      ${expires},
      ${input.clientToken ?? null}
    )
    returning *
  `;
  return { ok: true, lock: rows[0]! };
}

export async function releaseProjectLock(input: {
  projectId: string;
  userId: string;
  lockKind?: DbLockKind;
}): Promise<void> {
  const sql = await getSql();
  const kind = input.lockKind ?? "project";
  await sql`
    delete from ptm_project_locks
    where project_id = ${input.projectId}
      and lock_kind = ${kind}
      and user_id = ${input.userId}
  `;
}

export async function listActiveLocks(projectId: string): Promise<DbProjectLockRow[]> {
  const sql = await getSql();
  await sql`
    delete from ptm_project_locks
    where project_id = ${projectId} and expires_at < now()
  `;
  return sql<DbProjectLockRow>`
    select * from ptm_project_locks where project_id = ${projectId}
  `;
}

export type FullProjectBundle = {
  project: DbProjectRow;
  scenes: DbSceneRow[];
  cast: DbCastRow[];
  voiceSamples: DbVoiceSampleRow[];
  locks: DbProjectLockRow[];
};

export async function loadFullProject(
  projectId: string,
  userId: string,
): Promise<FullProjectBundle | null> {
  const project = await getProject(projectId, userId);
  if (!project) return null;
  const [scenes, cast, voiceSamples, locks] = await Promise.all([
    listScenes(projectId),
    listCast(projectId),
    listVoiceSamples(projectId),
    listActiveLocks(projectId),
  ]);
  return { project, scenes, cast, voiceSamples, locks };
}
