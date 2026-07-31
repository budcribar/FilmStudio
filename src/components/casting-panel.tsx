import { Camera, Check, Plus, Trash2, Users } from "lucide-react";
import { useRef } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  castIsReady,
  emptyCastMember,
  personalizedCount,
  RELATION_OPTIONS,
  type CastMember,
  type CharacterRelation,
} from "@/lib/ptm/characters";
import { cn } from "@/lib/utils";

type Props = {
  cast: CastMember[];
  sourceKind: "classic" | "custom";
  disabled?: boolean;
  onChange: (cast: CastMember[]) => void;
  onContinue: () => void;
};

export function CastingPanel({ cast, sourceKind, disabled, onChange, onContinue }: Props) {
  const fileRefs = useRef<Record<string, HTMLInputElement | null>>({});

  function patch(id: string, partial: Partial<CastMember>) {
    onChange(cast.map((c) => (c.id === id ? { ...c, ...partial } : c)));
  }

  function remove(id: string) {
    if (cast.length <= 1) return;
    onChange(cast.filter((c) => c.id !== id));
  }

  function addPerson() {
    onChange([
      ...cast,
      emptyCastMember({
        roleInStory: "Supporting role",
        relation: "child",
        selected: true,
      }),
    ]);
  }

  function onPhoto(id: string, file: File | undefined) {
    if (!file || !file.type.startsWith("image/")) return;
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") {
        patch(id, { photoDataUrl: reader.result, selected: true });
      }
    };
    reader.readAsDataURL(file);
  }

  const ready = castIsReady(cast);
  const personal = personalizedCount(cast);

  return (
    <Card className="border-border-strong">
      <CardContent className="p-5 sm:p-6 space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
          <div className="flex items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[var(--radius-md)] border border-border bg-bg-subtle text-cinema">
              <Users className="h-4 w-4" />
            </span>
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.12em] text-fg-subtle mb-1">
                Step 2 · Characters
              </p>
              <h2 className="font-display font-semibold text-lg">
                Who do you want in the film?
              </h2>
              <p className="text-sm text-fg-muted mt-1 leading-relaxed max-w-xl">
                {sourceKind === "classic"
                  ? "This book’s screenplay and storyboard are already cached. Toggle roles to personalize — e.g. Alice as your child."
                  : "Pick roles to personalize. Next you can optionally add voice samples."}
              </p>
            </div>
          </div>
          <Badge variant={sourceKind === "classic" ? "success" : "cinema"}>
            {sourceKind === "classic" ? "Cached classic" : "Custom book"}
          </Badge>
        </div>

        <div className="space-y-3">
          {cast.map((member) => (
            <div
              key={member.id}
              className={cn(
                "rounded-[var(--radius-lg)] border px-3 py-3 sm:px-4 sm:py-4 space-y-3 transition-colors",
                member.selected
                  ? "border-cinema/40 bg-cinema/5"
                  : "border-border bg-bg opacity-90",
              )}
            >
              <div className="flex items-start gap-3">
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => patch(member.id, { selected: !member.selected })}
                  className={cn(
                    "mt-1 flex h-5 w-5 shrink-0 items-center justify-center rounded border transition-colors",
                    member.selected
                      ? "border-cinema bg-cinema text-bg"
                      : "border-border bg-bg",
                  )}
                  aria-label={member.selected ? "Deselect character" : "Select character"}
                >
                  {member.selected && <Check className="h-3 w-3" />}
                </button>

                <button
                  type="button"
                  disabled={disabled || !member.selected}
                  onClick={() => fileRefs.current[member.id]?.click()}
                  className={cn(
                    "relative h-14 w-14 shrink-0 overflow-hidden rounded-full border border-border bg-bg-subtle flex items-center justify-center text-fg-subtle",
                    member.photoDataUrl && "border-cinema/40",
                    !member.selected && "opacity-50",
                  )}
                  aria-label={`Photo for ${member.roleInStory}`}
                >
                  {member.photoDataUrl ? (
                    <img
                      src={member.photoDataUrl}
                      alt=""
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <Camera className="h-4 w-4" />
                  )}
                </button>
                <input
                  ref={(el) => {
                    fileRefs.current[member.id] = el;
                  }}
                  type="file"
                  accept="image/*"
                  className="sr-only"
                  disabled={disabled}
                  onChange={(e) => onPhoto(member.id, e.target.files?.[0])}
                />

                <div className="min-w-0 flex-1 space-y-2">
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-display font-semibold text-sm">{member.roleInStory}</p>
                    {cast.length > 1 && sourceKind === "custom" && (
                      <button
                        type="button"
                        disabled={disabled}
                        onClick={() => remove(member.id)}
                        className="text-fg-subtle hover:text-danger p-1"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>
                  {member.notes && (
                    <p className="text-xs text-fg-subtle leading-relaxed">{member.notes}</p>
                  )}
                </div>
              </div>

              {member.selected && (
                <div className="grid sm:grid-cols-2 gap-3 pl-0 sm:pl-8">
                  <div>
                    <label className="text-[11px] uppercase tracking-wide text-fg-subtle mb-1.5 block">
                      Replace with (name)
                    </label>
                    <Input
                      value={member.displayName}
                      disabled={disabled}
                      onChange={(e) => patch(member.id, { displayName: e.target.value })}
                      placeholder="e.g. your child’s name"
                    />
                  </div>
                  <div>
                    <label className="text-[11px] uppercase tracking-wide text-fg-subtle mb-1.5 block">
                      Relationship
                    </label>
                    <select
                      disabled={disabled}
                      value={member.relation}
                      onChange={(e) =>
                        patch(member.id, {
                          relation: e.target.value as CharacterRelation,
                        })
                      }
                      className="flex h-10 w-full rounded-[var(--radius-md)] border border-border bg-bg-elevated px-3 text-sm text-fg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      {RELATION_OPTIONS.map((o) => (
                        <option key={o.id} value={o.id}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>

        <div className="flex flex-col sm:flex-row gap-2">
          {sourceKind === "custom" && (
            <Button
              type="button"
              variant="secondary"
              disabled={disabled || cast.length >= 8}
              onClick={addPerson}
            >
              <Plus className="h-4 w-4" />
              Add character
            </Button>
          )}
          <Button
            type="button"
            className="sm:ml-auto"
            disabled={disabled || !ready}
            onClick={onContinue}
          >
            Continue to voice
            {personal > 0 ? ` · ${personal} personalized` : " · as written"}
          </Button>
        </div>
        {!ready && (
          <p className="text-xs text-danger">
            Selected characters need a name or photo before continuing.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
