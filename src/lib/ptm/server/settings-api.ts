/**
 * Settings server functions — same auth + repo pattern as projects-api.
 * Secrets stay server-side; client only sees masked status + prefs.
 */
import { createServerFn } from "@tanstack/react-start";
import catalog from "@/data/models/voice-models.json";
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
  models: Array<{
    id: string;
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

/** Public catalog for the Settings UI (no secrets). */
export const getVoiceCatalog = createServerFn({ method: "GET" }).handler(
  async (): Promise<VoiceCatalogDto> => {
    return {
      defaults: catalog.defaults,
      models: catalog.models.map((m) => ({
        id: m.id,
        providerId: m.providerId,
        displayName: m.displayName,
        description: m.description,
        kind: m.kind,
        requiresApiKey: m.requiresApiKey,
        apiKeyEnv:
          "apiKeyEnv" in m
            ? ((m as { apiKeyEnv?: string }).apiKeyEnv ?? null)
            : null,
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
    for (const m of catalog.models) {
      const envName =
        "apiKeyEnv" in m ? (m as { apiKeyEnv?: string }).apiKeyEnv : undefined;
      if (!envName) continue;
      const already = secrets.some((s) => s.keyName === envName);
      if (already) continue;
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

    const defaultModel =
      catalog.models.find((m) => m.providerId === catalog.defaults.cloneProvider)
        ?.id ?? "mock-instant-clone";

    return {
      prefs: prefs
        ? {
            voiceProviderId: prefs.voice_provider_id,
            voiceModelId: prefs.voice_model_id,
          }
        : {
            voiceProviderId: catalog.defaults.cloneProvider,
            voiceModelId: defaultModel,
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
    const model = catalog.models.find((m) => m.id === data.voiceModelId);
    if (!model) throw new Error("Unknown voice model");
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

/** Server-only helper for voice providers (not a createServerFn). */
export async function resolveVoiceRuntime(userId: string): Promise<{
  providerId: string;
  modelId: string;
  apiKey: string | null;
  apiKeySource: "db" | "env" | "none";
  apiKeyEnv: string | null;
}> {
  const prefs = await getProviderPrefs(userId);
  const modelId = prefs?.voice_model_id ?? "mock-instant-clone";
  const model = catalog.models.find((m) => m.id === modelId) ?? catalog.models[0]!;
  const providerId = prefs?.voice_provider_id ?? model.providerId;
  const apiKeyEnv =
    "apiKeyEnv" in model
      ? ((model as { apiKeyEnv?: string }).apiKeyEnv ?? null)
      : null;
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
