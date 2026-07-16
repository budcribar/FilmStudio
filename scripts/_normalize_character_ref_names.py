#!/usr/bin/env python3
"""Rename locked character refs to {character_key_lower}_ref.png and update seeds."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def canonical_ref_name(char_key: str) -> str:
    """Character_Mom → character_mom_ref.png; 'Kevin McCleary' → kevin_mccleary_ref.png."""
    k = (char_key or "").strip().replace(" ", "_").replace("\\", "/").lower()
    k = Path(k).name  # drop any path prefix
    if k.endswith("_ref.png"):
        return k
    return f"{k}_ref.png"


def normalize_project(project_id: str) -> None:
    proj = ROOT / "projects" / project_id
    char_dir = proj / "assets" / "characters"
    if not char_dir.is_dir():
        print(f"skip {project_id}: no characters dir")
        return

    # Known legacy short names → canonical
    legacy = {
        "buster_ref.png": "character_buster_ref.png",
        "mom_ref.png": "character_mom_ref.png",
        "daddy_ref.png": "character_daddy_ref.png",
        "narrator_ref.png": "character_narrator_ref.png",
    }
    for old, new in legacy.items():
        src, dst = char_dir / old, char_dir / new
        if src.is_file() and not dst.is_file():
            src.rename(dst)
            print(f"  renamed {old} → {new}")
        elif src.is_file() and dst.is_file():
            if src.stat().st_size == dst.stat().st_size:
                src.unlink()
                print(f"  removed duplicate {old}")
            else:
                print(f"  both exist size mismatch: {old} / {new}")

    for json_name in ("blueprint.clips.grok.json", "scenes.json", "nickandme.scenes.json", "nickandme.clips.grok.json"):
        path = proj / json_name
        if not path.is_file():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError) as e:
            print(f"  skip {json_name}: {e}")
            continue
        gpv = data.get("global_production_variables")
        if not isinstance(gpv, dict):
            continue
        seeds = gpv.get("character_seed_tokens")
        if not isinstance(seeds, dict):
            continue
        changed = False
        for key, seed in seeds.items():
            if not isinstance(seed, dict):
                continue
            want = canonical_ref_name(str(key))
            if seed.get("reference_image_placeholder") != want:
                old = seed.get("reference_image_placeholder")
                seed["reference_image_placeholder"] = want
                changed = True
                print(f"  {json_name}: {key} placeholder {old!r} → {want!r}")
        if changed:
            path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
            print(f"  wrote {json_name}")


def main() -> None:
    projects = ROOT / "projects"
    for d in sorted(projects.iterdir()):
        if not d.is_dir() or d.name.startswith("."):
            continue
        if not (d / "project.json").is_file() and not (d / "blueprint.clips.grok.json").is_file():
            # still try nickandme-style
            if not any(d.glob("*.json")):
                continue
        print(f"=== {d.name} ===")
        normalize_project(d.name)


if __name__ == "__main__":
    main()
