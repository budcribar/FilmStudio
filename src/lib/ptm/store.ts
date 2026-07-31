/**
 * Client project store — SERVER is source of truth for project/scene/cast/voice metadata.
 * Client only holds media blobs (MP3/MP4/capture) in IndexedDB.
 */
import { create } from "zustand";
import { classics, type StoryboardShot } from "@/data/classics";
import {
  castFromClassicCharacters,
  personalizedCount,
  suggestCastFromSource,
  type CastMember,
} from "./characters";
import { estimateProduction } from "./estimate";
import {
  deleteMyProject,
  getMyProject,
  listMyProjects,
  saveMyProject,
} from "./server/api";
import { runMockVoicePipeline } from "./providers/voice-clone";
import type { FilmProject, ProjectStage, WizardStep } from "./types";
import { resumeStage } from "./types";
import {
  emptyVoiceAddon,
  syncVoiceFromCast,
  voiceAssetsForClone,
  voiceCreditsExtra,
  voiceRolesCount,
  type VoiceAddon,
} from "./voice";
import { useWallet } from "./wallet";

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

/** Strip transient client-only fields before server save */
function forServer(p: FilmProject): FilmProject {
  return {
    ...p,
    cast: p.cast.map(({ photoDataUrl: _x, ...c }) => c),
    voice: {
      ...p.voice,
      samples: p.voice.samples.map((s) => ({
        ...s,
        asset: s.asset
          ? {
              mediaId: s.asset.mediaId,
              mimeType: s.asset.mimeType,
              kind: s.asset.kind,
              fileName: s.asset.fileName,
              durationSec: s.asset.durationSec,
              byteLength: s.asset.byteLength,
            }
          : undefined,
      })),
    },
  };
}

type Store = {
  projects: FilmProject[];
  hydrated: boolean;
  hydrating: boolean;
  saveError: string | null;
  hydrateFromServer: () => Promise<void>;
  createFromClassicBook: (classicId: string) => Promise<string>;
  createFromCustomBook: (title: string, text: string) => Promise<string>;
  updateProject: (id: string, patch: Partial<FilmProject>) => void;
  flushProject: (id: string) => Promise<void>;
  setCast: (id: string, cast: CastMember[]) => void;
  setVoice: (id: string, voice: VoiceAddon) => void;
  setWizardStep: (id: string, step: WizardStep) => void;
  confirmCasting: (id: string) => void;
  skipVoice: (id: string) => void;
  continueFromVoice: (id: string) => void;
  deleteProject: (id: string) => Promise<void>;
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

const saveTimers = new Map<string, ReturnType<typeof setTimeout>>();

async function persistProject(project: FilmProject) {
  const saved = await saveMyProject({ data: { project: forServer(project) } });
  useProjects.setState((s) => ({
    projects: s.projects.map((p) =>
      p.id === saved.id ? { ...saved, cast: mergeCastPreview(p.cast, saved.cast) } : p,
    ),
    saveError: null,
  }));
  return saved;
}

function mergeCastPreview(local: CastMember[], server: CastMember[]): CastMember[] {
  const preview = new Map(local.map((c) => [c.id, c.photoDataUrl]));
  return server.map((c) => ({
    ...c,
    photoDataUrl: preview.get(c.id),
  }));
}

function scheduleSave(id: string) {
  const prev = saveTimers.get(id);
  if (prev) clearTimeout(prev);
  saveTimers.set(
    id,
    setTimeout(() => {
      saveTimers.delete(id);
      const project = useProjects.getState().projects.find((p) => p.id === id);
      if (!project) return;
      void persistProject(project).catch((err) => {
        useProjects.setState({
          saveError: err instanceof Error ? err.message : "Failed to save project",
        });
      });
    }, 400),
  );
}

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
    (c) => c.selected && (c.displayName.trim() || c.photoMediaId || c.photoDataUrl),
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
  return `Client VO · cloning ${n} voice${n === 1 ? "" : "s"} + mock MP3…`;
}

async function prepareVoiceClones(project: FilmProject) {
  const samples = voiceAssetsForClone(project.voice);
  if (!samples.length) return;

  const lines = project.shots
    .filter((s) => s.dialogue)
    .slice(0, 3)
    .map((s, i) => ({
      castMemberId: samples[i % samples.length]!.castMemberId,
      text: s.dialogue!,
    }));

  if (lines.length === 0) {
    lines.push({
      castMemberId: samples[0]!.castMemberId,
      text: `${samples[0]!.displayName} speaks from the page.`,
    });
  }

  const result = await runMockVoicePipeline({
    samples,
    lines,
    projectId: project.id,
  });

  const sampleMap = new Map(samples.map((s) => [s.castMemberId, { ...s }]));
  for (const job of result.jobs) {
    const s = sampleMap.get(job.castMemberId);
    if (s && job.outputMediaId) s.cloneOutputMediaId = job.outputMediaId;
  }
  for (const line of result.lineMedia) {
    const s = sampleMap.get(line.castMemberId);
    if (s) s.lineMediaId = line.media.id;
  }

  const nextSamples = project.voice.samples.map((s) => {
    const updated = sampleMap.get(s.castMemberId);
    return updated
      ? {
          ...s,
          cloneOutputMediaId: updated.cloneOutputMediaId,
          lineMediaId: updated.lineMediaId,
        }
      : s;
  });

  const next: FilmProject = {
    ...project,
    voice: {
      ...project.voice,
      samples: nextSamples,
      stitchedVoMediaId: result.stitched?.mediaId,
      modelId: project.voice.modelId ?? "mock-instant-clone",
    },
  };
  useProjects.setState((s) => ({
    projects: s.projects.map((p) => (p.id === project.id ? next : p)),
  }));
  await persistProject(next);
}

