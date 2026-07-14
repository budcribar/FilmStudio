#!/usr/bin/env python3
"""Film-wide style lock: humans match stylized CG dog, not photoreal."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STYLE = (
    "STYLE LOCK: stylized 3D animated children's picture-book CG "
    "(same render family as the cartoon dog) -- smooth animated materials, "
    "expressive non-photoreal faces; NOT live-action, NOT photoreal human photography"
)


def patch_seeds(gpv: dict) -> dict:
    gpv["directorial_treatment"] = (
        "Warm stylized 3D animated children's picture-book short "
        "(Pixar/DreamWorks family CG, not live-action): soft practical lighting, "
        "gentle camera, playful dog energy that settles into cozy sleep. "
        "All humans match the same animated style as the dog -- never photoreal people. "
        "Faithful to rhyming VO; clear cause-and-effect bedtime routine; no slapstick cruelty. "
        "Keep Buster black-and-white coat locked every frame."
    )
    gpv["render_style_lock"] = STYLE
    seeds = gpv.get("character_seed_tokens") or {}
    for key in ("Character_Mom", "Character_Daddy"):
        seed = seeds.get(key)
        if not isinstance(seed, dict):
            continue
        vl = str(seed.get("visual_lock") or "")
        if "stylized" not in vl.lower() and "animated" not in vl.lower():
            seed["visual_lock"] = (
                "Stylized 3D animated character matching the dog's picture-book CG look "
                "(not photoreal, not live-action). " + vl
            ).strip()
        desc = str(seed.get("description") or "")
        if "stylized" not in desc.lower() and "animated" not in desc.lower():
            seed["description"] = (
                desc.rstrip(". ")
                + ". Stylized 3D animated picture-book design matching Buster's CG style, not photoreal."
            )
    return gpv


def main() -> None:
    for rel in ("projects/Buster/scenes.json", "projects/Buster/blueprint.clips.grok.json"):
        path = ROOT / rel
        data = json.loads(path.read_text(encoding="utf-8"))
        data["global_production_variables"] = patch_seeds(
            data.get("global_production_variables") or {}
        )
        if "clips" in Path(rel).name or "blueprint" in Path(rel).name:
            for sc in data.get("scenes") or []:
                for c in sc.get("veo_clips") or []:
                    vp = c.get("visual_prompt") or ""
                    if "Character_Mom" not in vp and "Character_Daddy" not in vp:
                        continue
                    if "STYLE LOCK" in vp:
                        print(
                            f"already S{sc.get('scene_number')}C{c.get('clip_number')}"
                        )
                        continue
                    m = re.search(r"\s*/\s*\d+p.*24fps\s*$", vp, flags=re.I)
                    body = vp[: m.start()].strip() if m else vp
                    suf = m.group(0) if m else " / 720p, 24fps"
                    body = STYLE + ". " + body
                    if len(body) + len(suf) > 720:
                        body = body[:700].rsplit(" ", 1)[0] + "..."
                    c["visual_prompt"] = re.sub(r"\s+", " ", body).strip() + suf
                    neg = c.get("negative_prompt") or ""
                    for frag in (
                        "photoreal human",
                        "live-action person",
                        "realistic skin pores photography",
                    ):
                        if frag not in neg.lower():
                            neg = f"{neg}, {frag}" if neg else frag
                    c["negative_prompt"] = neg
                    print(f"patched S{sc.get('scene_number')}C{c.get('clip_number')}")
        path.write_text(
            json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        print("saved", path)


if __name__ == "__main__":
    main()
