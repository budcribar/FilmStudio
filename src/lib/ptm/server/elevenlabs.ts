/**
 * ElevenLabs HTTP helpers — server only.
 * Key from settings-repo (DB) or process.env via resolveSecret.
 * Response audio is returned as bytes for the client media store.
 */

const EL_BASE = "https://api.elevenlabs.io/v1";

export class ElevenLabsError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ElevenLabsError";
  }
}

export async function elevenLabsCreateVoice(opts: {
  apiKey: string;
  name: string;
  /** Audio sample as Blob (wav/mp3/webm) */
  sample: Blob;
  fileName: string;
}): Promise<{ voiceId: string }> {
  const form = new FormData();
  form.append("name", opts.name.slice(0, 100));
  form.append("files", opts.sample, opts.fileName || "sample.webm");
  form.append("description", "Page to Movie personal voice clone");

  const res = await fetch(`${EL_BASE}/voices/add`, {
    method: "POST",
    headers: {
      "xi-api-key": opts.apiKey,
    },
    body: form,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ElevenLabsError(
      res.status,
      `ElevenLabs clone failed (${res.status}): ${text.slice(0, 200)}`,
    );
  }

  const json = (await res.json()) as { voice_id?: string };
  if (!json.voice_id) {
    throw new ElevenLabsError(500, "ElevenLabs response missing voice_id");
  }
  return { voiceId: json.voice_id };
}

export async function elevenLabsSpeak(opts: {
  apiKey: string;
  voiceId: string;
  text: string;
  modelId?: string;
}): Promise<ArrayBuffer> {
  const res = await fetch(`${EL_BASE}/text-to-speech/${opts.voiceId}`, {
    method: "POST",
    headers: {
      "xi-api-key": opts.apiKey,
      "Content-Type": "application/json",
      Accept: "audio/mpeg",
    },
    body: JSON.stringify({
      text: opts.text.slice(0, 2500),
      model_id: opts.modelId ?? "eleven_multilingual_v2",
    }),
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ElevenLabsError(
      res.status,
      `ElevenLabs TTS failed (${res.status}): ${text.slice(0, 200)}`,
    );
  }

  return res.arrayBuffer();
}
