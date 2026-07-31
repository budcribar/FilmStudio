import catalog from "@/data/models/voice-models.json";

export type VoiceModelDef = (typeof catalog.models)[number];

export function listVoiceModels(): VoiceModelDef[] {
  return catalog.models as VoiceModelDef[];
}

export function getDefaultVoiceModel(): VoiceModelDef {
  const id = catalog.defaults.cloneProvider;
  const byProvider = catalog.models.find(
    (m) => m.providerId === id && m.enabled,
  );
  if (byProvider) return byProvider as VoiceModelDef;
  const enabled = catalog.models.find((m) => m.enabled);
  if (!enabled) throw new Error("No voice models enabled");
  return enabled as VoiceModelDef;
}

export function getVoiceModel(modelId: string): VoiceModelDef | undefined {
  return catalog.models.find((m) => m.id === modelId) as VoiceModelDef | undefined;
}

export const voiceModelDefaults = catalog.defaults;
