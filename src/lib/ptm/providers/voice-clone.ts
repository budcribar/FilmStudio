/**
 * Voice clone + line TTS.
 *
 * Provider is chosen from server prefs (Settings). Capture stays in client
 * media store; live providers receive bytes via server fns and return MP3
 * for client storage + stitch.
 */

import type { VoiceCaptureAsset } from "../capture/audio-capture";
import { stitchAudioClipsClient } from "../media/client-stitch";
import {
  createObjectUrlSafe,
  getMediaBlobSafe,
  putMediaBlobSafe,
  type MediaRef,
} from "../media/client-media-store";
import { buildMockMp3Blob } from "../media/mock-mp3";
import { getDefaultVoiceModel, getVoiceModel } from "../models/voice-models";
import {
  cloneVoiceOnServer,
  getVoiceRuntimeStatus,
  speakLineOnServer,
} from "../server/voice-api";
import type { VoiceSample } from "../voice";

export type VoiceCloneJob = {
  castMemberId: string;
  status: "queued" | "ready" | "failed" | "demo";
  modelId: string;
  outputMediaId?: string;
  providerVoiceId?: string;
  message?: string;
  mimeType?: string;
};

export type SpeakLineResult = {
  castMemberId: string;
  text: string;
  media: MediaRef;
  objectUrl: string;
};

export type VoiceCloneProvider = {
  id: string;
  modelId: string;
  isLive: () => boolean;
  createClone: (
    sample: VoiceSample,
    opts?: { projectId?: string },
  ) => Promise<VoiceCloneJob>;
  speakLine: (
    sample: VoiceSample,
    text: string,
    opts?: { projectId?: string; durationSec?: number; providerVoiceId?: string },
  ) => Promise<SpeakLineResult>;
};

function requireCapture(sample: VoiceSample): VoiceCaptureAsset {
  const asset = sample.asset;
  if (!asset?.mediaId) {
    throw new Error("No client capture mediaId on sample");
  }
  return asset;
}

async function blobToBase64(blob: Blob): Promise<string> {
  const buf = await blob.arrayBuffer();
  let binary = "";
  const bytes = new Uint8Array(buf);
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  return btoa(binary);
}

/** Local mock — always works without keys. */
export const mockVoiceCloneProvider: VoiceCloneProvider = {
  id: "mock",
  modelId: "mock-instant-clone",
  isLive: () => false,

  async createClone(sample, opts) {
    const model = getVoiceModel("mock-instant-clone") ?? getDefaultVoiceModel();
    try {
      const asset = requireCapture(sample);
      await new Promise((r) => setTimeout(r, 120));
      const preview = buildMockMp3Blob(0.4);
      const media = await putMediaBlobSafe(preview, {
        fileName: `clone-preview-${sample.castMemberId}.mp3`,
        mimeType: "audio/mpeg",
        durationSec: 0.4,
        role: "clone-preview",
        projectId: opts?.projectId,
      });
      return {
        castMemberId: sample.castMemberId,
        status: "demo",
        modelId: model.id,
        outputMediaId: media.id,
        providerVoiceId: `mock_voice_${sample.castMemberId}`,
        message: `Mock clone ready (capture ${asset.mediaId})`,
        mimeType: "audio/mpeg",
      };
    } catch (e) {
      return {
        castMemberId: sample.castMemberId,
        status: "failed",
        modelId: model.id,
        message: e instanceof Error ? e.message : "Clone failed",
      };
    }
  },

  async speakLine(sample, text, opts) {
    requireCapture(sample);
    const durationSec =
      opts?.durationSec ?? Math.min(4, Math.max(0.8, text.length * 0.045));
    const blob = buildMockMp3Blob(durationSec);
    const media = await putMediaBlobSafe(blob, {
      fileName: `line-${sample.castMemberId}-${Date.now()}.mp3`,
      mimeType: "audio/mpeg",
      durationSec,
      role: "tts-line",
      projectId: opts?.projectId,
    });
    const objectUrl = (await createObjectUrlSafe(media.id))!;
    return { castMemberId: sample.castMemberId, text, media, objectUrl };
  },
};

/**
 * Live path: uses Settings prefs + server proxy (ElevenLabs today).
 * Falls back to mock if no key or provider is mock.
 */
