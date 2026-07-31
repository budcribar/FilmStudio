import { Check, Mic, MicOff, SkipForward, Upload, Video } from "lucide-react";
import { useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  VOICE_ADDON_BASE_CREDITS,
  VOICE_PER_ROLE_CREDITS,
  voiceCreditsExtra,
  voiceRolesReady,
  type VoiceAddon,
  type VoiceSampleSource,
} from "@/lib/ptm/voice";
import { cn } from "@/lib/utils";

type Props = {
  voice: VoiceAddon;
  disabled?: boolean;
  onChange: (voice: VoiceAddon) => void;
  onSkip: () => void;
  onContinue: () => void;
};

export function VoicePanel({ voice, disabled, onChange, onSkip, onContinue }: Props) {
  const fileRefs = useRef<Record<string, HTMLInputElement | null>>({});
  const [recordingId, setRecordingId] = useState<string | null>(null);

  const extra = voiceCreditsExtra(voice);
  const ready = voiceRolesReady(voice);
  const hasCandidates = voice.samples.length > 0;

  function setEnabledMaster(enabled: boolean) {
    onChange({
      ...voice,
      enabled,
      samples: voice.samples.map((s) =>
        enabled ? s : { ...s, enabled: false, hasSample: false, source: null },
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

  function simulateRecord(castMemberId: string, name: string) {
    setRecordingId(castMemberId);
    window.setTimeout(() => {
      patchSample(castMemberId, {
        enabled: true,
        hasSample: true,
        source: "mic" as VoiceSampleSource,
        sampleLabel: `Mic · ~12s · ${name}`,
      });
      setRecordingId(null);
    }, 1100);
  }

  function onFile(castMemberId: string, file: File | undefined) {
    if (!file) return;
    const isVideo = file.type.startsWith("video/");
    patchSample(castMemberId, {
      enabled: true,
      hasSample: true,
      source: "upload",
      sampleLabel: isVideo ? `Video · ${file.name}` : `Audio · ${file.name}`,
    });
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
                How do you want to capture the voice?
              </h2>
              <p className="text-sm text-fg-muted mt-1 leading-relaxed max-w-xl">
                Two equal ways to build a clone template — no script required.{" "}
                <strong className="text-fg font-medium">Record on the mic</strong> or{" "}
                <strong className="text-fg font-medium">upload audio / video</strong> of them
                already speaking. Base {VOICE_ADDON_BASE_CREDITS} cr +{" "}
                {VOICE_PER_ROLE_CREDITS} cr per role.
              </p>
            </div>
          </div>
          <Badge variant={extra > 0 ? "cinema" : "default"}>
            {extra > 0 ? `+${extra} credits` : "Optional"}
          </Badge>
        </div>

        {/* Equal option explainer */}
        <div className="grid sm:grid-cols-2 gap-3">
          <div className="rounded-[var(--radius-lg)] border border-border bg-bg px-4 py-3">
            <div className="flex items-center gap-2 text-cinema mb-1.5">
              <Mic className="h-4 w-4" />
              <p className="font-display font-semibold text-sm">Option A · Mic</p>
            </div>
            <p className="text-xs text-fg-muted leading-relaxed">
              Record ~10–15 seconds live. Say anything natural — a story, a hello, a few
              sentences.
            </p>
          </div>
          <div className="rounded-[var(--radius-lg)] border border-border bg-bg px-4 py-3">
            <div className="flex items-center gap-2 text-cinema mb-1.5">
              <Upload className="h-4 w-4" />
              <p className="font-display font-semibold text-sm">Option B · Upload</p>
            </div>
            <p className="text-xs text-fg-muted leading-relaxed">
              Drop a voice memo, podcast clip, or phone video with clear speech. We use the
              audio track as the template.
            </p>
          </div>
        </div>

        <button
          type="button"
          disabled={disabled || !hasCandidates}
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
                ? "Then pick mic or upload for each character you want to clone."
                : "Personalize a character first (name or photo) to unlock voice slots."}
            </p>
          </div>
        </button>

        {voice.enabled && hasCandidates && (
          <div className="space-y-3">
            {voice.samples.map((s) => (
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
                    disabled={disabled}
                    onClick={() =>
                      patchSample(s.castMemberId, {
                        enabled: !s.enabled,
                        hasSample: s.enabled ? false : s.hasSample,
                        source: s.enabled ? null : s.source,
                      })
                    }
                    className="text-xs font-medium text-cinema hover:underline self-start"
                  >
                    {s.enabled ? "Don’t clone this voice" : "Clone this voice"}
                  </button>
                </div>

                {s.enabled && (
                  <div className="space-y-2">
                    <p className="text-[11px] uppercase tracking-wide text-fg-subtle">
                      Choose one
                    </p>
                    <div className="grid sm:grid-cols-2 gap-2">
                      <button
                        type="button"
                        disabled={disabled || recordingId === s.castMemberId}
                        onClick={() => simulateRecord(s.castMemberId, s.displayName)}
                        className={cn(
                          "flex flex-col items-start gap-1 rounded-[var(--radius-md)] border px-3 py-3 text-left transition-colors",
                          s.source === "mic" && s.hasSample
                            ? "border-cinema/50 bg-cinema/10"
                            : "border-border bg-bg-elevated hover:border-border-strong",
                        )}
                      >
                        <span className="flex items-center gap-2 font-medium text-sm">
                          <Mic className="h-4 w-4 text-cinema" />
                          {recordingId === s.castMemberId
                            ? "Listening…"
                            : s.source === "mic" && s.hasSample
                              ? "Mic sample saved"
                              : "Use microphone"}
                        </span>
                        <span className="text-xs text-fg-muted">
                          Record ~10–15s in this browser
                        </span>
                      </button>

                      <button
                        type="button"
                        disabled={disabled}
                        onClick={() => fileRefs.current[s.castMemberId]?.click()}
                        className={cn(
                          "flex flex-col items-start gap-1 rounded-[var(--radius-md)] border px-3 py-3 text-left transition-colors",
                          s.source === "upload" && s.hasSample
                            ? "border-cinema/50 bg-cinema/10"
                            : "border-border bg-bg-elevated hover:border-border-strong",
                        )}
                      >
                        <span className="flex items-center gap-2 font-medium text-sm">
                          {s.sampleLabel?.startsWith("Video") ? (
                            <Video className="h-4 w-4 text-cinema" />
                          ) : (
                            <Upload className="h-4 w-4 text-cinema" />
                          )}
                          {s.source === "upload" && s.hasSample
                            ? "File uploaded"
                            : "Upload audio or video"}
                        </span>
                        <span className="text-xs text-fg-muted">
                          Voice memo, mp3, m4a, or phone video
                        </span>
                      </button>
                    </div>
                    <input
                      ref={(el) => {
                        fileRefs.current[s.castMemberId] = el;
                      }}
                      type="file"
                      accept="audio/*,video/*,.mp3,.m4a,.wav,.aac,.mp4,.mov,.webm"
                      className="sr-only"
                      onChange={(e) => {
                        onFile(s.castMemberId, e.target.files?.[0]);
                        e.target.value = "";
                      }}
                    />
                    {s.hasSample && (
                      <p className="text-xs text-success flex items-center gap-1.5">
                        <Check className="h-3.5 w-3.5" />
                        Template ready · {s.sampleLabel}
                        {s.source === "mic" ? " (mic)" : s.source === "upload" ? " (upload)" : ""}
                      </p>
                    )}
                  </div>
                )}
              </div>
            ))}
            <p className="text-xs text-fg-subtle leading-relaxed">
              No script required — any clear speech works. Demo: mic is simulated; uploads stay
              in this browser only.
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
            disabled={disabled}
            onClick={onSkip}
            className="sm:mr-auto"
          >
            <SkipForward className="h-4 w-4" />
            Skip — stock voices
          </Button>
          <Button type="button" disabled={disabled || !ready} onClick={onContinue}>
            {extra > 0
              ? `Continue with voice · +${extra} cr`
              : "Continue to estimate"}
          </Button>
        </div>
        {voice.enabled && !ready && (
          <p className="text-xs text-danger">
            For each cloned role, pick mic or upload a sample — or skip the add-on.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
