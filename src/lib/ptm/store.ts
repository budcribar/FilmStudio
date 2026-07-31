import { create } from "zustand";
import { persist } from "zustand/middleware";
import { classics, type StoryboardShot } from "@/data/classics";
import {
  castFromClassicCharacters,
  personalizedCount,
  suggestCastFromSource,
  type CastMember,
} from "./characters";
import { estimateProduction } from "./estimate";
import type { FilmProject, ProjectStage, ProjectStatus, WizardStep } from "./types";
import { resumeStage } from "./types";
import {
  emptyVoiceAddon,
  syncVoiceFromCast,
  voiceCreditsExtra,
  voiceRolesCount,
  type VoiceAddon,
} from "./voice";
import { useWallet } from "./wallet";

/** Bump when FilmProject shape changes; normalize() migrates older localStorage. */
export const PROJECT_STORE_VERSION = 2;

function uid() {
  return `p_${Math.random().toString(36).slice(2, 10)}`;
}

function now() {
  return new Date().toISOString();
}

function shotsFromCustom(_title: string, text: string, sceneTarget?: number): StoryboardShot[] {
  const lines = text
    .split(/\n+/)
    .map((l) => l.trim())
    .filter(Boolean);
  const count = Math.min(12, Math.max(3, sceneTarget ?? 4));
  const sample = lines.slice(0, count);
  while (sample.length < count) sample.push("A quiet beat holds on the frame.");
  const palettes = [
    "from-[#1a1c22] to-[#0a0b0d]",
    "from-[#1c1820] to-[#0c0a10]",
    "from-[#161a1e] to-[#080a0c]",
    "from-[#1a1614] to-[#0c0a08]",
  ];
  return sample.map((line, i) => ({
    id: `c${i + 1}`,
    scene: i + 1,
    heading: i === 0 ? "FADE IN" : `SCENE ${i + 1}`,
    visual: line.slice(0, 140),
    dialogue: i % 2 === 0 ? line.slice(0, 80) : undefined,
    durationSec: 5 + (i % 3),
    palette: palettes[i % palettes.length]!,
  }));
}

function screenplayFromCustom(title: string, text: string) {
  const body = text.slice(0, 900);
  return `Title: ${title.toUpperCase()}
Credit: Original adaptation
Draft date: ${new Date().toISOString().slice(0, 10)}

FADE IN:

INT. UNSPECIFIED LOCATION - DAY

${body}

NARRATOR (V.O.)
The page becomes a picture. The picture becomes a cut.

FADE OUT.`;
}

const VALID_STATUS: ProjectStatus[] = ["setup", "sample", "generating", "ready"];
const VALID_WIZARD: WizardStep[] = ["cast", "voice", "estimate", "confirm", "done"];

function buildEstimate(project: FilmProject) {
  const voice = project.voice ?? emptyVoiceAddon();
  return estimateProduction({
    text: project.sourceText,
    sourceKind: project.sourceKind,
    sceneCount: project.shots.length || undefined,
    personalizedRoles: personalizedCount(project.cast),
    voiceCredits: voiceCreditsExtra(voice),
    voiceRoles: voiceRolesCount(voice),
  });
}

function normalize(project: FilmProject): FilmProject {
  let status: ProjectStatus = VALID_STATUS.includes(project.status)
    ? project.status
    : "setup";
  if ((project.status as string) === "estimate") status = "setup";
  if ((project.status as string) === "draft") status = "setup";

  let wizardStep: WizardStep = VALID_WIZARD.includes(project.wizardStep)
    ? project.wizardStep
    : status === "setup"
      ? "cast"
      : "done";

  // Legacy: projects that jumped cast → estimate without voice
  if (
    status === "setup" &&
    wizardStep === "estimate" &&
    project.castingConfirmed &&
    !(project as { voice?: VoiceAddon }).voice
  ) {
    // leave on estimate; voice will default to stock via emptyVoiceAddon
  }

  const cast =
    project.cast?.length > 0
      ? project.cast.map((c) => ({
          ...c,
          selected: c.selected ?? !!(c.displayName || c.photoDataUrl),
        }))
      : suggestCastFromSource(project.sourceText || "", project.title);

  const voice = syncVoiceFromCast(
    cast,
    project.voice ?? emptyVoiceAddon(),
  );

  const base: FilmProject = {
    ...project,
    status,
    wizardStep,
    sourceKind: project.sourceKind ?? (project.classicId ? "classic" : "custom"),
    screenplayLocked: project.screenplayLocked ?? true,
    castingConfirmed: project.castingConfirmed ?? false,
    unlockedShots: project.unlockedShots ?? 0,
    cast,
    voice,
  };
  return {
    ...base,
    estimate: project.estimate ?? buildEstimate(base),
  };
}

