/**
 * Client-side binary media store (MP3 / MP4 / capture blobs).
 *
 * Architecture intent (product):
 * - Large files stay on the client (IndexedDB), not server object storage.
 * - Project JSON / Zustand only holds lightweight MediaRef keys.
 * - Client FFmpeg (future) stitches refs into a final cut without server CPU.
 *
 * Do NOT put base64 data URLs into localStorage / Zustand persist.
 */

const DB_NAME = "ptm-client-media";
const DB_VERSION = 1;
const STORE = "blobs";

export type MediaKind = "audio" | "video" | "image" | "other";

export type MediaRef = {
  /** IndexedDB key — stable, serializable, small */
  id: string;
  mimeType: string;
  kind: MediaKind;
  fileName: string;
  byteLength: number;
  durationSec?: number;
  createdAt: string;
  /** Optional tag: capture | clone-output | stitch | plate */
  role?: string;
  projectId?: string;
};

type MediaRecord = MediaRef & {
  blob: Blob;
};

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof indexedDB === "undefined") {
      reject(new Error("IndexedDB unavailable"));
      return;
    }
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: "id" });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error("IDB open failed"));
  });
}

function idbReq<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error("IDB request failed"));
  });
}

function newMediaId(prefix = "m") {
  return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

function kindFromMime(mimeType: string): MediaKind {
  if (mimeType.startsWith("audio/")) return "audio";
  if (mimeType.startsWith("video/")) return "video";
  if (mimeType.startsWith("image/")) return "image";
  return "other";
}

export async function putMediaBlob(
  blob: Blob,
  meta: {
    fileName: string;
    mimeType?: string;
    durationSec?: number;
    role?: string;
    projectId?: string;
    id?: string;
  },
): Promise<MediaRef> {
  const mimeType = meta.mimeType || blob.type || "application/octet-stream";
  const ref: MediaRef = {
    id: meta.id ?? newMediaId(),
    mimeType,
    kind: kindFromMime(mimeType),
    fileName: meta.fileName,
    byteLength: blob.size,
    durationSec: meta.durationSec,
    createdAt: new Date().toISOString(),
    role: meta.role,
    projectId: meta.projectId,
  };
  const db = await openDb();
  try {
    const tx = db.transaction(STORE, "readwrite");
    await idbReq(tx.objectStore(STORE).put({ ...ref, blob } satisfies MediaRecord));
  } finally {
    db.close();
  }
  return ref;
}

export async function getMediaRecord(id: string): Promise<MediaRecord | null> {
  const db = await openDb();
  try {
    const tx = db.transaction(STORE, "readonly");
    const row = await idbReq(tx.objectStore(STORE).get(id));
    return (row as MediaRecord) ?? null;
  } finally {
    db.close();
  }
}

export async function getMediaBlob(id: string): Promise<Blob | null> {
  const row = await getMediaRecord(id);
  return row?.blob ?? null;
}

export async function getMediaRef(id: string): Promise<MediaRef | null> {
  const row = await getMediaRecord(id);
  if (!row) return null;
  const { blob: _b, ...ref } = row;
  return ref;
}

/** Object URL for <audio>/<video>/ffmpeg input. Caller must revoke. */
export async function createObjectUrl(id: string): Promise<string | null> {
  const blob = await getMediaBlob(id);
  if (!blob) return null;
  return URL.createObjectURL(blob);
}

export async function deleteMedia(id: string): Promise<void> {
  const db = await openDb();
  try {
    const tx = db.transaction(STORE, "readwrite");
    await idbReq(tx.objectStore(STORE).delete(id));
  } finally {
    db.close();
  }
}

export async function deleteMediaMany(ids: string[]): Promise<void> {
  await Promise.all(ids.map((id) => deleteMedia(id)));
}

/** File → client media (capture / upload path). */
export async function putMediaFile(
  file: File,
  meta?: { durationSec?: number; role?: string; projectId?: string },
): Promise<MediaRef> {
  return putMediaBlob(file, {
    fileName: file.name,
    mimeType: file.type || undefined,
    durationSec: meta?.durationSec,
    role: meta?.role ?? "capture",
    projectId: meta?.projectId,
  });
}

/**
 * In-memory fallback if IndexedDB fails (private mode quirks).
 * Still avoids bloating Zustand — refs only; blobs held in Map until reload.
 */
const memoryFallback = new Map<string, MediaRecord>();

export async function putMediaBlobSafe(
  blob: Blob,
  meta: Parameters<typeof putMediaBlob>[1],
): Promise<MediaRef> {
  try {
    return await putMediaBlob(blob, meta);
  } catch {
    const mimeType = meta.mimeType || blob.type || "application/octet-stream";
    const ref: MediaRef = {
      id: meta.id ?? newMediaId("mem"),
      mimeType,
      kind: kindFromMime(mimeType),
      fileName: meta.fileName,
      byteLength: blob.size,
      durationSec: meta.durationSec,
      createdAt: new Date().toISOString(),
      role: meta.role,
      projectId: meta.projectId,
    };
    memoryFallback.set(ref.id, { ...ref, blob });
    return ref;
  }
}

export async function getMediaBlobSafe(id: string): Promise<Blob | null> {
  try {
    const b = await getMediaBlob(id);
    if (b) return b;
  } catch {
    /* fall through */
  }
  return memoryFallback.get(id)?.blob ?? null;
}

export async function createObjectUrlSafe(id: string): Promise<string | null> {
  const blob = await getMediaBlobSafe(id);
  if (!blob) return null;
  return URL.createObjectURL(blob);
}
