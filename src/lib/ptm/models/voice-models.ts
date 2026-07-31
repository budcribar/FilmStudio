/**
 * Voice-facing helpers over the single models.json catalog.
 * Prefer @/lib/ptm/models/catalog for multi-capability code.
 */
import {
  getDefaultModel,
  getModel,
  listModels,
  sampleDefaults,
  type CatalogModel,
} from "./catalog";

export type VoiceModelDef = CatalogModel;

export function listVoiceModels(): VoiceModelDef[] {
  return listModels("voice");
}

export function getDefaultVoiceModel(): VoiceModelDef {
  return getDefaultModel("voice");
}

export function getVoiceModel(modelId: string): VoiceModelDef | undefined {
  const m = getModel(modelId);
  if (!m || m.capability !== "voice") return undefined;
  return m;
}

export const voiceModelDefaults = {
  cloneProvider: getDefaultVoiceModel().providerId,
  ...sampleDefaults,
};
