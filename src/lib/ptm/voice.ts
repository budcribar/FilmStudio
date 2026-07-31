import type { CastMember } from "./characters";
import type { VoiceCaptureAsset } from "./capture/audio-capture";

export type VoiceSampleSource = "mic" | "upload" | null;

/** Per-role optional voice clone sample */
export type VoiceSample = {
  castMemberId: string;
  roleInStory: string;
  displayName: string;
  /** User opted this role into the voice add-on */
  enabled: boolean;
  /** Template captured and ready for future clone API */
  hasSample: boolean;
  sampleLabel?: string;
  /** How the template was captured */
  source: VoiceSampleSource;
  /** Real capture payload (data URL for demo persistence) */
  asset?: VoiceCaptureAsset;
  /** Guardian / self consent for this identity sample */
  consent: boolean;
};

export type VoiceAddon = {
  /** Master: include personal voices (paid add-on) */
  enabled: boolean;
  samples: VoiceSample[];
};

/** Base fee to turn on the voice add-on, plus per cloned role */
export const VOICE_ADDON_BASE_CREDITS = 5;
export const VOICE_PER_ROLE_CREDITS = 4;

export function emptyVoiceAddon(): VoiceAddon {
  return { enabled: false, samples: [] };
}

/** Sync voice slots from current cast (selected people who can speak). */
export function syncVoiceFromCast(cast: CastMember[], prev?: VoiceAddon): VoiceAddon {
  const prevMap = new Map((prev?.samples ?? []).map((s) => [s.castMemberId, s]));
  const candidates = cast.filter(
    (c) => c.selected && (c.displayName.trim() || c.photoDataUrl),
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
      asset: old?.asset,
      consent: old?.consent ?? false,
    };
  });

  return {
    enabled: prev?.enabled ?? false,
    samples,
  };
}

/** Ready = every enabled role has a sample + consent */
export function voiceRolesReady(voice: VoiceAddon): boolean {
  if (!voice.enabled) return true;
  const active = voice.samples.filter((s) => s.enabled);
  if (active.length === 0) return false;
  return active.every((s) => s.hasSample && s.consent);
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

/** Samples that can be sent to a clone provider later */
export function voiceAssetsForClone(voice: VoiceAddon): VoiceSample[] {
  if (!voice.enabled) return [];
  return voice.samples.filter((s) => s.enabled && s.hasSample && s.consent && s.asset);
}
