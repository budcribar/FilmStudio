import { Check, Mic, MicOff, Pause, Play, SkipForward, Square, Upload, Video } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  CAPTURE_TARGET_SEC,
  CaptureError,
  captureErrorMessage,
  fileToCaptureAsset,
  formatCaptureLabel,
  isMicSupported,
  startMicSession,
  type MicRecorderSession,
} from "@/lib/ptm/capture/audio-capture";
import { createObjectUrlSafe } from "@/lib/ptm/media/client-media-store";
import {
  VOICE_ADDON_BASE_CREDITS,
  VOICE_PER_ROLE_CREDITS,
  voiceCreditsExtra,
  voiceRolesReady,
  type VoiceAddon,
} from "@/lib/ptm/voice";
import { cn } from "@/lib/utils";

type Props = {
  voice: VoiceAddon;
  disabled?: boolean;
  projectId?: string;
  onChange: (voice: VoiceAddon) => void;
  onSkip: () => void;
  onContinue: () => void;
};

export function VoicePanel({
  voice,
  disabled,
  projectId,
  onChange,
  onSkip,
  onContinue,
}: Props) {
  const fileRefs = useRef<Record<string, HTMLInputElement | null>>({});
  const sessionRef = useRef<MicRecorderSession | null>(null);
  const [recordingId, setRecordingId] = useState<string | null>(null);
  const [elapsed, setElapsed] = useState(0);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [errorById, setErrorById] = useState<Record<string, string>>({});
  const [playingId, setPlayingId] = useState<string | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const objectUrlRef = useRef<string | null>(null);
  const micOk = isMicSupported();

  useEffect(() => {
    return () => {
      sessionRef.current?.cancel();
      sessionRef.current = null;
      audioRef.current?.pause();
      if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
    };
  }, []);

  const extra = voiceCreditsExtra(voice);
  const ready = voiceRolesReady(voice);
  const hasCandidates = voice.samples.length > 0;

  function setError(castMemberId: string, msg: string) {
    setErrorById((m) => ({ ...m, [castMemberId]: msg }));
  }

  function clearError(castMemberId: string) {
    setErrorById((m) => {
      const next = { ...m };
      delete next[castMemberId];
      return next;
    });
  }

  function setEnabledMaster(enabled: boolean) {
    onChange({
      ...voice,
      enabled,
      samples: voice.samples.map((s) =>
        enabled
          ? s
          : {
              ...s,
              enabled: false,
              hasSample: false,
              source: null,
              asset: undefined,
              consent: false,
            },
      ),
    });
  }

  function patchSample(
    castMemberId: string,
    partial: Partial<VoiceAddon["samples"][0]>,
  ) {
    onChange({
      ...voice,
      enabled: true,
      samples: voice.samples.map((s) =>
        s.castMemberId === castMemberId ? { ...s, ...partial } : s,
      ),
    });
  }

  async function startMic(castMemberId: string) {
    if (disabled || recordingId) return;
    clearError(castMemberId);
    try {
      const session = await startMicSession({
        onTick: (sec) => setElapsed(sec),
        projectId,
      });
      sessionRef.current = session;
      setRecordingId(castMemberId);
      setElapsed(0);
    } catch (err) {
      setError(castMemberId, captureErrorMessage(err));
    }
  }

  async function stopMic(castMemberId: string, displayName: string) {
    const session = sessionRef.current;
    if (!session) return;
    setBusyId(castMemberId);
    try {
      const asset = await session.stop();
      sessionRef.current = null;
      setRecordingId(null);
      patchSample(castMemberId, {
        enabled: true,
        hasSample: true,
        source: "mic",
        asset,
        sampleLabel: `${formatCaptureLabel(asset, "mic")} · ${displayName}`,
      });
      clearError(castMemberId);
    } catch (err) {
      sessionRef.current = null;
      setRecordingId(null);
      if (!(err instanceof CaptureError && err.code === "aborted")) {
        setError(castMemberId, captureErrorMessage(err));
      }
    } finally {
      setBusyId(null);
      setElapsed(0);
    }
  }

  function cancelMic() {
    sessionRef.current?.cancel();
    sessionRef.current = null;
    setRecordingId(null);
    setElapsed(0);
  }

  async function onFile(castMemberId: string, file: File | undefined) {
    if (!file) return;
    clearError(castMemberId);
    setBusyId(castMemberId);
    try {
      const asset = await fileToCaptureAsset(file, { projectId });
      patchSample(castMemberId, {
        enabled: true,
        hasSample: true,
        source: "upload",
        asset,
        sampleLabel: formatCaptureLabel(asset, "upload"),
      });
    } catch (err) {
      setError(castMemberId, captureErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function togglePlay(castMemberId: string, mediaId: string) {
    if (playingId === castMemberId) {
      audioRef.current?.pause();
      setPlayingId(null);
      return;
    }
    audioRef.current?.pause();
    if (objectUrlRef.current) {
      URL.revokeObjectURL(objectUrlRef.current);
      objectUrlRef.current = null;
    }
    const url = await createObjectUrlSafe(mediaId);
    if (!url) {
      setError(castMemberId, "Sample missing from client storage — re-capture.");
      return;
    }
    objectUrlRef.current = url;
    const audio = new Audio(url);
    audioRef.current = audio;
    audio.onended = () => setPlayingId(null);
    audio.onerror = () => {
      setPlayingId(null);
      setError(castMemberId, "Could not play this sample.");
    };
    void audio.play().then(() => setPlayingId(castMemberId));
  }

  return (
    <Card className="border-border-strong">
      <CardContent className="p-5 sm:p-6 space-y-5">
        <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[var(--radius-md)] border border-border bg-bg-subtle text-cinema">
              <Mic className="h-4 w-4" />
            </span>
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.12em] text-fg-subtle mb-1">
                Step 3 · Voice{" "}
                <span className="text-cinema normal-case tracking-normal">optional add-on</span>
              </p>
              <h2 className="font-display font-semibold text-lg">
                Capture stays on your device
              </h2>
              <p className="text-sm text-fg-muted mt-1 leading-relaxed max-w-xl">
                Mic or upload → client media store (like local MP3/MP4). Mock clone writes a
                fake MP3 into the same store for client-side stitch. Base{" "}
                {VOICE_ADDON_BASE_CREDITS} cr + {VOICE_PER_ROLE_CREDITS} cr per role.
              </p>
            </div>
          </div>
          <Badge variant={extra > 0 ? "cinema" : "default"}>
            {extra > 0 ? `+${extra} credits` : "Optional"}
          </Badge>
        </div>

        <div className="grid sm:grid-cols-2 gap-3">
          <div className="rounded-[var(--radius-lg)] border border-border bg-bg px-4 py-3">
            <div className="flex items-center gap-2 text-cinema mb-1.5">
              <Mic className="h-4 w-4" />
              <p className="font-display font-semibold text-sm">Option A · Mic</p>
            </div>
            <p className="text-xs text-fg-muted leading-relaxed">
              Record ~{CAPTURE_TARGET_SEC}s. Blob → IndexedDB, not server.
              {!micOk && " (Mic unavailable — use upload.)"}
            </p>
          </div>
          <div className="rounded-[var(--radius-lg)] border border-border bg-bg px-4 py-3">
            <div className="flex items-center gap-2 text-cinema mb-1.5">
              <Upload className="h-4 w-4" />
              <p className="font-display font-semibold text-sm">Option B · Upload</p>
            </div>
            <p className="text-xs text-fg-muted leading-relaxed">
              MP3 / M4A / WAV or phone video — stored as a client media ref for later FFmpeg.
            </p>
          </div>
        </div>

        <button
          type="button"
          disabled={disabled || !hasCandidates || !!recordingId}
          onClick={() => setEnabledMaster(!voice.enabled)}
          className={cn(
            "w-full flex items-start gap-3 rounded-[var(--radius-lg)] border px-4 py-3 text-left transition-colors",
            voice.enabled
              ? "border-cinema/40 bg-cinema/5"
              : "border-border bg-bg hover:border-border-strong",
            !hasCandidates && "opacity-60",
          )}
        >
          <span
            className={cn(
              "mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border",
              voice.enabled ? "border-cinema bg-cinema text-bg" : "border-border",
            )}
          >
            {voice.enabled && <Check className="h-3 w-3" />}
          </span>
          <div>
            <p className="font-display font-semibold text-sm">Enable voice add-on</p>
            <p className="text-xs text-fg-muted mt-0.5">
              {hasCandidates
                ? "Capture a sample, consent, then mock MP3 VO on generate."
                : "Personalize a character first to unlock voice slots."}
            </p>
          </div>
        </button>

        {voice.enabled && hasCandidates && (
          <div className="space-y-3">
            {voice.samples.map((s) => {
              const isRec = recordingId === s.castMemberId;
              const isBusy = busyId === s.castMemberId;
              return (
                <div
                  key={s.castMemberId}
                  className={cn(
                    "rounded-[var(--radius-lg)] border px-4 py-4 space-y-3",
                    s.enabled ? "border-cinema/30 bg-bg" : "border-border bg-bg-subtle/50",
                  )}
                >
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                    <div>
                      <p className="font-display font-semibold text-sm">{s.displayName}</p>
                      <p className="text-xs text-fg-muted">as {s.roleInStory}</p>
                    </div>
                    <button
                      type="button"
                      disabled={disabled || !!recordingId}
                      onClick={() =>
                        patchSample(s.castMemberId, {
                          enabled: !s.enabled,
                          hasSample: s.enabled ? false : s.hasSample,
                          source: s.enabled ? null : s.source,
                          asset: s.enabled ? undefined : s.asset,
                          consent: s.enabled ? false : s.consent,
                        })
                      }
                      className="text-xs font-medium text-cinema hover:underline self-start"
                    >
                      {s.enabled ? "Don’t clone this voice" : "Clone this voice"}
                    </button>
                  </div>

                  {s.enabled && (
                    <div className="space-y-3">
                      <p className="text-[11px] uppercase tracking-wide text-fg-subtle">
                        Choose one
                      </p>
                      <div className="grid sm:grid-cols-2 gap-2">
                        <div
                          className={cn(
                            "rounded-[var(--radius-md)] border px-3 py-3 space-y-2",
                            s.source === "mic" && s.hasSample
                              ? "border-cinema/50 bg-cinema/10"
                              : "border-border bg-bg-elevated",
                          )}
                        >
                          <div className="flex items-center gap-2 font-medium text-sm">
                            <Mic className="h-4 w-4 text-cinema" />
                            Microphone
                          </div>
                          {isRec ? (
                            <div className="space-y-2">
                              <p className="text-xs text-cinema tabular-nums">
                                Recording… {elapsed}s
                                <span className="text-fg-subtle">
                                  {" "}
                                  / target {CAPTURE_TARGET_SEC}s
                                </span>
                              </p>
                              <div className="flex flex-wrap gap-2">
                                <Button
                                  type="button"
                                  size="sm"
                                  disabled={disabled || isBusy}
                                  onClick={() => void stopMic(s.castMemberId, s.displayName)}
                                >
                                  <Square className="h-3.5 w-3.5 fill-current" />
                                  Stop & save
                                </Button>
                                <Button
                                  type="button"
                                  size="sm"
                                  variant="ghost"
                                  disabled={disabled}
                                  onClick={cancelMic}
                                >
                                  Cancel
                                </Button>
                              </div>
                            </div>
                          ) : (
                            <Button
                              type="button"
                              size="sm"
                              variant="secondary"
                              disabled={disabled || !micOk || !!recordingId || isBusy}
                              onClick={() => void startMic(s.castMemberId)}
                            >
                              <Mic className="h-3.5 w-3.5" />
                              {s.source === "mic" && s.hasSample
                                ? "Re-record"
                                : `Record ~${CAPTURE_TARGET_SEC}s`}
                            </Button>
                          )}
                        </div>

                        <div
                          className={cn(
                            "rounded-[var(--radius-md)] border px-3 py-3 space-y-2",
                            s.source === "upload" && s.hasSample
                              ? "border-cinema/50 bg-cinema/10"
                              : "border-border bg-bg-elevated",
                          )}
                        >
                          <div className="flex items-center gap-2 font-medium text-sm">
                            {s.asset?.kind === "video" ? (
                              <Video className="h-4 w-4 text-cinema" />
                            ) : (
                              <Upload className="h-4 w-4 text-cinema" />
                            )}
                            Upload
                          </div>
                          <Button
                            type="button"
                            size="sm"
                            variant="secondary"
                            disabled={disabled || !!recordingId || isBusy}
                            onClick={() => fileRefs.current[s.castMemberId]?.click()}
                          >
                            <Upload className="h-3.5 w-3.5" />
                            {s.source === "upload" && s.hasSample
                              ? "Replace file"
                              : "Audio or video"}
                          </Button>
                          <input
                            ref={(el) => {
                              fileRefs.current[s.castMemberId] = el;
                            }}
                            type="file"
                            accept="audio/*,video/*,.mp3,.m4a,.wav,.aac,.mp4,.mov,.webm"
                            className="sr-only"
                            onChange={(e) => {
                              void onFile(s.castMemberId, e.target.files?.[0]);
                              e.target.value = "";
                            }}
                          />
                        </div>
                      </div>

                      {s.hasSample && s.asset?.mediaId && (
                        <div className="flex flex-col sm:flex-row sm:items-center gap-2 rounded-[var(--radius-md)] border border-success/25 bg-success/5 px-3 py-2">
                          <p className="text-xs text-success flex items-center gap-1.5 flex-1 min-w-0">
                            <Check className="h-3.5 w-3.5 shrink-0" />
                            <span className="truncate">
                              Client media · {s.sampleLabel}
                              {s.asset.byteLength
                                ? ` · ${Math.round(s.asset.byteLength / 1024)}KB`
                                : ""}
                              <span className="text-fg-subtle"> · {s.asset.mediaId}</span>
                            </span>
                          </p>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            className="self-start"
                            onClick={() => void togglePlay(s.castMemberId, s.asset!.mediaId)}
                          >
                            {playingId === s.castMemberId ? (
                              <>
                                <Pause className="h-3.5 w-3.5" /> Pause
                              </>
                            ) : (
                              <>
                                <Play className="h-3.5 w-3.5" /> Preview
                              </>
                            )}
                          </Button>
                        </div>
                      )}

                      {s.hasSample && (
                        <label className="flex items-start gap-2.5 text-sm text-fg-muted cursor-pointer">
                          <input
                            type="checkbox"
                            className="mt-1 rounded border-border"
                            checked={s.consent}
                            disabled={disabled}
                            onChange={(e) =>
                              patchSample(s.castMemberId, { consent: e.target.checked })
                            }
                          />
                          <span>
                            I have permission to use this voice. Media stays in this browser
                            for local stitch (no server media warehouse).
                          </span>
                        </label>
                      )}

                      {errorById[s.castMemberId] && (
                        <p className="text-xs text-danger">{errorById[s.castMemberId]}</p>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
            <p className="text-xs text-fg-subtle leading-relaxed">
              Models: see <code className="text-fg-muted">src/data/models/models.json</code>
              . Mock clone → fake MP3 → client stitch. ElevenLabs entry is disabled until
              keyed.
            </p>
          </div>
        )}

        {!voice.enabled && (
          <div className="flex items-start gap-2 rounded-[var(--radius-md)] border border-border bg-bg px-3 py-2.5 text-sm text-fg-muted">
            <MicOff className="h-4 w-4 shrink-0 mt-0.5 text-fg-subtle" />
            Stock performance voices will be used — no extra credits.
          </div>
        )}

        <div className="flex flex-col sm:flex-row gap-2">
          <Button
            type="button"
            variant="secondary"
            disabled={disabled || !!recordingId}
            onClick={onSkip}
            className="sm:mr-auto"
          >
            <SkipForward className="h-4 w-4" />
            Skip — stock voices
          </Button>
          <Button
            type="button"
            disabled={disabled || !ready || !!recordingId}
            onClick={onContinue}
          >
            {extra > 0
              ? `Continue with voice · +${extra} cr`
              : "Continue to estimate"}
          </Button>
        </div>
        {voice.enabled && !ready && (
          <p className="text-xs text-danger">
            Capture a sample and check consent for each cloned role, or skip.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
