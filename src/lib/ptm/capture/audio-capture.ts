/**
 * Phase 2 capture — browser-side voice templates only.
 * No clone provider calls here; produces a portable VoiceCaptureAsset
 * the store can hold and a future voice-clone adapter can upload.
 */

export const CAPTURE_TARGET_SEC = 12;
export const CAPTURE_MAX_SEC = 20;
export const CAPTURE_MIN_SEC = 2;
/** ~3MB data URL budget per sample (localStorage-friendly for a few roles) */
export const CAPTURE_MAX_BYTES = 3 * 1024 * 1024;

export type CaptureKind = "audio" | "video";

export type VoiceCaptureAsset = {
  /** audio/* or video/* mime */
  mimeType: string;
  kind: CaptureKind;
  /** Original filename when upload; synthetic for mic */
  fileName: string;
  durationSec: number;
  byteLength: number;
  /**
   * data: URL for demo persistence / playback.
   * Production would swap for object storage keys after upload.
   */
  dataUrl: string;
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

/** Prefer widely supported recorder mimes. */
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

function blobToDataUrl(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") resolve(reader.result);
      else reject(new CaptureError("read_failed", "Could not read capture data"));
    };
    reader.onerror = () =>
      reject(new CaptureError("read_failed", "Could not read capture data"));
    reader.readAsDataURL(blob);
  });
}

/** Best-effort duration from an audio/video element. */
export function probeDuration(dataUrl: string, mimeType: string): Promise<number> {
  return new Promise((resolve) => {
    const isVideo = mimeType.startsWith("video/");
    const el = document.createElement(isVideo ? "video" : "audio");
    el.preload = "metadata";
    const done = (sec: number) => {
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
    // safety timeout
    window.setTimeout(() => done(0), 4000);
    el.src = dataUrl;
  });
}

export async function fileToCaptureAsset(file: File): Promise<VoiceCaptureAsset> {
  if (!file || file.size === 0) {
    throw new CaptureError("empty", "That file is empty.");
  }
  if (file.size > CAPTURE_MAX_BYTES) {
    throw new CaptureError(
      "too_large",
      `Keep samples under ${Math.round(CAPTURE_MAX_BYTES / (1024 * 1024))}MB for now.`,
    );
  }
  const isVideo = file.type.startsWith("video/");
  const isAudio = file.type.startsWith("audio/") || /\.(mp3|m4a|wav|aac|ogg|webm)$/i.test(file.name);
  if (!isVideo && !isAudio) {
    throw new CaptureError(
      "unsupported",
      "Use an audio file or a video with speech.",
    );
  }

  const dataUrl = await blobToDataUrl(file);
  const mimeType = file.type || (isVideo ? "video/mp4" : "audio/mpeg");
  let durationSec = await probeDuration(dataUrl, mimeType);
  if (durationSec > 0 && durationSec < CAPTURE_MIN_SEC) {
    throw new CaptureError(
      "too_short",
      `Need at least ~${CAPTURE_MIN_SEC}s of speech.`,
    );
  }
  if (durationSec > CAPTURE_MAX_SEC + 5) {
    // Soft warn only: still accept long files but label the cap for clone later
    durationSec = Math.round(durationSec * 10) / 10;
  }

  return {
    mimeType,
    kind: isVideo ? "video" : "audio",
    fileName: file.name,
    durationSec: durationSec || 0,
    byteLength: file.size,
    dataUrl,
  };
}

export type MicRecorderSession = {
  /** Stop and return asset (or throw) */
  stop: () => Promise<VoiceCaptureAsset>;
  /** Cancel without saving */
  cancel: () => void;
  /** Elapsed seconds (approx) */
  getElapsed: () => number;
  stream: MediaStream;
};

/**
 * Start a mic session. Caller should show elapsed UI and call stop() around
 * CAPTURE_TARGET_SEC, or let max duration auto-stop.
 */
export async function startMicSession(opts?: {
  maxSec?: number;
  onTick?: (elapsedSec: number) => void;
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
    const dataUrl = await blobToDataUrl(blob);
    const elapsed = Math.max(0, (Date.now() - startedAt) / 1000);
    let durationSec = await probeDuration(dataUrl, blob.type);
    if (!durationSec) durationSec = Math.round(elapsed * 10) / 10;
    if (durationSec < CAPTURE_MIN_SEC) {
      throw new CaptureError(
        "too_short",
        `Speak for at least ~${CAPTURE_MIN_SEC} seconds.`,
      );
    }
    const ext = blob.type.includes("mp4") ? "m4a" : "webm";
    return {
      mimeType: blob.type || "audio/webm",
      kind: "audio" as const,
      fileName: `mic-${Date.now()}.${ext}`,
      durationSec,
      byteLength: blob.size,
      dataUrl,
    };
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
