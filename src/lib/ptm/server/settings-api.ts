/**
 * Settings server functions — same auth + repo pattern as projects-api.
 * Model catalog: single src/data/models/models.json (all capabilities).
 */
import { createServerFn } from "@tanstack/react-start";
import {
  getApiKeyEnv,
  getDefaultModelId,
  getFullCatalog,
  getModel,
  listModels,
} from "@/lib/ptm/models/catalog";
import { ptmAuthMiddleware } from "./ptm-auth";
import {
  deleteSecret,
  listSecretsMeta,
  resolveSecret,
  upsertProviderPrefs,
  upsertSecret,
  getProviderPrefs,
} from "./settings-repo";

export type SettingsSecretMeta = {
  keyName: string;
  providerId: string;
  label: string | null;
  configured: boolean;
  masked: string;
  updatedAt: string;
  source: "db" | "env";
};

export type SettingsPrefsDto = {
  voiceProviderId: string;
  voiceModelId: string;
};

export type SettingsBundle = {
  prefs: SettingsPrefsDto;
  secrets: SettingsSecretMeta[];
};

export type VoiceCatalogDto = {
  defaults: {
    cloneProvider: string;
    sampleMinSec: number;
    sampleTargetSec: number;
    sampleMaxSec: number;
    outputFormat: string;
    outputExtension: string;
  };
  /** All capabilities in one catalog; UI may filter by capability */
  capabilities: string[];
  models: Array<{
    id: string;
    capability: string;
    providerId: string;
    displayName: string;
    description: string;
    kind: string;
    requiresApiKey: boolean;
    apiKeyEnv: string | null;
    enabled: boolean;
    clientSideOnly: boolean;
  }>;
};

/** Public catalog for Settings UI (no secrets). */
export const getVoiceCatalog = createServerFn({ method: "GET" }).handler(
  async (): Promise<VoiceCatalogDto> => {
    const full = getFullCatalog();
    const voiceDefault = getDefaultModelId("voice") ?? "mock-instant-clone";
    const defaultModel = getModel(voiceDefault);
    const capabilities = [
      ...new Set(listModels().map((m) => m.capability)),
    ].sort();

    return {
      defaults: {
        cloneProvider: defaultModel?.providerId ?? "mock",
        sampleMinSec: full.sampleDefaults.sampleMinSec,
        sampleTargetSec: full.sampleDefaults.sampleTargetSec,
        sampleMaxSec: full.sampleDefaults.sampleMaxSec,
        outputFormat: full.sampleDefaults.outputFormat,
        outputExtension: full.sampleDefaults.outputExtension,
      },
      capabilities,
      models: listModels().map((m) => ({
        id: m.id,
        capability: m.capability,
        providerId: m.providerId,
        displayName: m.displayName,
        description: m.description,
        kind: m.kind,
        requiresApiKey: m.requiresApiKey,
        apiKeyEnv: getApiKeyEnv(m),
        enabled: m.enabled,
        clientSideOnly: m.clientSideOnly,
      })),
    };
  },
);

export const getMySettings = createServerFn({ method: "GET" })
  .middleware([ptmAuthMiddleware])
  .handler(async ({ context }): Promise<SettingsBundle> => {
    const prefs = await getProviderPrefs(context.userId);
    const secrets = await listSecretsMeta(context.userId);

    const envHints: SettingsSecretMeta[] = [];
    for (const m of listModels()) {
      const envName = getApiKeyEnv(m);
      if (!envName) continue;
      if (secrets.some((s) => s.keyName === envName)) continue;
      const resolved = await resolveSecret(context.userId, envName);
      if (resolved.source === "env" && resolved.value) {
        envHints.push({
          keyName: envName,
          providerId: m.providerId,
          label: `${m.displayName} (env)`,
          configured: true,
          masked: "••••env",
          updatedAt: new Date().toISOString(),
          source: "env",
        });
      }
    }

    const defaultModelId = getDefaultModelId("voice") ?? "mock-instant-clone";
    const defaultModel = getModel(defaultModelId);

    return {
      prefs: prefs
        ? {
            voiceProviderId: prefs.voice_provider_id,
            voiceModelId: prefs.voice_model_id,
          }
        : {
            voiceProviderId: defaultModel?.providerId ?? "mock",
            voiceModelId: defaultModelId,
          },
      secrets: [
        ...secrets.map(
          (s): SettingsSecretMeta => ({
            keyName: s.keyName,
            providerId: s.providerId,
            label: s.label,
            configured: s.configured,
            masked: s.masked,
            updatedAt: s.updatedAt,
            source: "db",
          }),
        ),
        ...envHints,
      ],
    };
  });

export const saveVoicePrefs = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: { voiceProviderId: string; voiceModelId: string }) => data,
  )
  .handler(async ({ context, data }): Promise<SettingsPrefsDto> => {
    const model = getModel(data.voiceModelId);
    if (!model || model.capability !== "voice") {
      throw new Error("Unknown voice model");
    }
    const row = await upsertProviderPrefs({
      userId: context.userId,
      voiceProviderId: data.voiceProviderId || model.providerId,
      voiceModelId: data.voiceModelId,
      extras: {},
    });
    return {
      voiceProviderId: row.voice_provider_id,
      voiceModelId: row.voice_model_id,
    };
  });

export const saveProviderSecret = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator(
    (data: {
      keyName: string;
      keyValue: string;
      providerId: string;
      label?: string;
    }) => data,
  )
  .handler(async ({ context, data }) => {
    const value = data.keyValue.trim();
    if (!value) throw new Error("Key value is empty");
    if (!data.keyName.trim()) throw new Error("Key name is required");
    await upsertSecret({
      userId: context.userId,
      keyName: data.keyName.trim(),
      keyValue: value,
      providerId: data.providerId.trim(),
      label: data.label,
    });
    return { ok: true as const, keyName: data.keyName.trim() };
  });

export const removeProviderSecret = createServerFn({ method: "POST" })
  .middleware([ptmAuthMiddleware])
  .validator((data: { keyName: string }) => data)
  .handler(async ({ context, data }) => {
    const ok = await deleteSecret(context.userId, data.keyName);
    return { ok };
  });

/** Server-only helper for voice providers. */
export async function resolveVoiceRuntime(userId: string): Promise<{
  providerId: string;
  modelId: string;
  apiKey: string | null;
  apiKeySource: "db" | "env" | "none";
  apiKeyEnv: string | null;
}> {
  const prefs = await getProviderPrefs(userId);
  const modelId =
    prefs?.voice_model_id ?? getDefaultModelId("voice") ?? "mock-instant-clone";
  const model = getModel(modelId) ?? listModels("voice")[0]!;
  const providerId = prefs?.voice_provider_id ?? model.providerId;
  const apiKeyEnv = getApiKeyEnv(model);
  if (!apiKeyEnv) {
    return {
      providerId,
      modelId: model.id,
      apiKey: null,
      apiKeySource: "none",
      apiKeyEnv: null,
    };
  }
  const resolved = await resolveSecret(userId, apiKeyEnv);
  return {
    providerId,
    modelId: model.id,
    apiKey: resolved.value,
    apiKeySource: resolved.source,
    apiKeyEnv,
  };
}
