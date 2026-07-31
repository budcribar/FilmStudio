import type { CastMember } from "./characters";
import type { VoiceCaptureAsset } from "./capture/audio-capture";
import { serializeCaptureForPersist } from "./capture/audio-capture";
import { getDefaultModelId } from "./models/catalog";

export type VoiceSampleSource = "mic" | "upload" | null;

/** Per-role optional voice clone sample */
export type VoiceSample = {
  castMemberId: string;
  roleInStory: string;
  displayName: string;
  enabled: boolean;
  hasSample: boolean;
  sampleLabel?: string;
  source: VoiceSampleSource;
  /** Capture template — mediaId points at client IndexedDB blob */
  asset?: VoiceCaptureAsset;
  consent: boolean;
  /** Mock/live clone job output media id (client MP3) */
  cloneOutputMediaId?: string;
  /** Last TTS / stitched VO media id for this role */
  lineMediaId?: string;
};

export type VoiceAddon = {
  enabled: boolean;
  samples: VoiceSample[];
  /** Client media id of stitched VO track after generate */
  stitchedVoMediaId?: string;
  /** Model id from models.json (capability: voice) */
  modelId?: string;
};

export const VOICE_ADDON_BASE_CREDITS = 5;
export const VOICE_PER_ROLE_CREDITS = 4;

export function emptyVoiceAddon(): VoiceAddon {
  return {
    enabled: false,
    samples: [],
    modelId: getDefaultModelId("voice") ?? "mock-instant-clone",
  };
}

export function syncVoiceFromCast(cast: CastMember[], prev?: VoiceAddon): VoiceAddon {
  const prevMap = new Map((prev?.samples ?? []).map((s) => [s.castMemberId, s]));
  const candidates = cast.filter(
    (c) => c.selected && (c.displayName.trim() || c.photoDataUrl || c.photoMediaId),
  );

  const samples: VoiceSample[] = candidates.map((c) => {
    const old = prevMap.get(c.id);
    return {
      castMemberId: c.id,
      roleInStory: c.roleInStory,
      displayName: c.displayName.trim() || c.roleInStory,
      enabled: old?.enabled ?? false,
      hasSample: old?.hasSample ?? false,
      sampleLabel: old?.sampleLabel,
      source: old?.source ?? null,
      asset: serializeCaptureForPersist(old?.asset),
      consent: old?.consent ?? false,
      cloneOutputMediaId: old?.cloneOutputMediaId,
      lineMediaId: old?.lineMediaId,
    };
  });

  return {
    enabled: prev?.enabled ?? false,
    samples,
    stitchedVoMediaId: prev?.stitchedVoMediaId,
    modelId: prev?.modelId ?? getDefaultModelId("voice") ?? "mock-instant-clone",
  };
}

export function voiceRolesReady(voice: VoiceAddon): boolean {
  if (!voice.enabled) return true;
  const active = voice.samples.filter((s) => s.enabled);
  if (active.length === 0) return false;
  return active.every((s) => s.hasSample && s.consent && !!s.asset?.mediaId);
}

export function voiceCreditsExtra(voice: VoiceAddon): number {
  if (!voice.enabled) return 0;
  const roles = voice.samples.filter((s) => s.enabled && s.hasSample).length;
  if (roles === 0) return 0;
  return VOICE_ADDON_BASE_CREDITS + roles * VOICE_PER_ROLE_CREDITS;
}

export function voiceRolesCount(voice: VoiceAddon): number {
  if (!voice.enabled) return 0;
  return voice.samples.filter((s) => s.enabled && s.hasSample).length;
}

export function voiceAssetsForClone(voice: VoiceAddon): VoiceSample[] {
  if (!voice.enabled) return [];
  return voice.samples.filter(
    (s) => s.enabled && s.hasSample && s.consent && s.asset?.mediaId,
  );
}
