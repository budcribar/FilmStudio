import type { ClassicCharacter } from "@/data/classics";

export type CharacterRelation =
  | "self"
  | "spouse"
  | "partner"
  | "child"
  | "parent"
  | "sibling"
  | "friend"
  | "original"
  | "other";

export type CastMember = {
  id: string;
  roleInStory: string;
  displayName: string;
  relation: CharacterRelation;
  notes?: string;
  /**
   * Transient client preview only — never persisted to server.
   * Prefer photoMediaId (client media store) for durable local photos.
   */
  photoDataUrl?: string;
  /** Client media store id for portrait (binary stays on device) */
  photoMediaId?: string;
  selected: boolean;
  classicCharacterId?: string;
};

export const RELATION_OPTIONS: { id: CharacterRelation; label: string }[] = [
  { id: "self", label: "Me" },
  { id: "spouse", label: "Spouse" },
  { id: "partner", label: "Partner" },
  { id: "child", label: "Child" },
  { id: "parent", label: "Parent" },
  { id: "sibling", label: "Sibling" },
  { id: "friend", label: "Friend" },
  { id: "original", label: "Keep as written" },
  { id: "other", label: "Other" },
];

function uid() {
  return `cast_${Math.random().toString(36).slice(2, 9)}`;
}

export function castFromClassicCharacters(chars: ClassicCharacter[]): CastMember[] {
  let primaryPicked = false;
  return chars.map((c) => {
    const selected = c.personalizable && !primaryPicked;
    if (selected) primaryPicked = true;
    const isChildLead =
      /alice|child|girl|boy/i.test(c.name) || /alice|child/i.test(c.blurb);
    return {
      id: uid(),
      classicCharacterId: c.id,
      roleInStory: c.name,
      displayName: "",
      relation: isChildLead ? "child" : c.personalizable ? "self" : "original",
      notes: c.blurb,
      selected,
    };
  });
}

export function suggestCastFromSource(text: string, title?: string): CastMember[] {
  const lower = text.toLowerCase();
  const cast: CastMember[] = [
    {
      id: uid(),
      roleInStory: /\bI\b/.test(text) ? "Narrator / lead" : "Lead character",
      displayName: "",
      relation: "self",
      notes: "Main presence — often you or your child.",
      selected: true,
    },
  ];

  if (/\b(child|son|daughter|boy|girl|alice)\b/i.test(lower) || /alice/i.test(title ?? "")) {
    cast.push({
      id: uid(),
      roleInStory: "Child character",
      displayName: "",
      relation: "child",
      notes: "Optional second role.",
      selected: false,
    });
  }

  if (/\b(wife|husband|spouse|partner|love)\b/i.test(lower)) {
    cast.push({
      id: uid(),
      roleInStory: "Partner",
      displayName: "",
      relation: "spouse",
      selected: false,
    });
  }

  return cast.slice(0, 6);
}

export function emptyCastMember(
  partial?: Partial<CastMember>,
): CastMember {
  return {
    id: uid(),
    roleInStory: "Supporting role",
    displayName: "",
    relation: "other",
    selected: true,
    ...partial,
  };
}

export function personalizedCount(cast: CastMember[]): number {
  return cast.filter(
    (c) => c.selected && (c.displayName.trim() || c.photoDataUrl || c.photoMediaId),
  ).length;
}

export function castIsReady(cast: CastMember[]): boolean {
  const selected = cast.filter((c) => c.selected);
  if (selected.length === 0) return true;
  return selected.every(
    (c) => c.displayName.trim() !== "" || !!c.photoDataUrl || !!c.photoMediaId,
  );
}

export function relationLabel(r: CharacterRelation): string {
  return RELATION_OPTIONS.find((o) => o.id === r)?.label ?? r;
}
