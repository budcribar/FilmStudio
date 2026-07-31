/**
 * Phase 2 capture — browser-side voice templates.
 * Binaries go to client media store (IndexedDB); only MediaRef returns here.
 */

import {
  putMediaBlobSafe,
  type MediaRef,
} from "../media/client-media-store";

export const CAPTURE_TARGET_SEC = 12;
export const CAPTURE_MAX_SEC = 20;
export const CAPTURE_MIN_SEC = 2;
/** Soft cap for a single capture before FFmpeg/client work */
export const CAPTURE_MAX_BYTES = 8 * 1024 * 1024;

export type CaptureKind = "audio" | "video";

/** Lightweight capture result — blob is NOT embedded; use mediaId. */
export type VoiceCaptureAsset = {
  mediaId: string;
  mimeType: string;
  kind: CaptureKind;
  fileName: string;
  durationSec: number;
  byteLength: number;
};

export type CaptureErrorCode =
  | "unsupported"
  | "permission_denied"
  | "no_device"
  | "too_short"
  | "too_large"
  | "empty"
  | "read_failed"
  | "aborted";

export class CaptureError extends Error {
  code: CaptureErrorCode;
  constructor(code: CaptureErrorCode, message: string) {
    super(message);
    this.code = code;
    this.name = "CaptureError";
  }
}

export function isMicSupported(): boolean {
  return (
    typeof window !== "undefined" &&
    !!navigator.mediaDevices?.getUserMedia &&
    typeof MediaRecorder !== "undefined"
  );
}

export function pickRecorderMime(): string {
  const candidates = [
    "audio/webm;codecs=opus",
    "audio/webm",
    "audio/mp4",
    "audio/ogg;codecs=opus",
  ];
  for (const m of candidates) {
    if (typeof MediaRecorder !== "undefined" && MediaRecorder.isTypeSupported(m)) {
      return m;
    }
  }
  return "";
}

export function probeDurationFromBlob(blob: Blob, mimeType: string): Promise<number> {
  return new Promise((resolve) => {
    const isVideo = mimeType.startsWith("video/");
    const el = document.createElement(isVideo ? "video" : "audio");
    el.preload = "metadata";
    const url = URL.createObjectURL(blob);
    const done = (sec: number) => {
      URL.revokeObjectURL(url);
      el.removeAttribute("src");
      el.load();
      resolve(sec);
    };
    el.onloadedmetadata = () => {
      const d = el.duration;
      if (Number.isFinite(d) && d > 0) done(Math.round(d * 10) / 10);
      else done(0);
    };
    el.onerror = () => done(0);
    window.setTimeout(() => done(0), 4000);
    el.src = url;
  });
}

async function blobToAsset(
  blob: Blob,
  opts: {
    fileName: string;
    mimeType: string;
    kind: CaptureKind;
    durationSec: number;
    role?: string;
    projectId?: string;
  },
): Promise<VoiceCaptureAsset> {
  const ref: MediaRef = await putMediaBlobSafe(blob, {
    fileName: opts.fileName,
    mimeType: opts.mimeType,
    durationSec: opts.durationSec,
    role: opts.role ?? "capture",
    projectId: opts.projectId,
  });
  return {
    mediaId: ref.id,
    mimeType: ref.mimeType,
    kind: opts.kind,
    fileName: ref.fileName,
    durationSec: opts.durationSec,
    byteLength: ref.byteLength,
  };
}

export async function fileToCaptureAsset(
  file: File,
  opts?: { projectId?: string },
): Promise<VoiceCaptureAsset> {
  if (!file || file.size === 0) {
    throw new CaptureError("empty", "That file is empty.");
  }
  if (file.size > CAPTURE_MAX_BYTES) {
    throw new CaptureError(
      "too_large",
      `Keep samples under ${Math.round(CAPTURE_MAX_BYTES / (1024 * 1024))}MB.`,
    );
  }
  const isVideo = file.type.startsWith("video/");
  const isAudio =
    file.type.startsWith("audio/") ||
    /\.(mp3|m4a|wav|aac|ogg|webm)$/i.test(file.name);
  if (!isVideo && !isAudio) {
    throw new CaptureError(
      "unsupported",
      "Use an audio file or a video with speech.",
    );
  }

  const mimeType = file.type || (isVideo ? "video/mp4" : "audio/mpeg");
  let durationSec = await probeDurationFromBlob(file, mimeType);
  if (durationSec > 0 && durationSec < CAPTURE_MIN_SEC) {
    throw new CaptureError(
      "too_short",
      `Need at least ~${CAPTURE_MIN_SEC}s of speech.`,
    );
  }

  return blobToAsset(file, {
    fileName: file.name,
    mimeType,
    kind: isVideo ? "video" : "audio",
    durationSec: durationSec || 0,
    role: "voice-capture",
    projectId: opts?.projectId,
  });
}

export type MicRecorderSession = {
  stop: () => Promise<VoiceCaptureAsset>;
  cancel: () => void;
  getElapsed: () => number;
  stream: MediaStream;
};