type Store = {
  projects: FilmProject[];
  createFromClassicBook: (classicId: string) => string;
  createFromCustomBook: (title: string, text: string) => string;
  updateProject: (id: string, patch: Partial<FilmProject>) => void;
  setCast: (id: string, cast: CastMember[]) => void;
  setVoice: (id: string, voice: VoiceAddon) => void;
  setWizardStep: (id: string, step: WizardStep) => void;
  confirmCasting: (id: string) => void;
  skipVoice: (id: string) => void;
  continueFromVoice: (id: string) => void;
  deleteProject: (id: string) => void;
  setStage: (id: string, stage: ProjectStage) => void;
  lockScreenplay: (id: string) => void;
  unlockScreenplay: (id: string) => void;
  openForResume: (id: string) => ProjectStage;
  runFreeSample: (id: string) => Promise<{ ok: true } | { ok: false; reason: string }>;
  runFullGenerate: (
    id: string,
  ) => Promise<{ ok: true } | { ok: false; reason: string; needCredits?: number }>;
  runRerender: (id: string) => Promise<{ ok: true } | { ok: false; reason: string }>;
  toggleStar: (id: string) => void;
};

async function simulatePipeline(
  update: (patch: Partial<FilmProject>) => void,
  labels: string[],
) {
  update({
    status: "generating",
    progress: 0,
    progressLabel: "Starting…",
    stage: "film",
    screenplayLocked: true,
    wizardStep: "done",
  });
  for (let i = 0; i < labels.length; i++) {
    await new Promise((r) => setTimeout(r, 360 + Math.random() * 260));
    update({
      progress: Math.round(((i + 1) / labels.length) * 100),
      progressLabel: labels[i],
      status: "generating",
    });
  }
}

function castLine(project: FilmProject) {
  const named = project.cast.filter(
    (c) => c.selected && (c.displayName.trim() || c.photoDataUrl),
  );
  if (!named.length) {
    return project.sourceKind === "classic"
      ? "Using cached classic cast…"
      : "Casting default looks…";
  }
  return `Compositing ${named.map((c) => c.displayName.trim() || c.roleInStory).join(", ")}…`;
}

function voiceLine(project: FilmProject) {
  const n = voiceRolesCount(project.voice);
  if (n === 0) return "Mixing stock voices…";
  return `Cloning ${n} personal voice${n === 1 ? "" : "s"}…`;
}

