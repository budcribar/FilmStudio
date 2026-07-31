/**
 * Single app-wide model catalog loader.
 * Source: src/data/models/models.json (voice, video, chat, image, …).
 */
import catalog from "@/data/models/models.json";

export type ModelCapability =
  | "voice"
  | "video"
  | "chat"
  | "image"
  | "face_swap"
  | (string & {});

export type CatalogModel = (typeof catalog.models)[number];

export function getFullCatalog() {
  return catalog;
}

export function listModels(capability?: ModelCapability): CatalogModel[] {
  const all = catalog.models as CatalogModel[];
  if (!capability) return all;
  return all.filter((m) => m.capability === capability);
}

export function getModel(modelId: string): CatalogModel | undefined {
  return (catalog.models as CatalogModel[]).find((m) => m.id === modelId);
}

export function getDefaultModelId(capability: ModelCapability): string | null {
  const defaults = catalog.defaults as Record<string, string | null>;
  return defaults[capability] ?? null;
}

export function getDefaultModel(capability: ModelCapability): CatalogModel {
  const id = getDefaultModelId(capability);
  if (id) {
    const m = getModel(id);
    if (m) return m;
  }
  const first = listModels(capability).find((m) => m.enabled);
  if (!first) {
    throw new Error(`No models enabled for capability: ${capability}`);
  }
  return first;
}

export function getApiKeyEnv(model: CatalogModel): string | null {
  return "apiKeyEnv" in model
    ? ((model as { apiKeyEnv?: string }).apiKeyEnv ?? null)
    : null;
}

export const sampleDefaults = catalog.sampleDefaults;