export const useProjects = create<Store>((set, get) => ({
  projects: [],
  hydrated: false,
  hydrating: false,
  saveError: null,

  hydrateFromServer: async () => {
    if (get().hydrating) return;
    set({ hydrating: true, saveError: null });
    try {
      const projects = await listMyProjects();
      set({ projects, hydrated: true, hydrating: false });
    } catch (err) {
      set({
        hydrating: false,
        hydrated: true,
        saveError: err instanceof Error ? err.message : "Failed to load projects",
      });
    }
  },

  createFromClassicBook: async (classicId) => {
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
    await persistProject(project);
    return id;
  },

  createFromCustomBook: async (title, text) => {
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
    await persistProject(project);
    return id;
  },

  updateProject: (id, patch) => {
    set((s) => ({
      projects: s.projects.map((p) =>
        p.id === id ? { ...p, ...patch, updatedAt: now() } : p,
      ),
    }));
    scheduleSave(id);
  },

  flushProject: async (id) => {
    const t = saveTimers.get(id);
    if (t) {
      clearTimeout(t);
      saveTimers.delete(id);
    }
    const project = get().projects.find((p) => p.id === id);
    if (!project) return;
    await persistProject(project);
  },

  setCast: (id, cast) => {
    const project = get().projects.find((p) => p.id === id);
    if (!project) return;
    const voice = syncVoiceFromCast(cast, project.voice);
    const next = {
      ...project,
      cast,
      voice,
      castingConfirmed: false,
      estimate: buildEstimate({ ...project, cast, voice }),
      updatedAt: now(),
    };
    set((s) => ({
      projects: s.projects.map((p) => (p.id === id ? next : p)),
    }));
    scheduleSave(id);
  },

  setVoice: (id, voice) => {
    const project = get().projects.find((p) => p.id === id);
    if (!project) return;
    const safe = syncVoiceFromCast(project.cast, voice);
    const next = {
      ...project,
      voice: safe,
      estimate: buildEstimate({ ...project, voice: safe }),
      updatedAt: now(),
    };
    set((s) => ({
      projects: s.projects.map((p) => (p.id === id ? next : p)),
    }));
    scheduleSave(id);
  },

  setWizardStep: (id, step) => {
    if (step === "done") {
      get().updateProject(id, { wizardStep: "done" });
      return;
    }
    get().updateProject(id, { wizardStep: step, status: "setup" });
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
        asset: undefined,
        consent: false,
        cloneOutputMediaId: undefined,
        lineMediaId: undefined,
      })),
      stitchedVoMediaId: undefined,
      modelId: project.voice.modelId ?? "mock-instant-clone",
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

  deleteProject: async (id) => {
    await deleteMyProject({ data: { projectId: id } });
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
    void getMyProject({ data: { projectId: id } }).then((remote) => {
      if (!remote) return;
      set((s) => ({
        projects: s.projects.map((p) =>
          p.id === id ? { ...remote, cast: mergeCastPreview(p.cast, remote.cast) } : p,
        ),
      }));
    });
    return resumeStage(project);
  },

  runFreeSample: async (id) => {
    await get().flushProject(id);
    const project = get().projects.find((p) => p.id === id);
    if (!project) return { ok: false, reason: "Project not found" };
    if (project.wizardStep !== "confirm" && project.wizardStep !== "done") {
      return { ok: false, reason: "Finish cast → voice → estimate → confirm first." };
    }
    if (project.status === "sample" || project.unlockedShots >= 1) {
      return { ok: false, reason: "Free sample already generated" };
    }

    const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
    await prepareVoiceClones(project);
    const labels = [
      project.sourceKind === "classic"
        ? "Loading cached screenplay…"
        : "Writing opening scene…",
      castLine(project),
      voiceLine(get().projects.find((p) => p.id === id) ?? project),
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
    await get().flushProject(id);
    return { ok: true };
  },

  runFullGenerate: async (id) => {
    await get().flushProject(id);
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

    await prepareVoiceClones(project);
    const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
    const labels =
      project.sourceKind === "classic"
        ? [
            "Loading cached screenplay…",
            "Loading cached storyboard…",
            castLine(project),
            voiceLine(get().projects.find((p) => p.id === id) ?? project),
            "Compositing personalized characters…",
            "Assembling picture lock…",
            "Movie ready",
          ]
        : [
            "Writing screenplay from scratch…",
            "Building storyboard…",
            castLine(project),
            voiceLine(get().projects.find((p) => p.id === id) ?? project),
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
    await get().flushProject(id);
    return { ok: true };
  },

  runRerender: async (id) => {
    await get().flushProject(id);
    const project = get().projects.find((p) => p.id === id);
    if (!project) return { ok: false, reason: "Project not found" };
    const fullCost = project.estimate?.creditsFull ?? 15;
    const owned =
      project.status === "ready" && project.unlockedShots >= project.shots.length;
    const cost = owned ? Math.max(4, Math.round(fullCost / 2)) : fullCost;
    if (!useWallet.getState().spend(cost)) {
      return { ok: false, reason: "Not enough credits" };
    }
    await prepareVoiceClones(project);
    const update = (patch: Partial<FilmProject>) => get().updateProject(id, patch);
    await simulatePipeline(update, [
      "Applying cast updates…",
      castLine(project),
      voiceLine(get().projects.find((p) => p.id === id) ?? project),
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
    await get().flushProject(id);
    return { ok: true };
  },

  toggleStar: (id) => {
    const project = get().projects.find((p) => p.id === id);
    if (!project) return;
    get().updateProject(id, { stars: project.stars > 0 ? 0 : 1 });
  },
}));
