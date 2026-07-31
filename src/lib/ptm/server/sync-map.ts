/**
 * Map client FilmProject ↔ server rows for dual-write.
 * Binaries never leave the client; only media ids are stored.
 */
import type { FilmProject } from "../types";
import type { ProjectContentLocks } from "./types";

export function locksFromClientProject(p: FilmProject): ProjectContentLocks {
  return {
    screenplayLocked: p.screenplayLocked,
    castLocked: p.castingConfirmed && p.wizardStep !== "cast",
    voiceLocked:
      p.wizardStep === "estimate" ||
      p.wizardStep === "confirm" ||
      p.wizardStep === "done",
    estimateLocked: p.wizardStep === "confirm" || p.wizardStep === "done",
    pictureLocked: p.status === "ready",
    generationLocked: p.status === "generating" || p.status === "ready",
  };
}

export function clientProjectToServerHeader(p: FilmProject, userId: string) {
  return {
    id: p.id,
    userId,
    title: p.title,
    author: p.author,
    genre: p.genre,
    sourceKind: p.sourceKind,
    classicId: p.classicId ?? null,
    sourceText: p.sourceText,
    screenplay: p.screenplay,
    stage: p.stage,
    status: p.status,
    wizardStep: p.wizardStep,
    progress: p.progress,
    progressLabel: p.progressLabel,
    unlockedShots: p.unlockedShots,
    stars: p.stars,
    castingConfirmed: p.castingConfirmed,
    locks: locksFromClientProject(p),
    estimateJson: p.estimate ?? null,
    voiceJson: {
      enabled: p.voice.enabled,
      modelId: p.voice.modelId,
      stitchedVoMediaId: p.voice.stitchedVoMediaId,
      samples: p.voice.samples.map((s) => ({
        castMemberId: s.castMemberId,
        enabled: s.enabled,
        hasSample: s.hasSample,
        consent: s.consent,
        source: s.source,
        mediaId: s.asset?.mediaId,
        cloneOutputMediaId: s.cloneOutputMediaId,
        lineMediaId: s.lineMediaId,
      })),
    },
    stitchedVoMediaId: p.voice.stitchedVoMediaId ?? null,
    outputMediaId: null as string | null,
  };
}

/** Global-unique scene PK: classics reuse local shot ids across books. */
export function clientShotsToServerScenes(p: FilmProject) {
  return p.shots.map((s, i) => ({
    id: `${p.id}__${s.id}`,
    sceneNumber: s.scene,
    heading: s.heading,
    visual: s.visual,
    dialogue: s.dialogue,
    durationSec: s.durationSec,
    palette: s.palette,
    locked: i >= p.unlockedShots,
    plateMediaId: null as string | null,
    renderMediaId: null as string | null,
  }));
}

export function serverSceneIdToClient(projectId: string, serverId: string): string {
  const prefix = `${projectId}__`;
  return serverId.startsWith(prefix) ? serverId.slice(prefix.length) : serverId;
}
