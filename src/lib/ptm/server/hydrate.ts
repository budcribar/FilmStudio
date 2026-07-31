/**
 * Map DB rows ↔ FilmProject (serializable DTO for the client).
 * No binary payloads — mediaIds only.
 */
import type { StoryboardShot } from "@/data/classics";
import type { CastMember, CharacterRelation } from "../characters";
import type { ProductionEstimate } from "../estimate";
import type { FilmProject } from "../types";
import type { VoiceAddon, VoiceSample } from "../voice";
import type { FullProjectBundle } from "./projects-repo";
import type { DbProjectRow } from "./types";
import { serverSceneIdToClient } from "./sync-map";

function asEstimate(json: unknown): ProductionEstimate | undefined {
  if (!json || typeof json !== "object") return undefined;
  return json as ProductionEstimate;
}

function asIso(v: string | Date): string {
  if (typeof v === "string") return v;
  return new Date(v).toISOString();
}

export function bundleToFilmProject(bundle: FullProjectBundle): FilmProject {
  const p = bundle.project;
  const shots: StoryboardShot[] = bundle.scenes.map((s) => ({
    id: serverSceneIdToClient(p.id, s.id),
    scene: s.scene_number,
    heading: s.heading,
    visual: s.visual,
    dialogue: s.dialogue ?? undefined,
    durationSec: s.duration_sec,
    palette: s.palette ?? "from-[#1a1c22] to-[#0a0b0d]",
  }));

  const cast: CastMember[] = bundle.cast.map((c) => ({
    id: c.id,
    roleInStory: c.role_in_story,
    displayName: c.display_name,
    relation: (c.relation as CharacterRelation) || "other",
    notes: c.notes ?? undefined,
    selected: c.selected,
    photoMediaId: c.photo_media_id ?? undefined,
  }));

  const voiceSamples: VoiceSample[] = bundle.voiceSamples.map((v) => ({
    castMemberId: v.cast_id,
    roleInStory: cast.find((c) => c.id === v.cast_id)?.roleInStory ?? "",
    displayName: cast.find((c) => c.id === v.cast_id)?.displayName ?? "",
    enabled: v.enabled,
    hasSample: v.has_sample,
    consent: v.consent,
    source: v.source,
    sampleLabel: v.sample_label ?? undefined,
    asset: v.capture_media_id
      ? {
          mediaId: v.capture_media_id,
          mimeType: "application/octet-stream",
          kind: "audio" as const,
          fileName: v.sample_label ?? "capture",
          durationSec: 0,
          byteLength: 0,
        }
      : undefined,
    cloneOutputMediaId: v.clone_output_media_id ?? undefined,
    lineMediaId: v.line_media_id ?? undefined,
  }));

  const voiceJson = (p.voice_json ?? {}) as {
    enabled?: boolean;
    modelId?: string;
    stitchedVoMediaId?: string;
  };

  const voice: VoiceAddon = {
    enabled: voiceJson.enabled ?? voiceSamples.some((s) => s.enabled),
    samples: voiceSamples,
    modelId: voiceJson.modelId ?? "mock-instant-clone",
    stitchedVoMediaId:
      p.stitched_vo_media_id ?? voiceJson.stitchedVoMediaId ?? undefined,
  };

  if (voice.samples.length === 0 && cast.length > 0) {
    voice.samples = cast
      .filter((c) => c.selected)
      .map((c) => ({
        castMemberId: c.id,
        roleInStory: c.roleInStory,
        displayName: c.displayName,
        enabled: false,
        hasSample: false,
        consent: false,
        source: null,
      }));
  }

  return {
    id: p.id,
    title: p.title,
    author: p.author,
    genre: p.genre,
    sourceText: p.source_text,
    screenplay: p.screenplay,
    shots,
    stage: p.stage,
    screenplayLocked: p.screenplay_locked,
    status: p.status,
    wizardStep: p.wizard_step,
    sourceKind: p.source_kind,
    progress: p.progress,
    progressLabel: p.progress_label,
    estimate: asEstimate(p.estimate_json),
    cast,
    castingConfirmed: p.casting_confirmed,
    voice,
    unlockedShots: p.unlocked_shots,
    classicId: p.classic_id ?? undefined,
    createdAt: asIso(p.created_at),
    updatedAt: asIso(p.updated_at),
    stars: p.stars,
  };
}

export function projectRowSummary(p: DbProjectRow) {
  return {
    id: p.id,
    title: p.title,
    author: p.author,
    genre: p.genre,
    status: p.status,
    wizardStep: p.wizard_step,
    sourceKind: p.source_kind,
    updatedAt: asIso(p.updated_at),
    stars: p.stars,
    classicId: p.classic_id ?? undefined,
  };
}
