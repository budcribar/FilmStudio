#!/usr/bin/env python3
"""Replan Buster Stage 2 with current planner (P0)."""
from __future__ import annotations

import json
import shutil
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from scripts.two_stage_adaptation import stage2_plan_grok as mod  # noqa: E402


def main() -> None:
    proj = ROOT / "projects" / "Buster"
    stage1 = json.loads((proj / "scenes.json").read_text(encoding="utf-8"))
    gpv = dict(stage1.get("global_production_variables") or {})
    loc = gpv.get("location_seed_tokens") or {}
    char = gpv.get("character_seed_tokens") or {}
    planned = [
        mod.plan_scene(
            s, resolution="720p", location_seeds=loc, character_seeds=char
        )
        for s in stage1.get("scenes") or []
    ]
    out = proj / "blueprint.clips.grok.json"
    old: dict = {}
    if out.is_file():
        old = json.loads(out.read_text(encoding="utf-8"))
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        shutil.copy2(out, out.with_suffix(out.suffix + f".bak_pre_stage2_{stamp}"))

    old_seeds = (old.get("global_production_variables") or {}).get(
        "character_seed_tokens"
    ) or {}
    new_seeds = dict(gpv.get("character_seed_tokens") or {})
    for k, v in old_seeds.items():
        if isinstance(v, dict) and k in new_seeds and isinstance(new_seeds[k], dict):
            for field in (
                "design_reference_images",
                "book_reference_images",
                "reference_image_placeholder",
            ):
                if v.get(field) and not new_seeds[k].get(field):
                    new_seeds[k][field] = v[field]
    gpv["character_seed_tokens"] = new_seeds

    plan = {
        "schema_version": "stage2.v1",
        "movie_title": stage1.get("movie_title"),
        "source_book_title": stage1.get("source_book_title"),
        "video_provider_profile": "grok",
        "global_production_variables": gpv,
        "scenes": planned,
        "stage2_meta": {
            "source_stage1": "scenes.json",
            "resolution": "720p",
            "stage1_fingerprint": mod.stage1_content_fingerprint(stage1),
            "completed_at": datetime.now().isoformat(timespec="seconds"),
            "last_run_ok": True,
            "last_run_message": f"Replanned P0: {len(planned)} scenes",
            "total_clips": sum(len(s.get("veo_clips") or []) for s in planned),
        },
    }
    out.write_text(json.dumps(plan, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    for sn, cn in ((1, 1), (1, 2), (5, 1), (6, 2)):
        for sc in plan["scenes"]:
            if sc.get("scene_number") != sn:
                continue
            for c in sc.get("veo_clips") or []:
                if c.get("clip_number") != cn:
                    continue
                vp = c.get("visual_prompt") or ""
                print(
                    f"S{sn}C{cn} len={len(vp)} "
                    f"hat={'hat' in vp.lower()} "
                    f"pj={'pajama' in vp.lower()} "
                    f"daddy={'Daddy' in vp} "
                    f"pollute={'never brown' in vp.lower()}"
                )
                print(" ", vp[:180])
    print("fp", plan["stage2_meta"]["stage1_fingerprint"])
    print("saved", out)


if __name__ == "__main__":
    main()
