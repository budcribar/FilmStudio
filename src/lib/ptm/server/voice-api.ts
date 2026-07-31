/**
 * Voice clone / TTS server functions.
 * Client sends capture bytes; server calls the configured provider; MP3 returns
 * to the client for IndexedDB storage + stitch (same media architecture).
 */
import { createServerFn } from "@tanstack/react-start";
import catalog from "@/data/models/voice-models.json";
import { elevenLabsCreateVoice, elevenLabsSpeak } from "./elevenlabs";
import { ptmAuthMiddleware } from "./ptm-auth";
import { resolveVoiceRuntime } from "./settings-api";

function b64ToBlob(b64: string, mimeType: string): Blob {
  const binary = Buffer.from(b64, "base64");
  return new Blob([binary], { type: mimeType });
}

function abToB64(buf: ArrayBuffer): string {
  return Buffer.from(buf).toString("base64");
}

export const getVoiceRuntimeStatus = createServerFn({ method: "GET" })
  .middleware([ptmAuthMiddleware])
  .handler(async ({ context }) => {
    const rt = await resolveVoiceRuntime(context.userId);
    return {
      providerId: rt.providerId,
      modelId: rt.modelId,
      hasApiKey: !!rt.apiKey,
      apiKeySource: rt.apiKeySource,
      live: rt.providerId !== "mock" && !!rt.apiKey,
    };
  });

/**
 * Instant clone from a client capture (base64).
 * Returns provider voice id for later TTS — does not store audio on server.
 */
export const cloneVoiceOnServer = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: {
      castMemberId: string;
      displayName: string;
      sampleBase64: string;
      mimeType: string;
      fileName?: string;
    }) => data,
  )
  .handler(async ({ context, data }) => {
    const rt = await resolveVoiceRuntime(context.userId);

    if (rt.providerId === "mock" || !rt.apiKey) {
      return {
        status: "demo" as const,
        castMemberId: data.castMemberId,
        providerVoiceId: `mock_voice_${data.castMemberId}`,
        modelId: rt.modelId,
        message: "Mock clone — no live API key / mock provider selected",
      };
    }

    if (rt.providerId === "elevenlabs") {
      const blob = b64ToBlob(data.sampleBase64, data.mimeType || "audio/webm");
      const { voiceId } = await elevenLabsCreateVoice({
        apiKey: rt.apiKey,
        name: data.displayName || data.castMemberId,
        sample: blob,
        fileName: data.fileName || "sample.webm",
      });
      return {
        status: "ready" as const,
        castMemberId: data.castMemberId,
        providerVoiceId: voiceId,
        modelId: rt.modelId,
        message: "ElevenLabs voice created",
      };
    }

    return {
      status: "failed" as const,
      castMemberId: data.castMemberId,
      modelId: rt.modelId,
      message: `Provider ${rt.providerId} not implemented yet`,
    };
  });

/**
 * Speak a line with a provider voice id; returns MP3 base64 for client store.
 */
export const speakLineOnServer = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: {
      castMemberId: string;
      providerVoiceId: string;
      text: string;
    }) => data,
  )
  .handler(async ({ context, data }) => {
    const rt = await resolveVoiceRuntime(context.userId);
    const model = catalog.models.find((m) => m.id === rt.modelId);
    const apiModelId =
      model && "apiModelId" in model
        ? ((model as { apiModelId?: string | null }).apiModelId ?? undefined)
        : undefined;

    if (rt.providerId === "mock" || !rt.apiKey) {
      // Client will fall back to local mock MP3
      return {
        status: "demo" as const,
        castMemberId: data.castMemberId,
        mimeType: "audio/mpeg",
        audioBase64: null as string | null,
        message: "Use client mock audio",
      };
    }

    if (rt.providerId === "elevenlabs") {
      const buf = await elevenLabsSpeak({
        apiKey: rt.apiKey,
        voiceId: data.providerVoiceId,
        text: data.text,
        modelId: apiModelId ?? "eleven_multilingual_v2",
      });
      return {
        status: "ready" as const,
        castMemberId: data.castMemberId,
        mimeType: "audio/mpeg",
        audioBase64: abToB64(buf),
        message: "ElevenLabs TTS ok",
      };
    }

    return {
      status: "failed" as const,
      castMemberId: data.castMemberId,
      mimeType: "audio/mpeg",
      audioBase64: null,
      message: `Provider ${rt.providerId} not implemented`,
    };
  });
