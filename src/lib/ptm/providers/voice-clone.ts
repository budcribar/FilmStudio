/**
 * Voice clone + line TTS providers.
 *
 * Contract: binary I/O via client media store (MediaRef ids), not server disks.
 * Mock provider returns a synthetic MP3 into IndexedDB so client FFmpeg stitch
 * can treat it like any other local MP3.
 */

import type { VoiceCaptureAsset } from "../capture/audio-capture";
import { stitchAudioClipsClient } from "../media/client-stitch";
import {
  createObjectUrlSafe,
  putMediaBlobSafe,
  type MediaRef,
} from "../media/client-media-store";
import { buildMockMp3Blob } from "../media/mock-mp3";
import { getDefaultVoiceModel, getVoiceModel } from "../models/voice-models";
import type { VoiceSample } from "../voice";

export type VoiceCloneJob = {
  castMemberId: string;
  status: "queued" | "ready" | "failed" | "demo";
  modelId: string;
  /** Client media id of cloned template or TTS line */
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
  /** Register/clone from capture asset already on the client */
  createClone: (
    sample: VoiceSample,
    opts?: { projectId?: string },
  ) => Promise<VoiceCloneJob>;
  /** Synthesize a dialogue line as MP3 on the client store */
  speakLine: (
    sample: VoiceSample,
    text: string,
    opts?: { projectId?: string; durationSec?: number },
  ) => Promise<SpeakLineResult>;
};

function requireCapture(sample: VoiceSample): VoiceCaptureAsset {
  const asset = sample.asset;
  if (!asset?.mediaId) {
    throw new Error("No client capture mediaId on sample");
  }
  return asset;
}

/** Mock: “clone” acknowledges capture, then speakLine writes fake MP3. */
export const mockVoiceCloneProvider: VoiceCloneProvider = {
  id: "mock",
  modelId: "mock-instant-clone",
  isLive: () => false,

  async createClone(sample, opts) {
    const model = getVoiceModel("mock-instant-clone") ?? getDefaultVoiceModel();
    try {
      const asset = requireCapture(sample);
      // Touch capture existence by requiring mediaId; mock does not re-upload.
      await new Promise((r) => setTimeout(r, 180));
      // Optional: write a short “preview timbre” mp3 next to the capture
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
    const durationSec = opts?.durationSec ?? Math.min(4, Math.max(0.8, text.length * 0.045));
    const blob = buildMockMp3Blob(durationSec);
    const media = await putMediaBlobSafe(blob, {
      fileName: `line-${sample.castMemberId}-${Date.now()}.mp3`,
      mimeType: "audio/mpeg",
      durationSec,
      role: "tts-line",
      projectId: opts?.projectId,
    });
    const objectUrl = (await createObjectUrlSafe(media.id))!;
    return {
      castMemberId: sample.castMemberId,
      text,
      media,
      objectUrl,
    };
  },
};

/**
 * ElevenLabs stub — uses model JSON; not live until API key + server proxy.
 * Still would write returned MP3 into client media store for local stitch.
 */
export const elevenLabsStubProvider: VoiceCloneProvider = {
  id: "elevenlabs",
  modelId: "elevenlabs-instant-ivc",
  isLive: () => false,
  async createClone(sample) {
    return {
      castMemberId: sample.castMemberId,
      status: "failed",
      modelId: "elevenlabs-instant-ivc",
      message: "ElevenLabs disabled — enable model + API key (see voice-models.json)",
    };
  },
  async speakLine() {
    throw new Error("ElevenLabs not configured");
  },
};

export function getVoiceCloneProvider(modelId?: string): VoiceCloneProvider {
  const model = modelId ? getVoiceModel(modelId) : getDefaultVoiceModel();
  if (model?.providerId === "elevenlabs" && model.enabled) {
    return elevenLabsStubProvider;
  }
  return mockVoiceCloneProvider;
}

/** Clone all consented samples, then speak first dialogue lines into client MP3s. */
export async function runMockVoicePipeline(opts: {
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

  const lineMedia: SpeakLineResult[] = [];
  for (const line of opts.lines) {
    const sample = opts.samples.find((s) => s.castMemberId === line.castMemberId);
    if (!sample) continue;
    lineMedia.push(
      await provider.speakLine(sample, line.text, { projectId: opts.projectId }),
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
