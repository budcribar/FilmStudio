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
  photoDataUrl?: string;
  /** User opted to personalize this role (name/photo swap) */
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
    // Pre-check the first personalizable role (e.g. Alice) so “kid as lead” is one click away
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
      notes: "Swap in a photo of your kid for a personal children's-book feel.",
      selected: false,
    });
  }
  if (/\b(wife|husband|spouse|juliet|romeo)\b/i.test(lower)) {
    cast.push({
      id: uid(),
      roleInStory: "Partner in story",
      displayName: "",
      relation: "spouse",
      selected: false,
    });
  }
  if (cast.length < 2) {
    cast.push({
      id: uid(),
      roleInStory: "Supporting character",
      displayName: "",
      relation: "original",
      selected: false,
      notes: "Optional personalization.",
    });
  }
  return cast.slice(0, 6);
}

export function personalizedCount(cast: CastMember[]) {
  return cast.filter(
    (c) => c.selected && (c.displayName.trim().length > 0 || !!c.photoDataUrl),
  ).length;
}

/** Ready when every *selected* role has a name or photo. Zero selected = generate as written. */
export function castIsReady(cast: CastMember[]): boolean {
  const selected = cast.filter((c) => c.selected);
  if (selected.length === 0) return true;
  return selected.every((c) => c.displayName.trim().length >= 1 || !!c.photoDataUrl);
}

export function emptyCastMember(partial?: Partial<CastMember>): CastMember {
  return {
    id: uid(),
    roleInStory: partial?.roleInStory ?? "New character",
    displayName: partial?.displayName ?? "",
    relation: partial?.relation ?? "child",
    notes: partial?.notes,
    photoDataUrl: partial?.photoDataUrl,
    selected: partial?.selected ?? true,
    classicCharacterId: partial?.classicCharacterId,
  };
}

export function relationLabel(r: CharacterRelation) {
  return RELATION_OPTIONS.find((o) => o.id === r)?.label ?? r;
}
