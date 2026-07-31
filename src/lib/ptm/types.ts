import type { StoryboardShot } from "@/data/classics";
import type { CastMember } from "./characters";
import type { ProductionEstimate } from "./estimate";
import type { VoiceAddon } from "./voice";

export type ProjectStage = "source" | "screenplay" | "storyboard" | "film";

/**
 * book → cast → voice (optional) → estimate → confirm → generate → edit
 */
export type WizardStep = "cast" | "voice" | "estimate" | "confirm" | "done";

export type ProjectStatus = "setup" | "sample" | "generating" | "ready";

export type SourceKind = "classic" | "custom";

export type FilmProject = {
  id: string;
  title: string;
  author: string;
  genre: string;
  sourceText: string;
  screenplay: string;
  shots: StoryboardShot[];
  stage: ProjectStage;
  screenplayLocked: boolean;
  status: ProjectStatus;
  wizardStep: WizardStep;
  sourceKind: SourceKind;
  progress: number;
  progressLabel: string;
  estimate?: ProductionEstimate;
  cast: CastMember[];
  castingConfirmed: boolean;
  /** Optional paid voice-clone add-on */
  voice: VoiceAddon;
  unlockedShots: number;
  classicId?: string;
  createdAt: string;
  updatedAt: string;
  stars: number;
};

export function resumeStage(project: FilmProject): ProjectStage {
  if (project.wizardStep !== "done" && project.status === "setup") return "film";
  if (
    project.status === "ready" ||
    project.status === "generating" ||
    project.status === "sample" ||
    project.status === "setup"
  ) {
    return "film";
  }
  if (project.screenplayLocked && project.stage === "screenplay") return "storyboard";
  return project.stage;
}

export function resumeActionLabel(project: FilmProject): string {
  if (project.status === "setup") {
    if (project.wizardStep === "cast") return "Pick characters";
    if (project.wizardStep === "voice") return "Voice (optional)";
    if (project.wizardStep === "estimate") return "View estimate";
    if (project.wizardStep === "confirm") return "Confirm & generate";
    return "Continue setup";
  }
  if (project.status === "sample") return "Watch free scene";
  if (project.status === "ready") return "Watch movie";
  if (project.status === "generating") return "Open render";
  return "Open project";
}
