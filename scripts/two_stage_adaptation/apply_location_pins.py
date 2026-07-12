#!/usr/bin/env python3
"""
Apply location inventory pins into Stage 1 scenes.json and Stage 2 clips.grok.json.

Does NOT re-LLM the film. Implements location_ids / location_seed_tokens /
clip.location_id per Stage 1/2 prompts, using location_inventory.json.

Usage (repo root):
  python scripts/two_stage_adaptation/apply_location_pins.py
  python scripts/two_stage_adaptation/apply_location_pins.py --project NickAndMe
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[2]


def _project_dir(project_id: Optional[str]) -> Path:
    if project_id:
        return ROOT / "projects" / project_id
    ws = ROOT / "projects" / "workspace.json"
    pid = "NickAndMe"
    if ws.is_file():
        try:
            pid = json.loads(ws.read_text(encoding="utf-8")).get("active_project") or pid
        except (json.JSONDecodeError, OSError):
            pass
    return ROOT / "projects" / str(pid)


def _backup(path: Path) -> Path:
    ts = time.strftime("%Y%m%d_%H%M%S")
    bak = path.with_suffix(path.suffix + f".bak_loc_{ts}")
    shutil.copy2(path, bak)
    return bak


def _load(path: Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _save(path: Path, data: Dict[str, Any]) -> None:
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


# Keywords for assigning clip location when scene has multiple Loc_*
LOC_KEYWORDS: Dict[str, List[str]] = {
    "Loc_Moxie_Gym": ["moxie", "gym", "preacher curl", "curl bench", "weights"],
    "Loc_Kirk_Alley": ["alley", "kickball", "garbage can", "window smash", "mrs. engel"],
    "Loc_Metal_Factory": ["factory", "metal", "crane", "assembly"],
    "Loc_Pizza_Place": ["pizza", "polo", "delivery"],
    "Loc_Joes_Bar": ["joe's", "joes", "bar", "pool table", "smoky"],
    "Loc_Java_Hut": ["java hut", "coffee", "coffee shop"],
    "Loc_Public_Library": ["library", "bookshelf", "stacks"],
    "Loc_Yoga_Studio": ["yoga", "mat", "studio"],
    "Loc_Sionna_Home": ["sionna", "duplex", "her house", "her bedroom"],
    "Loc_Nick_Apt": ["nick's apartment", "werner", "nick apartment"],
    "Loc_Hospital": ["hospital", "er ", "icu", "doctor", "nurse", "life support"],
    "Loc_Cemetery_Church": ["cemetery", "funeral", "st. patrick", "burial", "mass"],
    "Loc_Lake": ["lake", "pier", "mendota", "lakeside", "water", "serenity"],
    "Loc_Writing_Studio": ["writing studio", "studio", "notebooks", "typewriter"],
    "Loc_University": ["university", "campus", "professor", "college"],
    "Loc_Door_County_Farm": ["farm", "mushroom", "ginger", "door county", "farmhouse"],
    "Loc_Road_Travel": ["road", "highway", "hatchback", "driving", "bus", "car interior"],
    "Loc_Milwaukee_Street": ["street", "sidewalk", "downtown", "crosswalk"],
    "Loc_Dairyland": ["dairyland", "ice cream"],
    "Loc_College_Classroom": ["classroom", "english class", "philosophy"],
    "Loc_Friend_House_Flashback": ["steve", "donny", "yard"],
    "Loc_Park": ["park", "autumn", "montage"],
    "Loc_Kirk_Apt": [
        "apartment",
        "kirk",
        "kitchen",
        "bedroom",
        "bathroom",
        "living room",
        "window",
        "hallway",
        "ma ",
    ],
}


def _score_loc(text: str, loc_id: str) -> int:
    blob = (text or "").lower()
    score = 0
    for kw in LOC_KEYWORDS.get(loc_id, []):
        if kw.lower() in blob:
            score += 2 if len(kw) > 4 else 1
    # id token fragments
    frag = loc_id.replace("Loc_", "").replace("_", " ").lower()
    if frag and frag in blob:
        score += 1
    return score


def pick_clip_location(
    location_ids: List[str],
    *,
    visual_prompt: str = "",
    setting: str = "",
    beat_location: Optional[str] = None,
    clip_index: int = 0,
    n_clips: int = 1,
) -> str:
    if beat_location and beat_location in location_ids:
        return beat_location
    if not location_ids:
        return "Loc_Unknown"
    if len(location_ids) == 1:
        return location_ids[0]

    text = f"{visual_prompt} {setting}"
    scored = [(lid, _score_loc(text, lid)) for lid in location_ids]
    scored.sort(key=lambda x: (-x[1], location_ids.index(x[0])))
    if scored[0][1] > 0:
        return scored[0][0]

    # Multi-place without cues: early clips → first id, later → last id
    if n_clips <= 1:
        return location_ids[0]
    mid = max(1, n_clips // 2)
    return location_ids[0] if clip_index < mid else location_ids[-1]


def build_location_seeds(inventory: Dict[str, Any]) -> Dict[str, Any]:
    seeds_in = inventory.get("draft_location_seed_tokens") or {}
    locs = inventory.get("locations") or {}
    out: Dict[str, Any] = {}
    for lid, draft in seeds_in.items():
        meta = locs.get(lid) or {}
        display = draft.get("display_name") or meta.get("display_name") or lid
        notes = draft.get("description") or meta.get("notes") or display
        visual = (draft.get("visual_lock") or "").strip()
        if not visual:
            # Bootstrap visual_lock from notes + lighting samples
            lights = meta.get("lighting_samples") or []
            light_bit = lights[0] if lights else ""
            visual = (
                f"{display}: {notes}. "
                f"Keep architecture, layout, and signature props consistent on every return. "
                f"{('Lighting baseline: ' + light_bit) if light_bit else ''}"
            ).strip()
        out[lid] = {
            "display_name": display,
            "description": notes,
            "visual_lock": visual[:500],
            "reference_image_placeholder": draft.get("reference_image_placeholder")
            or f"assets/locations/{lid.lower()}_ref.png",
        }
    return out


def apply_to_stage1(
    stage1: Dict[str, Any],
    inventory: Dict[str, Any],
    seeds: Dict[str, Any],
) -> Tuple[int, int]:
    gpv = stage1.setdefault("global_production_variables", {})
    gpv["location_seed_tokens"] = seeds

    by_scene = {
        int(s["scene_number"]): s["location_ids"]
        for s in inventory.get("scenes") or []
        if s.get("scene_number") is not None
    }
    n_scenes = 0
    n_beats = 0
    for sc in stage1.get("scenes") or []:
        sn = int(sc.get("scene_number") or 0)
        lids = list(by_scene.get(sn) or sc.get("location_ids") or [])
        if not lids:
            lids = ["Loc_Unknown"]
        sc["location_ids"] = lids
        sc["primary_location_id"] = lids[0]
        n_scenes += 1
        # Optional beat pins when multi-place
        beats = sc.get("story_beats") or []
        for i, beat in enumerate(beats):
            if not isinstance(beat, dict):
                continue
            if len(lids) == 1:
                beat["location_id"] = lids[0]
            else:
                ve = beat.get("visual_event") or beat.get("intent") or ""
                beat["location_id"] = pick_clip_location(
                    lids,
                    visual_prompt=ve,
                    setting=sc.get("setting") or "",
                    beat_location=beat.get("location_id"),
                    clip_index=i,
                    n_clips=len(beats),
                )
            n_beats += 1
    return n_scenes, n_beats


def apply_to_stage2(
    stage2: Dict[str, Any],
    inventory: Dict[str, Any],
    seeds: Dict[str, Any],
    *,
    inject_visual_lock: bool = True,
) -> Tuple[int, int]:
    gpv = stage2.setdefault("global_production_variables", {})
    gpv["location_seed_tokens"] = seeds

    by_scene = {
        int(s["scene_number"]): s["location_ids"]
        for s in inventory.get("scenes") or []
        if s.get("scene_number") is not None
    }
    n_scenes = 0
    n_clips = 0
    for sc in stage2.get("scenes") or []:
        sn = int(sc.get("scene_number") or 0)
        lids = list(by_scene.get(sn) or sc.get("location_ids") or [])
        if not lids:
            lids = ["Loc_Unknown"]
        sc["location_ids"] = lids
        sc["primary_location_id"] = lids[0]
        n_scenes += 1
        clips = sc.get("veo_clips") or []
        for i, clip in enumerate(clips):
            if not isinstance(clip, dict):
                continue
            lid = pick_clip_location(
                lids,
                visual_prompt=clip.get("visual_prompt") or "",
                setting=sc.get("setting") or "",
                beat_location=clip.get("location_id"),
                clip_index=i,
                n_clips=len(clips),
            )
            clip["location_id"] = lid
            n_clips += 1
            if inject_visual_lock and lid in seeds:
                lock = (seeds[lid].get("visual_lock") or seeds[lid].get("display_name") or "").strip()
                vp = clip.get("visual_prompt") or ""
                # Only inject once
                if lock and lock[:40].lower() not in vp.lower()[:120]:
                    core = re.sub(r"\s*/\s*\d+p.*$", "", vp, flags=re.I).strip()
                    # Keep prompt budget: short lock prefix
                    short_lock = lock[:140].rsplit(" ", 1)[0] if len(lock) > 140 else lock
                    suffix_m = re.search(r"\s*/\s*\d+p,\s*24fps\s*$", vp, re.I)
                    suffix = suffix_m.group(0) if suffix_m else " / 720p, 24fps"
                    new_vp = f"{short_lock} {core}{suffix}"
                    if len(new_vp) > 800:
                        # drop injection if it blows hard limit
                        pass
                    else:
                        clip["visual_prompt"] = new_vp
    return n_scenes, n_clips


def verify(
    stage1: Dict[str, Any],
    stage2: Dict[str, Any],
) -> List[str]:
    errs: List[str] = []
    seeds1 = (stage1.get("global_production_variables") or {}).get("location_seed_tokens") or {}
    seeds2 = (stage2.get("global_production_variables") or {}).get("location_seed_tokens") or {}
    if not seeds1:
        errs.append("Stage1 missing location_seed_tokens")
    if not seeds2:
        errs.append("Stage2 missing location_seed_tokens")

    for sc in stage1.get("scenes") or []:
        sn = sc.get("scene_number")
        lids = sc.get("location_ids") or []
        if not lids:
            errs.append(f"Stage1 S{sn}: empty location_ids")
        for lid in lids:
            if lid not in seeds1 and lid != "Loc_Unknown":
                errs.append(f"Stage1 S{sn}: location_id {lid} not in seeds")

    for sc in stage2.get("scenes") or []:
        sn = sc.get("scene_number")
        lids = sc.get("location_ids") or []
        if not lids:
            errs.append(f"Stage2 S{sn}: empty location_ids")
        for c in sc.get("veo_clips") or []:
            lid = c.get("location_id")
            cn = c.get("clip_number")
            if not lid:
                errs.append(f"Stage2 S{sn}C{cn}: missing location_id")
            elif lids and lid not in lids:
                errs.append(f"Stage2 S{sn}C{cn}: location_id {lid} not in scene location_ids {lids}")
            elif lid not in seeds2 and lid != "Loc_Unknown":
                errs.append(f"Stage2 S{sn}C{cn}: location_id {lid} not in seeds")
    return errs


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--project", default=None)
    ap.add_argument("--inventory", default=None, help="Path to location_inventory.json")
    ap.add_argument("--scenes", default=None, help="Stage 1 scenes.json path")
    ap.add_argument("--clips", default=None, help="Stage 2 clips.grok.json path")
    ap.add_argument("--no-inject-lock", action="store_true", help="Do not prefix visual_prompt")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    project = _project_dir(args.project)
    inv_path = Path(args.inventory) if args.inventory else project / "location_inventory.json"
    if not inv_path.is_file():
        raise SystemExit(
            f"Missing {inv_path}. Run: python scripts/inventory_locations.py"
        )

    scenes_path = Path(args.scenes) if args.scenes else project / "nickandme.scenes.json"
    clips_path = Path(args.clips) if args.clips else project / "nickandme.clips.grok.json"
    if not scenes_path.is_file():
        raise SystemExit(f"Missing Stage 1: {scenes_path}")
    if not clips_path.is_file():
        raise SystemExit(f"Missing Stage 2: {clips_path}")

    inventory = _load(inv_path)
    seeds = build_location_seeds(inventory)
    stage1 = _load(scenes_path)
    stage2 = _load(clips_path)

    s1n, b1n = apply_to_stage1(stage1, inventory, seeds)
    s2n, c2n = apply_to_stage2(
        stage2,
        inventory,
        seeds,
        inject_visual_lock=not args.no_inject_lock,
    )

    errs = verify(stage1, stage2)
    print(f"Stage1 scenes pinned: {s1n} (beats touched: {b1n})")
    print(f"Stage2 scenes pinned: {s2n} (clips pinned: {c2n})")
    print(f"location_seed_tokens: {len(seeds)}")
    if errs:
        print(f"VERIFY FAIL ({len(errs)}):")
        for e in errs[:40]:
            print(" ", e)
        raise SystemExit(1)
    print("VERIFY OK — all scenes/clips have location pins consistent with seeds")

    if args.dry_run:
        print("Dry run — no files written")
        return

    b1 = _backup(scenes_path)
    b2 = _backup(clips_path)
    _save(scenes_path, stage1)
    _save(clips_path, stage2)
    print(f"Wrote {scenes_path} (backup {b1.name})")
    print(f"Wrote {clips_path} (backup {b2.name})")


if __name__ == "__main__":
    main()
