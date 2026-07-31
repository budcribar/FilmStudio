/**
 * Voice clone provider seam (Phase 2 finish — not wired to a real API yet).
 * Capture produces VoiceCaptureAsset; this module will POST them later.
 */
import type { VoiceCaptureAsset } from "../capture/audio-capture";
import type { VoiceSample } from "../voice";

export type VoiceCloneJob = {
  castMemberId: string;
  status: "queued" | "ready" | "failed" | "demo";
  providerVoiceId?: string;
  message?: string;
};

export type VoiceCloneProvider = {
  id: string;
  /** True when env/key would allow live clone */
  isLive: () => boolean;
  createClone: (sample: VoiceSample) => Promise<VoiceCloneJob>;
};

/** Demo provider: validates asset exists, returns fake ready job. */
export const demoVoiceCloneProvider: VoiceCloneProvider = {
  id: "demo",
  isLive: () => false,
  async createClone(sample) {
    const asset = sample.asset as VoiceCaptureAsset | undefined;
    if (!asset?.dataUrl) {
      return {
        castMemberId: sample.castMemberId,
        status: "failed",
        message: "No capture asset",
      };
    }
    // Simulate network
    await new Promise((r) => setTimeout(r, 200));
    return {
      castMemberId: sample.castMemberId,
      status: "demo",
      providerVoiceId: `demo_voice_${sample.castMemberId}`,
      message: "Demo clone — real provider not configured",
    };
  },
};

export function getVoiceCloneProvider(): VoiceCloneProvider {
  // Future: if (import.meta.env.VITE_VOICE_CLONE_PROVIDER === "elevenlabs") ...
  return demoVoiceCloneProvider;
}