export const configuredVoiceCloneProvider: VoiceCloneProvider = {
  id: "configured",
  modelId: "from-settings",
  isLive: () => true,

  async createClone(sample, opts) {
    const asset = requireCapture(sample);
    try {
      const status = await getVoiceRuntimeStatus();
      if (!status.live) {
        return mockVoiceCloneProvider.createClone(sample, opts);
      }

      const blob = await getMediaBlobSafe(asset.mediaId);
      if (!blob) {
        return {
          castMemberId: sample.castMemberId,
          status: "failed",
          modelId: status.modelId,
          message: "Capture missing from client media store",
        };
      }

      const sampleBase64 = await blobToBase64(blob);
      const res = await cloneVoiceOnServer({
        data: {
          castMemberId: sample.castMemberId,
          displayName: sample.displayName,
          sampleBase64,
          mimeType: asset.mimeType || blob.type || "audio/webm",
          fileName: asset.fileName,
        },
      });

      if (res.status === "demo") {
        return mockVoiceCloneProvider.createClone(sample, opts);
      }

      // Tiny local preview clip so UI still has an output media id
      const preview = buildMockMp3Blob(0.3);
      const media = await putMediaBlobSafe(preview, {
        fileName: `clone-ack-${sample.castMemberId}.mp3`,
        mimeType: "audio/mpeg",
        role: "clone-preview",
        projectId: opts?.projectId,
      });

      return {
        castMemberId: sample.castMemberId,
        status: res.status === "ready" ? "ready" : "failed",
        modelId: res.modelId,
        outputMediaId: media.id,
        providerVoiceId: res.providerVoiceId,
        message: res.message,
        mimeType: "audio/mpeg",
      };
    } catch (e) {
      return {
        castMemberId: sample.castMemberId,
        status: "failed",
        modelId: "configured",
        message: e instanceof Error ? e.message : "Clone failed",
      };
    }
  },

  async speakLine(sample, text, opts) {
    const asset = requireCapture(sample);
    try {
      const status = await getVoiceRuntimeStatus();
      const voiceId = opts?.providerVoiceId;
      if (!status.live || !voiceId || voiceId.startsWith("mock_")) {
        return mockVoiceCloneProvider.speakLine(sample, text, opts);
      }

      const res = await speakLineOnServer({
        data: {
          castMemberId: sample.castMemberId,
          providerVoiceId: voiceId,
          text,
        },
      });

      if (res.status !== "ready" || !res.audioBase64) {
        return mockVoiceCloneProvider.speakLine(sample, text, opts);
      }

      const binary = atob(res.audioBase64);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      const blob = new Blob([bytes], { type: res.mimeType || "audio/mpeg" });
      const media = await putMediaBlobSafe(blob, {
        fileName: `line-${sample.castMemberId}-${Date.now()}.mp3`,
        mimeType: "audio/mpeg",
        role: "tts-line",
        projectId: opts?.projectId,
      });
      const objectUrl = (await createObjectUrlSafe(media.id))!;
      return { castMemberId: sample.castMemberId, text, media, objectUrl };
    } catch {
      return mockVoiceCloneProvider.speakLine(sample, text, {
        ...opts,
        durationSec: opts?.durationSec,
      });
    }
  },
};

export function getVoiceCloneProvider(_modelId?: string): VoiceCloneProvider {
  // Runtime always goes through configured provider (reads Settings on server).
  // Mock is used automatically when prefs say mock or key is missing.
  return configuredVoiceCloneProvider;
}

/** @deprecated use runVoicePipeline */
export const runMockVoicePipeline = runVoicePipeline;

/** Clone consented samples, speak lines, stitch on client. */
export async function runVoicePipeline(opts: {
  samples: VoiceSample[];
  lines: { castMemberId: string; text: string }[];
  projectId?: string;
}): Promise<{
  jobs: VoiceCloneJob[];
  lineMedia: SpeakLineResult[];
  stitched?: { mediaId: string; objectUrl: string; method: string };
}> {
  const provider = getVoiceCloneProvider();
  const jobs: VoiceCloneJob[] = [];
  for (const s of opts.samples) {
    jobs.push(await provider.createClone(s, { projectId: opts.projectId }));
  }

  const voiceByCast = new Map(
    jobs
      .filter((j) => j.providerVoiceId)
      .map((j) => [j.castMemberId, j.providerVoiceId!]),
  );

  const lineMedia: SpeakLineResult[] = [];
  for (const line of opts.lines) {
    const sample = opts.samples.find((s) => s.castMemberId === line.castMemberId);
    if (!sample) continue;
    lineMedia.push(
      await provider.speakLine(sample, line.text, {
        projectId: opts.projectId,
        providerVoiceId: voiceByCast.get(line.castMemberId),
      }),
    );
  }

  let stitched: { mediaId: string; objectUrl: string; method: string } | undefined;
  if (lineMedia.length > 0) {
    const result = await stitchAudioClipsClient(
      lineMedia.map((l) => ({ mediaId: l.media.id, label: l.text.slice(0, 24) })),
      { projectId: opts.projectId, fileName: `vo-mix-${opts.projectId ?? "proj"}.mp3` },
    );
    stitched = {
      mediaId: result.media.id,
      objectUrl: result.objectUrl,
      method: result.method,
    };
  }

  return { jobs, lineMedia, stitched };
}
