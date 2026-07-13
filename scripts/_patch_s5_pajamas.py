#!/usr/bin/env python3
"""Patch S5 C2/C3 to keep black-and-white pajamas from C1."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = ROOT / "projects" / "Buster" / "blueprint.clips.grok.json"
d = json.loads(p.read_text(encoding="utf-8"))

# Seed: bedtime PJs sticky after dressed
buster = d["global_production_variables"]["character_seed_tokens"]["Character_Buster"]
buster["visual_lock"] = (
    "Always wearing signature silly noodle-head hat. "
    "After bedtime dress: STILL wearing black-and-white dog pajamas matching fur "
    "(never drop pajamas mid-night / on stairs); bare fur only before pajamas are put on."
)
desc = str(buster.get("description") or "")
if "still wearing" not in desc.lower() and "after" not in desc.lower():
    buster["description"] = (
        "Buster the Noodle Head Dog: small black-and-white dog (never brown/tan redesign), "
        "playful bouncy build, expressive face; ALWAYS wears his signature silly noodle-head hat; "
        "at bedtime wears black-and-white dog pajamas matching his fur (keep pajamas on for all "
        "subsequent night beats — stairs, hall, bedroom); beloved family pet."
    )

PJ = (
    "WARDROBE CONTINUITY: Character_Buster is STILL wearing the same black-and-white dog "
    "pajamas matching his fur (already put on in living room — do not remove pajamas or "
    "switch to bare fur); hat still on"
)

for s in d.get("scenes") or []:
    if s.get("scene_number") != 5:
        continue
    s["wardrobe_notes"] = (
        "Once pajamas are on in living room, Buster keeps black-and-white dog pajamas "
        "for stairs/hall/bedroom; signature hat always."
    )
    for c in s.get("veo_clips") or []:
        cn = int(c.get("clip_number") or 0)
        vp = c.get("visual_prompt") or ""
        m = re.search(r"\s*/\s*\d+p.*24fps\s*$", vp, flags=re.I)
        body = vp[: m.start()].strip() if m else vp
        suffix = m.group(0) if m else " / 720p, 24fps"
        if cn == 1:
            if "pajama" not in body.lower():
                body += (
                    ". Character_Buster wears black-and-white dog pajamas matching his fur"
                )
            print("C1 ok pajamas")
        elif cn in (2, 3):
            if "wardrobe continuity" not in body.lower() and "still wearing" not in body.lower():
                body = body.rstrip(". ") + ". " + PJ
            # Ensure "in pajamas" appears early in action line
            if "pajama" not in body.lower():
                body = body.replace(
                    "Character_Buster climbs",
                    "Character_Buster in black-and-white pajamas climbs",
                    1,
                )
                body = body.replace(
                    "Character_Buster aims",
                    "Character_Buster in black-and-white pajamas aims",
                    1,
                )
            neg = c.get("negative_prompt") or ""
            for frag in (
                "bare fur only",
                "no pajamas",
                "naked dog without pajamas",
                "daytime coat without pajamas",
            ):
                if frag not in neg.lower():
                    neg = f"{neg}, {frag}" if neg else frag
            c["negative_prompt"] = neg
            print(f"C{cn} patched")
        c["visual_prompt"] = re.sub(r"\s+", " ", body).strip() + suffix
        print(c["visual_prompt"][:220], "…\n")

p.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

# Stage 1 wardrobe_notes for S5
p1 = ROOT / "projects" / "Buster" / "scenes.json"
s1 = json.loads(p1.read_text(encoding="utf-8"))
for s in s1.get("scenes") or []:
    if s.get("scene_number") == 5:
        s["wardrobe_notes"] = (
            "Buster puts on black-and-white dog pajamas in living room; keep them on "
            "for stairs and hall; signature hat always."
        )
        print("Stage1 S5 wardrobe_notes set")
p1.write_text(json.dumps(s1, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("saved")
