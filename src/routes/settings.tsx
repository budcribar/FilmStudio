import { createFileRoute, Link } from "@tanstack/react-router";
import { Check, KeyRound, Loader2, Settings2, Trash2 } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  getMySettings,
  getVoiceCatalog,
  removeProviderSecret,
  saveProviderSecret,
  saveVoicePrefs,
  type SettingsSecretMeta,
  type VoiceCatalogDto,
} from "@/lib/ptm/server/settings-api";
import { getVoiceRuntimeStatus } from "@/lib/ptm/server/voice-api";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/settings")({
  component: SettingsPage,
});

function SettingsPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [okMsg, setOkMsg] = useState("");
  const [catalog, setCatalog] = useState<VoiceCatalogDto | null>(null);
  const [capability, setCapability] = useState("voice");
  const [providerId, setProviderId] = useState("mock");
  const [modelId, setModelId] = useState("mock-instant-clone");
  const [secrets, setSecrets] = useState<SettingsSecretMeta[]>([]);
  const [keyDraft, setKeyDraft] = useState("");
  const [runtime, setRuntime] = useState<{
    live: boolean;
    hasApiKey: boolean;
    apiKeySource: string;
    providerId: string;
    modelId: string;
  } | null>(null);

  const models = useMemo(() => {
    const all = catalog?.models ?? [];
    return all.filter((m) => m.capability === capability);
  }, [catalog, capability]);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [cat, settings, status] = await Promise.all([
        getVoiceCatalog(),
        getMySettings(),
        getVoiceRuntimeStatus(),
      ]);
      setCatalog(cat);
      setProviderId(settings.prefs.voiceProviderId);
      setModelId(settings.prefs.voiceModelId);
      setSecrets(settings.secrets);
      setRuntime(status);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load settings");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const selectedModel = useMemo(
    () => models.find((m) => m.id === modelId),
    [models, modelId],
  );

  const modelsForProvider = useMemo(
    () => models.filter((m) => m.providerId === providerId),
    [models, providerId],
  );

  const providers = useMemo(() => {
    const map = new Map<string, { id: string; label: string }>();
    for (const m of models) {
      if (!map.has(m.providerId)) {
        map.set(m.providerId, {
          id: m.providerId,
          label: m.providerId === "mock" ? "Mock (local)" : m.providerId,
        });
      }
    }
    return [...map.values()];
  }, [models]);

  async function onSavePrefs() {
    if (capability !== "voice") {
      setError("Only voice prefs are wired to generate yet — video/chat next.");
      return;
    }
    setSaving(true);
    setError("");
    setOkMsg("");
    try {
      await saveVoicePrefs({
        data: { voiceProviderId: providerId, voiceModelId: modelId },
      });
      const status = await getVoiceRuntimeStatus();
      setRuntime(status);
      setOkMsg("Voice provider saved.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function onSaveKey() {
    if (!selectedModel?.apiKeyEnv) return;
    setSaving(true);
    setError("");
    setOkMsg("");
    try {
      await saveProviderSecret({
        data: {
          keyName: selectedModel.apiKeyEnv,
          keyValue: keyDraft,
          providerId: selectedModel.providerId,
          label: selectedModel.displayName,
        },
      });
      setKeyDraft("");
      await load();
      setOkMsg("API key stored on the server (never shown again in full).");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save key");
    } finally {
      setSaving(false);
    }
  }

  async function onRemoveKey(keyName: string) {
    setSaving(true);
    setError("");
    try {
      await removeProviderSecret({ data: { keyName } });
      await load();
      setOkMsg("Key removed.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Remove failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-4 sm:px-6 py-10 sm:py-14">
      <div className="mb-8">
        <Badge variant="cinema" className="mb-3 uppercase tracking-[0.14em] text-[10px]">
          Configuration
        </Badge>
        <h1 className="font-display text-3xl font-semibold tracking-tight flex items-center gap-2">
          <Settings2 className="h-7 w-7 text-cinema" />
          Settings
        </h1>
        <p className="mt-2 text-sm text-fg-muted max-w-xl leading-relaxed">
          One model catalog for the whole app (voice, video, chat…). Keys on the server;
          generate currently uses the voice selection.
        </p>
      </div>

      {loading ? (
        <div className="flex items-center gap-2 text-sm text-fg-muted">
          <Loader2 className="h-4 w-4 animate-spin text-cinema" />
          Loading configuration…
        </div>
      ) : (
        <div className="space-y-6">
          {error && (
            <div className="rounded-[var(--radius-md)] border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger">
              {error}
            </div>
          )}
          {okMsg && (
            <div className="rounded-[var(--radius-md)] border border-success/30 bg-success/10 px-4 py-3 text-sm text-success flex items-center gap-2">
              <Check className="h-4 w-4" />
              {okMsg}
            </div>
          )}

          {runtime && capability === "voice" && (
            <Card>
              <CardContent className="p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <p className="text-xs uppercase tracking-wide text-fg-subtle mb-1">
                    Voice runtime
                  </p>
                  <p className="font-display font-semibold text-sm">
                    {runtime.providerId} · {runtime.modelId}
                  </p>
                  <p className="text-xs text-fg-muted mt-1">
                    {runtime.live
                      ? `Live · key from ${runtime.apiKeySource}`
                      : "Mock / no key — client synthetic audio"}
                  </p>
                </div>
                <Badge variant={runtime.live ? "success" : "default"}>
                  {runtime.live ? "Live" : "Demo"}
                </Badge>
              </CardContent>
            </Card>
          )}

          <Card className="border-border-strong">
            <CardContent className="p-5 sm:p-6 space-y-5">
              <div>
                <h2 className="font-display font-semibold text-lg">Models</h2>
                <p className="text-sm text-fg-muted mt-1">
                  Single source:{" "}
                  <code className="text-fg-subtle">src/data/models/models.json</code>
                </p>
              </div>

              {(catalog?.capabilities?.length ?? 0) > 0 && (
                <div className="space-y-2">
                  <p className="text-[11px] uppercase tracking-wide text-fg-subtle">
                    Capability
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {(catalog?.capabilities ?? ["voice"]).map((c) => (
                      <button
                        key={c}
                        type="button"
                        onClick={() => {
                          setCapability(c);
                          const first = (catalog?.models ?? []).find(
                            (m) => m.capability === c,
                          );
                          if (first) {
                            setProviderId(first.providerId);
                            setModelId(first.id);
                          }
                        }}
                        className={cn(
                          "rounded-[var(--radius-md)] border px-3 py-2 text-sm font-medium capitalize transition-colors",
                          capability === c
                            ? "border-cinema/50 bg-cinema/10 text-fg"
                            : "border-border bg-bg hover:border-border-strong text-fg-muted",
                        )}
                      >
                        {c.replace("_", " ")}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              <div className="space-y-2">
                <p className="text-[11px] uppercase tracking-wide text-fg-subtle">
                  Provider
                </p>
                <div className="flex flex-wrap gap-2">
                  {providers.length === 0 ? (
                    <p className="text-sm text-fg-muted">
                      No models for this capability yet — add them in models.json.
                    </p>
                  ) : (
                    providers.map((p) => (
                      <button
                        key={p.id}
                        type="button"
                        onClick={() => {
                          setProviderId(p.id);
                          const first = models.find((m) => m.providerId === p.id);
                          if (first) setModelId(first.id);
                        }}
                        className={cn(
                          "rounded-[var(--radius-md)] border px-3 py-2 text-sm font-medium transition-colors",
                          providerId === p.id
                            ? "border-cinema/50 bg-cinema/10 text-fg"
                            : "border-border bg-bg hover:border-border-strong text-fg-muted",
                        )}
                      >
                        {p.label}
                      </button>
                    ))
                  )}
                </div>
              </div>

              <div className="space-y-2">
                <p className="text-[11px] uppercase tracking-wide text-fg-subtle">
                  Model
                </p>
                <div className="space-y-2">
                  {modelsForProvider.map((m) => (
                    <button
                      key={m.id}
                      type="button"
                      onClick={() => setModelId(m.id)}
                      className={cn(
                        "w-full text-left rounded-[var(--radius-lg)] border px-4 py-3 transition-colors",
                        modelId === m.id
                          ? "border-cinema/40 bg-cinema/5"
                          : "border-border bg-bg hover:border-border-strong",
                      )}
                    >
                      <p className="font-display font-semibold text-sm">{m.displayName}</p>
                      <p className="text-xs text-fg-muted mt-1 leading-relaxed">
                        {m.description}
                      </p>
                      {m.requiresApiKey && (
                        <Badge variant="cinema" className="mt-2">
                          Needs API key
                        </Badge>
                      )}
                    </button>
                  ))}
                </div>
              </div>

              <Button disabled={saving || models.length === 0} onClick={() => void onSavePrefs()}>
                {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                Save {capability} provider & model
              </Button>
            </CardContent>
          </Card>

          {selectedModel?.requiresApiKey && selectedModel.apiKeyEnv && (
            <Card className="border-border-strong">
              <CardContent className="p-5 sm:p-6 space-y-4">
                <div className="flex items-start gap-3">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[var(--radius-md)] border border-border bg-bg-subtle text-cinema">
                    <KeyRound className="h-4 w-4" />
                  </span>
                  <div>
                    <h2 className="font-display font-semibold text-lg">API key</h2>
                    <p className="text-sm text-fg-muted mt-1 leading-relaxed">
                      Stored as{" "}
                      <code className="text-fg-subtle">{selectedModel.apiKeyEnv}</code> for{" "}
                      <strong className="text-fg">{selectedModel.providerId}</strong>. Same
                      key store for every capability.
                    </p>
                  </div>
                </div>

                <Input
                  type="password"
                  autoComplete="off"
                  placeholder="Paste API key…"
                  value={keyDraft}
                  onChange={(e) => setKeyDraft(e.target.value)}
                />
                <div className="flex flex-wrap gap-2">
                  <Button
                    disabled={saving || keyDraft.trim().length < 8}
                    onClick={() => void onSaveKey()}
                  >
                    Save key
                  </Button>
                  <Button asChild variant="ghost">
                    <Link to="/studio">Back to studio</Link>
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-5 space-y-3">
              <h2 className="font-display font-semibold text-sm">Configured keys</h2>
              {secrets.length === 0 ? (
                <p className="text-sm text-fg-muted">No keys stored yet.</p>
              ) : (
                <ul className="space-y-2">
                  {secrets.map((s) => (
                    <li
                      key={`${s.keyName}-${s.source}`}
                      className="flex items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border px-3 py-2 text-sm"
                    >
                      <div className="min-w-0">
                        <p className="font-medium truncate">{s.keyName}</p>
                        <p className="text-xs text-fg-subtle">
                          {s.providerId} · {s.masked} · {s.source}
                        </p>
                      </div>
                      {s.source === "db" && (
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={saving}
                          onClick={() => void onRemoveKey(s.keyName)}
                          aria-label={`Remove ${s.keyName}`}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