export async function startMicSession(opts?: {
  maxSec?: number;
  onTick?: (elapsedSec: number) => void;
  projectId?: string;
}): Promise<MicRecorderSession> {
  if (!isMicSupported()) {
    throw new CaptureError(
      "unsupported",
      "This browser can’t record audio. Upload a clip instead.",
    );
  }

  let stream: MediaStream;
  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        channelCount: 1,
      },
      video: false,
    });
  } catch (e) {
    const name = e instanceof DOMException ? e.name : "";
    if (name === "NotAllowedError" || name === "PermissionDeniedError") {
      throw new CaptureError(
        "permission_denied",
        "Microphone blocked — allow access or upload a file.",
      );
    }
    if (name === "NotFoundError" || name === "DevicesNotFoundError") {
      throw new CaptureError("no_device", "No microphone found. Upload a file instead.");
    }
    throw new CaptureError("unsupported", "Could not open the microphone.");
  }

  const mimeType = pickRecorderMime();
  const recorder = mimeType
    ? new MediaRecorder(stream, { mimeType })
    : new MediaRecorder(stream);

  const chunks: BlobPart[] = [];
  recorder.ondataavailable = (ev) => {
    if (ev.data.size > 0) chunks.push(ev.data);
  };

  let startedAt = Date.now();
  let tickTimer: number | undefined;
  let maxTimer: number | undefined;
  let stopped = false;
  let resolveStop: ((a: VoiceCaptureAsset) => void) | null = null;
  let rejectStop: ((e: Error) => void) | null = null;

  const cleanupStream = () => {
    stream.getTracks().forEach((t) => t.stop());
    if (tickTimer) window.clearInterval(tickTimer);
    if (maxTimer) window.clearTimeout(maxTimer);
  };

  const finish = async () => {
    const blob = new Blob(chunks, {
      type: recorder.mimeType || mimeType || "audio/webm",
    });
    if (blob.size === 0) {
      throw new CaptureError("empty", "Nothing was recorded. Try again.");
    }
    if (blob.size > CAPTURE_MAX_BYTES) {
      throw new CaptureError("too_large", "Recording is too large. Try a shorter clip.");
    }
    const elapsed = Math.max(0, (Date.now() - startedAt) / 1000);
    let durationSec = await probeDurationFromBlob(blob, blob.type);
    if (!durationSec) durationSec = Math.round(elapsed * 10) / 10;
    if (durationSec < CAPTURE_MIN_SEC) {
      throw new CaptureError(
        "too_short",
        `Speak for at least ~${CAPTURE_MIN_SEC} seconds.`,
      );
    }
    const ext = blob.type.includes("mp4") ? "m4a" : "webm";
    return blobToAsset(blob, {
      fileName: `mic-${Date.now()}.${ext}`,
      mimeType: blob.type || "audio/webm",
      kind: "audio",
      durationSec,
      role: "voice-capture",
      projectId: opts?.projectId,
    });
  };

  recorder.onstop = () => {
    void (async () => {
      try {
        const asset = await finish();
        resolveStop?.(asset);
      } catch (err) {
        rejectStop?.(err instanceof Error ? err : new Error(String(err)));
      } finally {
        cleanupStream();
      }
    })();
  };

  recorder.start(250);
  startedAt = Date.now();

  const maxSec = opts?.maxSec ?? CAPTURE_MAX_SEC;
  tickTimer = window.setInterval(() => {
    opts?.onTick?.(Math.floor((Date.now() - startedAt) / 1000));
  }, 250);
  maxTimer = window.setTimeout(() => {
    if (!stopped && recorder.state === "recording") {
      stopped = true;
      recorder.stop();
    }
  }, maxSec * 1000);

  return {
    stream,
    getElapsed: () => (Date.now() - startedAt) / 1000,
    cancel: () => {
      if (stopped) return;
      stopped = true;
      rejectStop?.(new CaptureError("aborted", "Recording cancelled."));
      resolveStop = null;
      rejectStop = null;
      try {
        if (recorder.state === "recording") recorder.stop();
      } catch {
        /* ignore */
      }
      cleanupStream();
    },
    stop: () =>
      new Promise<VoiceCaptureAsset>((resolve, reject) => {
        if (stopped) {
          reject(new CaptureError("aborted", "Recorder already stopped."));
          return;
        }
        stopped = true;
        resolveStop = resolve;
        rejectStop = reject;
        try {
          if (recorder.state === "recording") recorder.stop();
          else {
            void finish()
              .then(resolve)
              .catch(reject)
              .finally(cleanupStream);
          }
        } catch (e) {
          cleanupStream();
          reject(e instanceof Error ? e : new Error(String(e)));
        }
      }),
  };
}

export function formatCaptureLabel(asset: VoiceCaptureAsset, source: "mic" | "upload") {
  const dur =
    asset.durationSec > 0 ? `${Math.round(asset.durationSec)}s` : "clip";
  if (source === "mic") return `Mic · ${dur}`;
  if (asset.kind === "video") return `Video · ${dur} · ${asset.fileName}`;
  return `Audio · ${dur} · ${asset.fileName}`;
}

export function captureErrorMessage(err: unknown): string {
  if (err instanceof CaptureError) return err.message;
  if (err instanceof Error) return err.message;
  return "Capture failed.";
}

/** Strip non-serializable / huge fields before Zustand persist. */
export function serializeCaptureForPersist(
  asset: VoiceCaptureAsset | undefined,
): VoiceCaptureAsset | undefined {
  if (!asset?.mediaId) return undefined;
  return {
    mediaId: asset.mediaId,
    mimeType: asset.mimeType,
    kind: asset.kind,
    fileName: asset.fileName,
    durationSec: asset.durationSec,
    byteLength: asset.byteLength,
  };
}
