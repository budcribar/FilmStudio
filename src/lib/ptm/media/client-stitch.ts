/**
 * Client-side stitch seam — binary assets stay local (IndexedDB MediaRefs).
 *
 * Production path: @ffmpeg/ffmpeg (wasm) loads MP3/MP4 blobs from
 * client-media-store and writes a final MediaRef. No server media CPU.
 *
 * This module currently concatenates audio blobs naively (mock) so the
 * pipeline can be proven end-to-end without pulling the ffmpeg wasm yet.
 */

import {
  createObjectUrlSafe,
  getMediaBlobSafe,
  putMediaBlobSafe,
  type MediaRef,
} from "./client-media-store";

export type StitchClip = {
  mediaId: string;
  /** Optional label for logging */
  label?: string;
};

export type StitchResult = {
  media: MediaRef;
  objectUrl: string;
  method: "mock-concat" | "ffmpeg";
};

/**
 * Mock audio concat: append raw bytes (works for same-codec mock MP3s).
 * Replace with ffmpeg concat demuxer when @ffmpeg/ffmpeg is added.
 */
export async function stitchAudioClipsClient(
  clips: StitchClip[],
  opts?: { fileName?: string; projectId?: string },
): Promise<StitchResult> {
  if (!clips.length) throw new Error("No clips to stitch");

  const blobs: Blob[] = [];
  for (const c of clips) {
    const b = await getMediaBlobSafe(c.mediaId);
    if (!b) throw new Error(`Missing media ${c.mediaId}`);
    blobs.push(b);
  }

  const out = new Blob(blobs, { type: "audio/mpeg" });
  const media = await putMediaBlobSafe(out, {
    fileName: opts?.fileName ?? `stitch-${Date.now()}.mp3`,
    mimeType: "audio/mpeg",
    role: "stitch",
    projectId: opts?.projectId,
    durationSec: undefined,
  });
  const objectUrl = (await createObjectUrlSafe(media.id))!;
  return { media, objectUrl, method: "mock-concat" };
}

/**
 * Hook for future ffmpeg.wasm:
 *   const ffmpeg = await loadFfmpeg();
 *   for (const c of clips) await ffmpeg.writeFile(name, await blobToU8(blob));
 *   await ffmpeg.exec(["-i", "concat:...", "-c", "copy", "out.mp4"]);
 *   const data = await ffmpeg.readFile("out.mp4");
 *   return putMediaBlobSafe(new Blob([data]), { ... role: "stitch" });
 */
export function clientStitchStrategy(): "mock-concat" | "ffmpeg-planned" {
  return "mock-concat";
}