export const useProjects = create<Store>()(
  persist(
    (set, get) => ({
      projects: [],

      createFromClassicBook: (classicId) => {
        const classic = classics.find((c) => c.id === classicId);
        if (!classic) throw new Error("Unknown classic");
        const id = uid();
        const cast = castFromClassicCharacters(classic.characters);
        const voice = syncVoiceFromCast(cast, emptyVoiceAddon());
        const project: FilmProject = {
          id,
          title: classic.title,
          author: classic.author,
          genre: classic.genre,
          sourceText: classic.excerpt,
          screenplay: classic.screenplay,
          shots: classic.shots,
          stage: "film",
          screenplayLocked: true,
          status: "setup",
          wizardStep: "cast",
          sourceKind: "classic",
          progress: 0,
          progressLabel: "",
          cast,
          castingConfirmed: false,
          voice,
          unlockedShots: 0,
          classicId: classic.id,
          createdAt: now(),
          updatedAt: now(),
          stars: 0,
        };
        project.estimate = buildEstimate(project);
        set((s) => ({ projects: [project, ...s.projects] }));
        return id;
      },

      createFromCustomBook: (title, text) => {
        const id = uid();
        const cleanTitle = title.trim() || "Untitled Adaptation";
        const source = text.trim();
        const cast = suggestCastFromSource(source, cleanTitle);
        const voice = syncVoiceFromCast(cast, emptyVoiceAddon());
        const shots = shotsFromCustom(cleanTitle, source);
        const project: FilmProject = {
          id,
          title: cleanTitle,
          author: "You",
          genre: "Original",
          sourceText: source,
          screenplay: screenplayFromCustom(cleanTitle, source),
          shots,
          stage: "film",
          screenplayLocked: true,
          status: "setup",
          wizardStep: "cast",
          sourceKind: "custom",
          progress: 0,
          progressLabel: "",
          cast,
          castingConfirmed: false,
          voice,
          unlockedShots: 0,
          createdAt: now(),
          updatedAt: now(),
          stars: 0,
        };
        project.estimate = buildEstimate(project);
        set((s) => ({ projects: [project, ...s.projects] }));
        return id;
      },

      updateProject: (id, patch) => {
        set((s) => ({
          projects: s.projects.map((p) =>
            p.id === id ? normalize({ ...p, ...patch, updatedAt: now() }) : normalize(p),
          ),
        }));
      },

      setCast: (id, cast) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        const voice = syncVoiceFromCast(cast, project.voice);
        const next = { ...project, cast, voice, castingConfirmed: false };
        get().updateProject(id, {
          cast,
          voice,
          castingConfirmed: false,
          estimate: buildEstimate(next),
        });
      },

      setVoice: (id, voice) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        const next = { ...project, voice };
        get().updateProject(id, {
          voice,
          estimate: buildEstimate(next),
        });
      },

      setWizardStep: (id, step) => {
        // Editing cast/voice from a finished project returns to setup without wiping unlocks
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        if (step === "done") {
          get().updateProject(id, { wizardStep: "done" });
          return;
        }
        const keepStatus =
          project.status === "ready" || project.status === "sample"
            ? project.status
            : "setup";
        get().updateProject(id, {
          wizardStep: step,
          status: keepStatus === "ready" || keepStatus === "sample" ? "setup" : "setup",
        });
      },

      confirmCasting: (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        const voice = syncVoiceFromCast(project.cast, project.voice);
        get().updateProject(id, {
          castingConfirmed: true,
          voice,
          estimate: buildEstimate({ ...project, castingConfirmed: true, voice }),
          wizardStep: "voice",
          status: "setup",
        });
      },

      skipVoice: (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        const voice: VoiceAddon = {
          enabled: false,
          samples: project.voice.samples.map((s) => ({
            ...s,
            enabled: false,
            hasSample: false,
            source: null,
          })),
        };
        get().updateProject(id, {
          voice,
          estimate: buildEstimate({ ...project, voice }),
          wizardStep: "estimate",
          status: "setup",
        });
      },

      continueFromVoice: (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return;
        get().updateProject(id, {
          estimate: buildEstimate(project),
          wizardStep: "estimate",
          status: "setup",
        });
      },

      deleteProject: (id) => {
        set((s) => ({ projects: s.projects.filter((p) => p.id !== id) }));
      },

      setStage: (id, stage) => {
        get().updateProject(id, { stage });
      },

      lockScreenplay: (id) => {
        get().updateProject(id, { screenplayLocked: true, stage: "storyboard" });
      },

      unlockScreenplay: (id) => {
        get().updateProject(id, { screenplayLocked: false, stage: "screenplay" });
      },

      openForResume: (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return "source";
        const stage = resumeStage(normalize(project));
        if (stage !== project.stage) get().updateProject(id, { stage });
        return stage;
      },

      runFreeSample: async (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return { ok: false, reason: "Project not found" };
        if (project.wizardStep !== "confirm" && project.wizardStep !== "done") {
          return { ok: false, reason: "Finish cast → voice → estimate → confirm first." };
        }
        if (project.status === "sample" || project.unlockedShots >= 1) {
          return { ok: false, reason: "Free sample already generated" };
        }

        const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
        const labels = [
          project.sourceKind === "classic"
            ? "Loading cached screenplay…"
            : "Writing opening scene…",
          castLine(project),
          voiceLine(project),
          "Rendering free sample scene…",
          "Sample ready",
        ];
        await simulatePipeline(update, labels);
        useWallet.getState().markFreeSample(id);
        get().updateProject(id, {
          status: "sample",
          progress: 100,
          progressLabel: "Free sample ready",
          unlockedShots: 1,
          wizardStep: "done",
        });
        return { ok: true };
      },

      runFullGenerate: async (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return { ok: false, reason: "Project not found" };
        if (project.wizardStep !== "confirm" && project.wizardStep !== "done") {
          return { ok: false, reason: "Finish cast → voice → estimate → confirm first." };
        }
        const cost = project.estimate?.creditsFull ?? 15;
        const wallet = useWallet.getState();
        if (!wallet.spend(cost)) {
          return {
            ok: false,
            reason: "Not enough credits",
            needCredits: Math.max(0, cost - wallet.credits),
          };
        }

        const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
        const labels =
          project.sourceKind === "classic"
            ? [
                "Loading cached screenplay…",
                "Loading cached storyboard…",
                castLine(project),
                voiceLine(project),
                "Compositing personalized characters…",
                "Assembling picture lock…",
                "Movie ready",
              ]
            : [
                "Writing screenplay from scratch…",
                "Building storyboard…",
                castLine(project),
                voiceLine(project),
                "Compositing frames…",
                "Mixing picture lock…",
                "Movie ready",
              ];
        await simulatePipeline(update, labels);
        get().updateProject(id, {
          status: "ready",
          progress: 100,
          progressLabel: "Movie ready",
          unlockedShots: project.shots.length,
          wizardStep: "done",
        });
        return { ok: true };
      },

      runRerender: async (id) => {
        const project = get().projects.find((p) => p.id === id);
        if (!project) return { ok: false, reason: "Project not found" };
        const fullCost = project.estimate?.creditsFull ?? 15;
        const owned =
          project.status === "ready" && project.unlockedShots >= project.shots.length;
        const cost = owned ? Math.max(4, Math.round(fullCost / 2)) : fullCost;
        if (!useWallet.getState().spend(cost)) {
          return { ok: false, reason: "Not enough credits" };
        }
        const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
        await simulatePipeline(update, [
          "Applying cast updates…",
          castLine(project),
          voiceLine(project),
          "Re-mixing cut…",
          "Movie ready",
        ]);
        get().updateProject(id, {
          status: "ready",
          progress: 100,
          progressLabel: "Movie ready",
          unlockedShots: project.shots.length,
          wizardStep: "done",
        });
        return { ok: true };
      },

      toggleStar: (id) => {
        set((s) => ({
          projects: s.projects.map((p) =>
            p.id === id
              ? { ...normalize(p), stars: p.stars > 0 ? 0 : 1, updatedAt: now() }
              : normalize(p),
          ),
        }));
      },
    }),
    {
      name: "page-to-movie-projects",
      version: PROJECT_STORE_VERSION,
      migrate: (persisted, fromVersion) => {
        const p = persisted as { projects?: FilmProject[] };
        if (!p?.projects) return persisted as never;
        // v0/v1 → v2: ensure voice + wizardStep
        if (fromVersion < 2) {
          return {
            ...p,
            projects: p.projects.map((proj) => normalize(proj as FilmProject)),
          };
        }
        return persisted as never;
      },
      merge: (persisted, current) => {
        const p = persisted as { projects?: FilmProject[] } | undefined;
        return {
          ...current,
          ...p,
          projects: (p?.projects ?? current.projects).map((proj) =>
            normalize(proj as FilmProject),
          ),
        };
      },
    },
  ),
);
