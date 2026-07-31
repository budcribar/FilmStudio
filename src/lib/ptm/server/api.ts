/**
 * Server functions — projects live in Postgres; client only holds media blobs.
 */
import { createServerFn } from "@tanstack/react-start";
import type { FilmProject } from "../types";
import { bundleToFilmProject } from "./hydrate";
import { ptmAuthMiddleware } from "./ptm-auth";
import {
  acquireProjectLock,
  deleteProject,
  listProjectsForUser,
  loadFullProject,
  releaseProjectLock,
  replaceCast,
  replaceScenes,
  replaceVoiceSamples,
  upsertProjectHeader,
} from "./projects-repo";
import {
  clientProjectToServerHeader,
  clientShotsToServerScenes,
} from "./sync-map";

export type ProjectDto = FilmProject;

export const listMyProjects = createServerFn({ method: "GET" })
  .middleware([ptmAuthMiddleware])
  .handler(async ({ context }) => {
    const rows = await listProjectsForUser(context.userId);
    const full: ProjectDto[] = [];
    for (const row of rows) {
      const bundle = await loadFullProject(row.id, context.userId);
      if (bundle) full.push(bundleToFilmProject(bundle));
    }
    return full;
  });

export const getMyProject = createServerFn({ method: "GET" })
  .middleware([ptmAuthMiddleware])
  .validator((data: { projectId: string }) => data)
  .handler(async ({ context, data }) => {
    const bundle = await loadFullProject(data.projectId, context.userId);
    if (!bundle) return null;
    return bundleToFilmProject(bundle);
  });

export const saveMyProject = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator((data: { project: FilmProject }) => data)
  .handler(async ({ context, data }) => {
    const p = data.project;
    const safe: FilmProject = {
      ...p,
      cast: p.cast.map(({ photoDataUrl: _p, ...c }) => c),
    };
    const header = clientProjectToServerHeader(safe, context.userId);
    await upsertProjectHeader(header);
    await replaceScenes(safe.id, clientShotsToServerScenes(safe));
    await replaceCast(
      safe.id,
      safe.cast.map((c) => ({
        id: c.id,
        roleInStory: c.roleInStory,
        displayName: c.displayName,
        relation: c.relation,
        selected: c.selected,
        notes: c.notes,
        photoMediaId: c.photoMediaId ?? null,
      })),
    );
    await replaceVoiceSamples(
      safe.id,
      safe.voice.samples.map((s) => ({
        castId: s.castMemberId,
        enabled: s.enabled,
        hasSample: s.hasSample,
        consent: s.consent,
        source: s.source,
        sampleLabel: s.sampleLabel,
        captureMediaId: s.asset?.mediaId ?? null,
        cloneOutputMediaId: s.cloneOutputMediaId ?? null,
        lineMediaId: s.lineMediaId ?? null,
        modelId: safe.voice.modelId,
      })),
    );
    const bundle = await loadFullProject(safe.id, context.userId);
    if (!bundle) throw new Error("Save succeeded but reload failed");
    return bundleToFilmProject(bundle);
  });

export const deleteMyProject = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator((data: { projectId: string }) => data)
  .handler(async ({ context, data }) => {
    return { ok: await deleteProject(data.projectId, context.userId) };
  });

export const acquireMyProjectLock = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: {
      projectId: string;
      lockKind?:
        | "project"
        | "screenplay"
        | "cast"
        | "voice"
        | "estimate"
        | "generate"
        | "render";
      holderLabel?: string;
    }) => data,
  )
  .handler(async ({ context, data }) => {
    return acquireProjectLock({
      projectId: data.projectId,
      userId: context.userId,
      lockKind: data.lockKind,
      holderLabel: data.holderLabel,
    });
  });

export const releaseMyProjectLock = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: {
      projectId: string;
      lockKind?:
        | "project"
        | "screenplay"
        | "cast"
        | "voice"
        | "estimate"
        | "generate"
        | "render";
    }) => data,
  )
  .handler(async ({ context, data }) => {
    await releaseProjectLock({
      projectId: data.projectId,
      userId: context.userId,
      lockKind: data.lockKind,
    });
    return { ok: true as const };
  });
