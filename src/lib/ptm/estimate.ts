export type ProductionEstimate = {
  words: number;
  pagesApprox: number;
  scenes: number;
  runtimeMinSec: number;
  runtimeMaxSec: number;
  /** Credits to render the full cut (includes voice add-on if any) */
  creditsFull: number;
  /** Picture/composite only — before voice add-on */
  creditsBase: number;
  /** Extra for personal voice clones */
  creditsVoice: number;
  /** What full would cost without cache discount (base only) */
  creditsIfFromScratch: number;
  freeSampleScenes: number;
  confidence: "low" | "medium" | "high";
  summary: string;
  sourceKind: "classic" | "custom";
  cachedPipeline: boolean;
  personalizedRoles: number;
  voiceRoles: number;
};

const WORDS_PER_PAGE = 250;

export function estimateProduction(opts: {
  text: string;
  sourceKind: "classic" | "custom";
  sceneCount?: number;
  personalizedRoles?: number;
  voiceCredits?: number;
  voiceRoles?: number;
}): ProductionEstimate {
  const { text, sourceKind } = opts;
  const words = text.trim().split(/\s+/).filter(Boolean).length;
  const pagesApprox = Math.max(1, Math.round((words / WORDS_PER_PAGE) * 10) / 10);
  const scenes =
    opts.sceneCount ?? Math.min(12, Math.max(3, Math.round(words / 120) || 3));

  const runtimeMinSec = scenes * 4;
  const runtimeMaxSec = scenes * 7;
  const personalizedRoles = opts.personalizedRoles ?? 0;
  const voiceRoles = opts.voiceRoles ?? 0;
  const creditsVoice = opts.voiceCredits ?? 0;

  let creditsIfFromScratch = Math.max(12, 6 + scenes * 3 + Math.round(pagesApprox * 2));
  creditsIfFromScratch += personalizedRoles * 2;

  let creditsBase = creditsIfFromScratch;
  const cachedPipeline = sourceKind === "classic";

  if (cachedPipeline) {
    creditsBase = Math.max(6, 4 + personalizedRoles * 3 + Math.round(scenes * 0.5));
  }

  const creditsFull = creditsBase + creditsVoice;

  const confidence: ProductionEstimate["confidence"] =
    words < 80 ? "low" : words < 400 ? "medium" : "high";

  let summary = cachedPipeline
    ? personalizedRoles > 0
      ? `Cached classic — mainly character composite (${personalizedRoles} personalized).`
      : "Cached classic — pipeline pre-built; generate is mostly assembly."
    : "Custom page — screenplay, storyboard, and film from scratch.";

  if (voiceRoles > 0) {
    summary += ` Voice add-on: ${voiceRoles} personal voice${voiceRoles === 1 ? "" : "s"} (+${creditsVoice} cr).`;
  } else {
    summary += " Stock voices included at no extra cost.";
  }

  return {
    words,
    pagesApprox,
    scenes,
    runtimeMinSec,
    runtimeMaxSec,
    creditsFull,
    creditsBase,
    creditsVoice,
    creditsIfFromScratch,
    freeSampleScenes: 1,
    confidence,
    summary,
    sourceKind,
    cachedPipeline,
    personalizedRoles,
    voiceRoles,
  };
}

export function estimateFromSource(text: string): ProductionEstimate {
  return estimateProduction({ text, sourceKind: "custom" });
}

export function formatRuntimeRange(minSec: number, maxSec: number) {
  const fmt = (s: number) => {
    if (s < 60) return `${s}s`;
    const m = Math.floor(s / 60);
    const r = s % 60;
    return r ? `${m}m ${r}s` : `${m}m`;
  };
  return `${fmt(minSec)} – ${fmt(maxSec)}`;
}
