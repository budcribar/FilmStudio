import { createFileRoute, Link } from "@tanstack/react-router";
import {
  ChevronLeft,
  ChevronRight,
  Coins,
  Film,
  Gift,
  Mic,
  Play,
  RefreshCw,
  Users,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { CastingPanel } from "@/components/casting-panel";
import { CreditsButton } from "@/components/credits-dialog";
import { VoicePanel } from "@/components/voice-panel";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { relationLabel } from "@/lib/ptm/characters";
import { formatRuntimeRange } from "@/lib/ptm/estimate";
import { createObjectUrlSafe } from "@/lib/ptm/media/client-media-store";
import { useProjects } from "@/lib/ptm/store";
import { voiceRolesCount } from "@/lib/ptm/voice";
import { useWallet } from "@/lib/ptm/wallet";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/studio/$projectId")({
  component: ProjectStudioPage,
});

const wizardLabels = [
  { id: "book", label: "1 · Book" },
  { id: "cast", label: "2 · Cast" },
  { id: "voice", label: "3 · Voice" },
  { id: "estimate", label: "4 · Estimate" },
  { id: "confirm", label: "5 · Confirm" },
  { id: "done", label: "6 · Movie" },
] as const;

function ProjectStudioPage() {
  const { projectId } = Route.useParams();
  const project = useProjects((s) => s.projects.find((p) => p.id === projectId));
  const setCast = useProjects((s) => s.setCast);
  const setVoice = useProjects((s) => s.setVoice);
  const confirmCasting = useProjects((s) => s.confirmCasting);
  const skipVoice = useProjects((s) => s.skipVoice);
  const continueFromVoice = useProjects((s) => s.continueFromVoice);
  const setWizardStep = useProjects((s) => s.setWizardStep);
  const openForResume = useProjects((s) => s.openForResume);
  const runFreeSample = useProjects((s) => s.runFreeSample);
  const runFullGenerate = useProjects((s) => s.runFullGenerate);
  const runRerender = useProjects((s) => s.runRerender);

  const credits = useWallet((s) => s.credits);
  const [shotIndex, setShotIndex] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [actionError, setActionError] = useState("");
  const [busyAction, setBusyAction] = useState(false);
  const [voUrl, setVoUrl] = useState<string | null>(null);
  const didAutoPlay = useRef(false);

  useEffect(() => {
    if (projectId) openForResume(projectId);
  }, [projectId, openForResume]);

  const unlocked = project?.unlockedShots ?? 0;
  const playableShots = useMemo(() => {
    if (!project || unlocked <= 0) return [];
    return project.shots.slice(0, unlocked);
  }, [project, unlocked]);

  useEffect(() => {
    if (!playing || playableShots.length === 0) return;
    if (shotIndex >= playableShots.length - 1) {
      setPlaying(false);
      return;
    }
    const dur = (playableShots[shotIndex]?.durationSec ?? 5) * 1000;
    const t = window.setTimeout(() => setShotIndex((i) => i + 1), Math.min(dur, 2500));
    return () => window.clearTimeout(t);
  }, [playing, shotIndex, playableShots]);

  useEffect(() => {
    if (
      (project?.status === "ready" || project?.status === "sample") &&
      !didAutoPlay.current &&
      (project.unlockedShots ?? 0) > 0
    ) {
      didAutoPlay.current = true;
      setShotIndex(0);
      setPlaying(true);
    }
    if (project?.status === "generating" || project?.status === "setup") {
      didAutoPlay.current = false;
    }
  }, [project?.status, project?.unlockedShots]);

  // Load client-stitched VO mp3 when present
  useEffect(() => {
    let revoked: string | null = null;
    const id = project?.voice?.stitchedVoMediaId;
    if (!id) {
      setVoUrl(null);
      return;
    }
    void createObjectUrlSafe(id).then((url) => {
      if (url) {
        revoked = url;
        setVoUrl(url);
      }
    });
    return () => {
      if (revoked) URL.revokeObjectURL(revoked);
    };
  }, [project?.voice?.stitchedVoMediaId]);

  if (!project) {
    return (
      <div className="mx-auto max-w-lg px-4 py-20 text-center">
        <h1 className="font-display text-xl font-semibold">Project not found</h1>
        <Button asChild className="mt-6">
          <Link to="/studio">Pick a book</Link>
        </Button>
      </div>
    );
  }

  const step = project.wizardStep;
  const estimate = project.estimate;
  const fullCost = estimate?.creditsFull ?? 15;
  const baseCost = estimate?.creditsBase ?? fullCost;
  const voiceCost = estimate?.creditsVoice ?? 0;
  const scratchCost = estimate?.creditsIfFromScratch ?? fullCost;
  const canAfford = credits >= fullCost;
  const namedCast = project.cast.filter(
    (c) => c.selected && (c.displayName.trim() || c.photoDataUrl),
  );
  const voiceN = voiceRolesCount(project.voice);
  const currentShot = playableShots[shotIndex] ?? playableShots[0];
  const isSetup = project.status === "setup";
  const isGenerating = project.status === "generating";
  const isSample = project.status === "sample";
  const isReady = project.status === "ready";

  const activeWizardIndex =
    step === "cast"
      ? 1
      : step === "voice"
        ? 2
        : step === "estimate"
          ? 3
          : step === "confirm"
            ? 4
            : 5;

  async function onFreeSample() {
    setActionError("");
    setBusyAction(true);
    setPlaying(false);
    try {
      const res = await runFreeSample(projectId);
      if (!res.ok) setActionError(res.reason);
    } finally {
      setBusyAction(false);
    }
  }

  async function onFull() {
    setActionError("");
    setBusyAction(true);
    setPlaying(false);
    try {
      const res = await runFullGenerate(projectId);
      if (!res.ok) {
        setActionError(
          res.reason === "Not enough credits"
            ? `Need ${res.needCredits ?? fullCost} more credits.`
            : res.reason,
        );
      }
    } finally {
      setBusyAction(false);
    }
  }

  async function onRerender() {
    setActionError("");
    setBusyAction(true);
    setPlaying(false);
    didAutoPlay.current = false;
    try {
      const res = await runRerender(projectId);
      if (!res.ok) setActionError(res.reason);
    } finally {
      setBusyAction(false);
    }
  }

  return (
    <div className="mx-auto max-w-6xl px-4 sm:px-6 py-8 sm:py-10">
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4 mb-6">
        <div>
          <div className="flex flex-wrap items-center gap-2 mb-2">
            <Button asChild variant="ghost" size="sm" className="-ml-2">
              <Link to="/studio">
                <ChevronLeft className="h-4 w-4" />
                Books
              </Link>
            </Button>
            <Badge variant={project.sourceKind === "classic" ? "success" : "cinema"}>
              {project.sourceKind === "classic" ? "Cached classic" : "Custom"}
            </Badge>
            {voiceN > 0 && (
              <Badge variant="accent" className="gap-1">
                <Mic className="h-3 w-3" />
                Voice add-on
              </Badge>
            )}
            {isReady && <Badge variant="success">Movie ready</Badge>}
            {isSample && <Badge variant="accent">Free sample</Badge>}
            {isGenerating && <Badge variant="cinema">Generating…</Badge>}
          </div>
          <h1 className="font-display text-2xl sm:text-3xl font-semibold tracking-tight">
            {project.title}
          </h1>
          <p className="text-sm text-fg-muted mt-1">
            {project.author} · {project.genre}
          </p>
        </div>
        <CreditsButton />
      </div>

      <div className="flex gap-1 overflow-x-auto pb-1 mb-6">
        {wizardLabels.map((w, i) => {
          const done = i < activeWizardIndex || (!isSetup && w.id === "done");
          const active =
            (w.id === "cast" && step === "cast") ||
            (w.id === "voice" && step === "voice") ||
            (w.id === "estimate" && step === "estimate") ||
            (w.id === "confirm" && step === "confirm") ||
            (w.id === "done" && step === "done");
          return (
            <div
              key={w.id}
              className={cn(
                "rounded-[var(--radius-sm)] border px-2.5 py-1.5 text-[11px] sm:text-xs font-medium whitespace-nowrap",
                active
                  ? "border-cinema/40 bg-cinema/10 text-fg"
                  : done
                    ? "border-border bg-bg-subtle text-fg-muted"
                    : "border-transparent text-fg-subtle",
              )}
            >
              {w.label}
            </div>
          );
        })}
      </div>

      {actionError && (
        <div className="mb-4 rounded-[var(--radius-md)] border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <span>{actionError}</span>
          {actionError.toLowerCase().includes("credit") && <CreditsButton />}
        </div>
      )}

      {isSetup && step === "cast" && (
        <CastingPanel
          cast={project.cast}
          sourceKind={project.sourceKind}
          disabled={busyAction}
          onChange={(cast) => setCast(projectId, cast)}
          onContinue={() => {
            confirmCasting(projectId);
            setActionError("");
          }}
        />
      )}

      {isSetup && step === "voice" && (
        <div className="space-y-3">
          <VoicePanel
            voice={project.voice}
            projectId={projectId}
            disabled={busyAction}
            onChange={(v) => setVoice(projectId, v)}
            onSkip={() => {
              skipVoice(projectId);
              setActionError("");
            }}
            onContinue={() => {
              continueFromVoice(projectId);
              setActionError("");
            }}
          />
          <Button variant="ghost" size="sm" onClick={() => setWizardStep(projectId, "cast")}>
            Back to cast
          </Button>
        </div>
      )}

      {isSetup && step === "estimate" && estimate && (
        <Card className="border-border-strong">
          <CardContent className="p-5 sm:p-6 space-y-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.12em] text-fg-subtle mb-1">
                Step 4 · Estimate
              </p>
              <h2 className="font-display text-xl font-semibold">Production estimate</h2>
              <p className="text-sm text-fg-muted mt-2 leading-relaxed">{estimate.summary}</p>
            </div>

            <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-3">
              <Stat
                label="Runtime range"
                value={formatRuntimeRange(estimate.runtimeMinSec, estimate.runtimeMaxSec)}
              />
              <Stat label="Picture / cast" value={`${baseCost} cr`} />
              <Stat
                label="Voice add-on"
                value={voiceCost > 0 ? `+${voiceCost} cr` : "None"}
              />
              <Stat label="Total full cut" value={`${fullCost} cr`} />
            </div>

            {estimate.cachedPipeline && (
              <div className="rounded-[var(--radius-md)] border border-success/30 bg-success/10 px-4 py-3 text-sm text-success">
                Cached classic base is ~{Math.max(0, scratchCost - baseCost)} cr less than
                from-scratch. Voice is always an optional extra on top.
              </div>
            )}

            {namedCast.length > 0 && (
              <div>
                <p className="text-xs uppercase tracking-wide text-fg-subtle mb-2">Cast</p>
                <ul className="space-y-1.5">
                  {namedCast.map((c) => (
                    <li key={c.id} className="text-sm text-fg-muted">
                      <span className="text-fg font-medium">{c.displayName || "Photo"}</span> as{" "}
                      {c.roleInStory} · {relationLabel(c.relation)}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {voiceN > 0 && (
              <div className="flex items-start gap-2 text-sm text-fg-muted">
                <Mic className="h-4 w-4 text-cinema shrink-0 mt-0.5" />
                {voiceN} personal voice{voiceN === 1 ? "" : "s"} — mock MP3 via client media
                store.
              </div>
            )}

            <div className="flex flex-col sm:flex-row gap-2">
              <Button variant="secondary" onClick={() => setWizardStep(projectId, "voice")}>
                Back to voice
              </Button>
              <Button
                className="sm:ml-auto"
                onClick={() => {
                  setWizardStep(projectId, "confirm");
                  setActionError("");
                }}
              >
                Continue to confirm
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {isSetup && step === "confirm" && estimate && (
        <Card className="border-border-strong">
          <CardContent className="p-5 sm:p-6 space-y-5">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.12em] text-fg-subtle mb-1">
                Step 5 · Confirm
              </p>
              <h2 className="font-display text-xl font-semibold">Ready to generate</h2>
              <p className="text-sm text-fg-muted mt-2 leading-relaxed">
                {project.title} · {estimate.scenes} scenes ·{" "}
                {formatRuntimeRange(estimate.runtimeMinSec, estimate.runtimeMaxSec)}
              </p>
              <p className="text-sm text-fg mt-2 tabular-nums">
                <span className="font-semibold">{fullCost} credits</span>
                {voiceCost > 0
                  ? ` (${baseCost} picture + ${voiceCost} voice)`
                  : " (stock voices)"}{" "}
                · you have {credits}
              </p>
            </div>

            <div className="grid gap-2">
              <Button
                variant="secondary"
                size="lg"
                disabled={busyAction}
                onClick={() => void onFreeSample()}
              >
                <Gift className="h-4 w-4" />
                Free: generate 1 sample scene
              </Button>
              <Button size="lg" disabled={busyAction} onClick={() => void onFull()}>
                <Coins className="h-4 w-4" />
                {canAfford
                  ? `Generate full movie · ${fullCost} credits`
                  : `Full movie · ${fullCost} credits (need more)`}
              </Button>
              {!canAfford && (
                <div className="flex justify-center">
                  <CreditsButton />
                </div>
              )}
            </div>

            <Button variant="ghost" size="sm" onClick={() => setWizardStep(projectId, "estimate")}>
              Back to estimate
            </Button>
          </CardContent>
        </Card>
      )}

      {(isGenerating || isSample || isReady) && (
        <div className="grid lg:grid-cols-[1.35fr_1fr] gap-6">
          <Card className="overflow-hidden">
            <div
              className={cn(
                "aspect-video bg-gradient-to-br relative flex flex-col film-grain",
                currentShot?.palette ?? "from-bg-subtle to-bg",
              )}
            >
              <div className="relative z-10 flex-1 flex flex-col items-center justify-center p-6 sm:p-10 text-center">
                {isGenerating ? (
                  <div className="w-full max-w-sm space-y-4">
                    <p className="font-display text-lg font-medium">Generating…</p>
                    <Progress value={project.progress} />
                    <p className="text-sm text-fg-muted">{project.progressLabel}</p>
                  </div>
                ) : (
                  <>
                    <p className="text-xs uppercase tracking-[0.16em] text-fg-subtle mb-3">
                      {isSample ? "Free sample" : "Your movie"} · Shot {shotIndex + 1}/
                      {playableShots.length}
                    </p>
                    {namedCast[0] && (
                      <p className="text-xs text-cinema mb-2 flex items-center justify-center gap-2">
                        <Users className="h-3 w-3" />
                        {namedCast[0].displayName || namedCast[0].roleInStory}
                        {voiceN > 0 && (
                          <>
                            <Mic className="h-3 w-3" />
                            personal voice
                          </>
                        )}
                      </p>
                    )}
                    <p className="font-mono text-xs text-cinema mb-2">{currentShot?.heading}</p>
                    <p className="font-display text-xl sm:text-2xl font-medium max-w-md text-balance">
                      {currentShot?.visual}
                    </p>
                    {currentShot?.dialogue && (
                      <p className="mt-4 text-sm text-fg-muted italic max-w-sm">
                        “{currentShot.dialogue}”
                      </p>
                    )}
                  </>
                )}
              </div>
              {!isGenerating && playableShots.length > 0 && (
                <div className="relative z-10 flex items-center justify-between gap-2 p-3 border-t border-white/10 bg-black/30">
                  <Button
                    size="sm"
                    variant="secondary"
                    disabled={shotIndex === 0}
                    onClick={() => {
                      setPlaying(false);
                      setShotIndex((i) => Math.max(0, i - 1));
                    }}
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    size="sm"
                    onClick={() => {
                      if (shotIndex >= playableShots.length - 1) setShotIndex(0);
                      setPlaying((p) => !p);
                    }}
                  >
                    <Play className="h-3.5 w-3.5 fill-current" />
                    {playing ? "Playing…" : "Play"}
                  </Button>
                  <Button
                    size="sm"
                    variant="secondary"
                    disabled={shotIndex >= playableShots.length - 1}
                    onClick={() => {
                      setPlaying(false);
                      setShotIndex((i) => Math.min(playableShots.length - 1, i + 1));
                    }}
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                </div>
              )}
            </div>
          </Card>

          <div className="space-y-4">
            {voUrl && (
              <Card>
                <CardContent className="p-5 space-y-2">
                  <h2 className="font-display font-semibold text-sm flex items-center gap-2">
                    <Mic className="h-4 w-4 text-cinema" />
                    Client VO track (mock MP3)
                  </h2>
                  <p className="text-xs text-fg-muted">
                    Stored in browser media DB · id{" "}
                    <span className="font-mono text-fg-subtle">
                      {project.voice.stitchedVoMediaId}
                    </span>
                  </p>
                  <audio controls src={voUrl} className="w-full mt-1" preload="metadata" />
                </CardContent>
              </Card>
            )}
            {isSample && (
              <Card>
                <CardContent className="p-5 space-y-3">
                  <h2 className="font-display font-semibold">Sample unlocked</h2>
                  <p className="text-sm text-fg-muted">
                    Unlock the full cut for {fullCost} credits
                    {voiceCost > 0 ? " (includes voice add-on)." : "."}
                  </p>
                  <Button disabled={busyAction} onClick={() => void onFull()}>
                    <Film className="h-4 w-4" />
                    Generate full movie · {fullCost} cr
                  </Button>
                </CardContent>
              </Card>
            )}
            {isReady && (
              <Card className="border-success/25">
                <CardContent className="p-5 space-y-3">
                  <h2 className="font-display font-semibold">Movie ready</h2>
                  <p className="text-sm text-fg-muted">
                    Edit cast or voice, then re-render.
                  </p>
                  <Button variant="secondary" onClick={() => setWizardStep(projectId, "cast")}>
                    <Users className="h-4 w-4" />
                    Edit cast
                  </Button>
                  <Button variant="secondary" onClick={() => setWizardStep(projectId, "voice")}>
                    <Mic className="h-4 w-4" />
                    Edit voice
                  </Button>
                  <Button disabled={busyAction} onClick={() => void onRerender()}>
                    <RefreshCw className="h-4 w-4" />
                    Re-render
                  </Button>
                </CardContent>
              </Card>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[var(--radius-md)] border border-border bg-bg px-3 py-2.5">
      <p className="text-[11px] uppercase tracking-wide text-fg-subtle">{label}</p>
      <p className="font-display font-semibold mt-0.5 tabular-nums">{value}</p>
    </div>
  );
}
