"""
Build a location inventory from the active Stage 2 blueprint.

Writes:
  projects/<active>/location_inventory.json
  projects/<active>/location_inventory.md

Run from workspace root:
  python scripts/inventory_locations.py
"""
from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]


def _active_project() -> Path:
    ws = ROOT / "projects" / "workspace.json"
    pid = "NickAndMe"
    if ws.is_file():
        try:
            data = json.loads(ws.read_text(encoding="utf-8"))
            pid = data.get("active_project") or pid
        except (json.JSONDecodeError, OSError):
            pass
    return ROOT / "projects" / str(pid)


def _load_blueprint(project: Path) -> Dict[str, Any]:
    # Prefer project meta / config blueprint_file
    name = "nickandme.clips.grok.json"
    meta_path = project / "project.json"
    if meta_path.is_file():
        try:
            meta = json.loads(meta_path.read_text(encoding="utf-8"))
            name = meta.get("blueprint_file") or name
        except (json.JSONDecodeError, OSError):
            pass
    cfg_path = project / "pipeline_config.json"
    if cfg_path.is_file():
        try:
            cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
            name = cfg.get("blueprint_file") or name
        except (json.JSONDecodeError, OSError):
            pass
    path = project / name
    if not path.is_file():
        # fallback any *.clips.grok.json
        cands = sorted(project.glob("*.clips.grok.json"))
        if not cands:
            raise FileNotFoundError(f"No blueprint in {project}")
        path = cands[0]
    return json.loads(path.read_text(encoding="utf-8")), path


# (pattern, loc_id, display_name, notes)
# Order matters: first match wins (more specific before generic).
RULES: List[Tuple[re.Pattern[str], str, str, str]] = [
    (re.compile(r"kirk\s*street\s*alley|alley.*flashback|flashback.*alley", re.I),
     "Loc_Kirk_Alley", "Kirk Street alley", "Childhood kickball / window smash flashback"),
    (re.compile(r"moxie", re.I),
     "Loc_Moxie_Gym", "Moxie's Gym", "Corner gym; preacher curls, fluorescent light"),
    (re.compile(r"metal\s*factory", re.I),
     "Loc_Metal_Factory", "Metal factory", "Nick's workplace; industrial"),
    (re.compile(r"pizza", re.I),
     "Loc_Pizza_Place", "Pizza place", "Narrator workplace / sidewalk outside"),
    (re.compile(r"joe['’`]?s\s*bar", re.I),
     "Loc_Joes_Bar", "Joe's Bar", "Smoky bar; pool, brawls"),
    (re.compile(r"java\s*hut|coffee\s*shop", re.I),
     "Loc_Java_Hut", "The Java Hut", "Downtown coffee shop; Sionna meet-cute"),
    (re.compile(r"public\s*library|(?<!university\s)library", re.I),
     "Loc_Public_Library", "Public library", "Self-study / research"),
    (re.compile(r"yoga", re.I),
     "Loc_Yoga_Studio", "Yoga studio", "Sionna's practice space"),
    (re.compile(r"dairyland|ice\s*cream", re.I),
     "Loc_Dairyland", "Dairyland ice cream stand", "Exterior treat stand; conquering fear beat"),
    (re.compile(r"community\s*college|english\s*class|philosophy\s*classroom|classroom", re.I),
     "Loc_College_Classroom", "Community college classroom", "Return to school; English / philosophy"),
    (re.compile(r"doctor['’`]?s\s*office|hospital|icu|life\s*support|emergency|\ber\b", re.I),
     "Loc_Hospital", "Hospital / doctor's office", "Medical, ER, ICU"),
    (re.compile(r"cemetery|burial|funeral|st\.?\s*patrick", re.I),
     "Loc_Cemetery_Church", "Cemetery / St. Patrick's", "Funeral, mass, burial"),
    (re.compile(r"lake\s*mendota|lakeside|pier|lake\s*of\s*serenity|dreamscape", re.I),
     "Loc_Lake", "Lake Mendota / lakeside pier", "Golden hour walks, pier, dream-lake, ending"),
    (re.compile(r"writing\s*studio", re.I),
     "Loc_Writing_Studio", "Writing studio", "Present-day framing; finishing the book"),
    (re.compile(r"university|milwaukee\s*u|campus|professor", re.I),
     "Loc_University", "Milwaukee University / campus", "Classes, professor office"),
    (re.compile(r"mushroom|ginger|door\s*county|farmhouse|farm\b", re.I),
     "Loc_Door_County_Farm", "Door County farmhouse", "Mushroom & Ginger; country visit"),
    (re.compile(r"road\s*trip|rainy\s*highway|eastern\s*wisconsin|pastoral|hatchback|country\s*road|\bbus\b", re.I),
     "Loc_Road_Travel", "Road / car / bus", "Driving beats; panic drive; bus"),
    (re.compile(r"nick['’`]?s\s*apartment|werner", re.I),
     "Loc_Nick_Apt", "Nick's apartment", "Werner Street; life without parole beat"),
    (re.compile(r"sionna['’`]?s\s*(duplex|house|place|home|bedroom)|sionna", re.I),
     "Loc_Sionna_Home", "Sionna's home", "Duplex / bedroom / couple scenes"),
    (re.compile(r"steve['’`]?s\s*house|donny['’`]?s\s*house", re.I),
     "Loc_Friend_House_Flashback", "Friend's house (flashback)", "Steve's yard / Donny's house one-offs"),
    (re.compile(r"\bpark\b|seasonal\s*passage|montage", re.I),
     "Loc_Park", "City park", "Exterior park / seasonal montage"),
    (re.compile(
        r"narrator['’`]?s\s*apartment|narrator['’`]?s\s*bathroom|narrator['’`]?s\s*bedroom|"
        r"apartment\s*\(|\bkitchen\b|\bbedroom\b|\bbathroom\b|living\s*room|kirk\s*street",
        re.I,
     ),
     "Loc_Kirk_Apt", "Kirk Street apartment", "5th-floor walk-up; Ma & narrator home"),
    (re.compile(r"street|sidewalk|downtown", re.I),
     "Loc_Milwaukee_Street", "Milwaukee street / sidewalk", "Generic exterior city"),
]


def _strip_time_prefix(text: str) -> str:
    """Remove leading 'Day 9 -' / 'Night 130 / Day 131 -' style prefixes for matching."""
    t = (text or "").strip()
    # Drop pure day/night tokens when splitting multi-place lines
    if re.fullmatch(r"(present\s*day|day|night)\s*\d*", t, re.I):
        return ""
    t = re.sub(
        r"^(?:present\s*day|day|night)\s*\d*(?:\s*/\s*(?:day|night)\s*\d*)?\s*[-–—:]\s*",
        "",
        t,
        flags=re.I,
    )
    return t.strip()


def _match_one(text: str) -> Optional[str]:
    t = _strip_time_prefix(text)
    if not t:
        return None
    # Ignore credit / title-card tails
    if re.search(r"end\s*credits|title\s*card|fade\s*to\s*black", t, re.I):
        return None
    for pat, loc_id, _disp, _note in RULES:
        if pat.search(t):
            return loc_id
    if re.search(r"apartment|kitchen|living\s*room|bathroom", t, re.I):
        return "Loc_Kirk_Apt"
    if re.search(r"dreamscape|dream\s*sequence|serenity", t, re.I):
        return "Loc_Dreamscape"
    return None


def classify(setting: str) -> List[str]:
    """Return one or more Loc_* ids for a setting string (story order)."""
    if not setting:
        return ["Loc_Unknown"]
    ids: List[str] = []

    # Scan slash-separated segments first so order follows story (Apt / Gym → Apt then Gym)
    for part in re.split(r"\s*/\s*", setting):
        matched = _match_one(part)
        if matched and matched not in ids:
            ids.append(matched)

    # Full-string match for settings without useful slash parts
    if not ids:
        full = _match_one(setting)
        if full:
            ids.append(full)

    if not ids:
        ids.append("Loc_Unknown")
    return ids


def main() -> None:
    project = _active_project()
    data, bp_path = _load_blueprint(project)
    scenes = data.get("scenes") or []

    loc_meta: Dict[str, Dict[str, Any]] = {}
    for pat, loc_id, display, notes in RULES:
        loc_meta[loc_id] = {
            "id": loc_id,
            "display_name": display,
            "notes": notes,
            "pattern_hint": pat.pattern,
            "scenes": [],
            "setting_strings": [],
            "lighting_samples": [],
        }
    loc_meta["Loc_Unknown"] = {
        "id": "Loc_Unknown",
        "display_name": "(unclassified)",
        "notes": "Needs manual assignment",
        "pattern_hint": "",
        "scenes": [],
        "setting_strings": [],
        "lighting_samples": [],
    }

    scene_rows: List[Dict[str, Any]] = []
    for s in scenes:
        sn = int(s.get("scene_number") or 0)
        setting = (s.get("setting") or "").strip()
        light = (s.get("lighting_continuity_token") or "").strip()
        ids = classify(setting)
        scene_rows.append(
            {
                "scene_number": sn,
                "setting": setting,
                "story_day": s.get("story_day"),
                "lighting_continuity_token": light,
                "location_ids": ids,
            }
        )
        for lid in ids:
            row = loc_meta[lid]
            row["scenes"].append(sn)
            if setting and setting not in row["setting_strings"]:
                row["setting_strings"].append(setting)
            if light and light not in row["lighting_samples"] and len(row["lighting_samples"]) < 5:
                row["lighting_samples"].append(light[:160])

    # Draft seed shells (for future Stage 1 / blueprint merge)
    seeds: Dict[str, Dict[str, Any]] = {}
    for lid, meta in loc_meta.items():
        if lid == "Loc_Unknown" and not meta["scenes"]:
            continue
        if not meta["scenes"] and lid != "Loc_Unknown":
            continue
        seeds[lid] = {
            "display_name": meta["display_name"],
            "description": meta["notes"],
            "visual_lock": "",  # fill later: architecture, palette, signature props
            "reference_image_placeholder": f"assets/locations/{lid.lower()}_ref.png",
            "scene_count": len(meta["scenes"]),
            "scene_numbers": meta["scenes"],
        }

    inventory = {
        "schema_version": "location_inventory.v1",
        "project": project.name,
        "source_blueprint": str(bp_path.relative_to(ROOT)).replace("\\", "/"),
        "scene_count": len(scene_rows),
        "location_count": sum(1 for m in loc_meta.values() if m["scenes"]),
        "locations": {
            lid: {
                "id": lid,
                "display_name": m["display_name"],
                "notes": m["notes"],
                "scene_count": len(m["scenes"]),
                "scene_numbers": m["scenes"],
                "setting_strings": m["setting_strings"],
                "lighting_samples": m["lighting_samples"],
            }
            for lid, m in loc_meta.items()
            if m["scenes"]
        },
        "scenes": scene_rows,
        "draft_location_seed_tokens": seeds,
        "next_steps": [
            "Review Loc_* assignments; reassign Loc_Unknown / split multi-place scenes if needed",
            "Fill visual_lock one-liners for top recurring locations",
            "Optionally generate/lock location plate images under assets/locations/",
            "Wire location_seed_tokens into Stage 1 schema + Stage 2 prompt injection",
        ],
    }

    out_json = project / "location_inventory.json"
    out_md = project / "location_inventory.md"
    out_json.write_text(json.dumps(inventory, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    # Markdown summary
    lines = [
        f"# Location inventory — {project.name}",
        "",
        f"Source: `{inventory['source_blueprint']}` · **{inventory['scene_count']}** scenes · "
        f"**{inventory['location_count']}** locations classified",
        "",
        "Generated by `python scripts/inventory_locations.py`.",
        "",
        "## Recurring locations (by scene count)",
        "",
        "| Id | Display name | Scenes | Scene #s |",
        "|----|--------------|--------|----------|",
    ]
    ranked = sorted(
        inventory["locations"].values(),
        key=lambda x: (-int(x["scene_count"]), x["id"]),
    )
    for m in ranked:
        nums = m["scene_numbers"]
        nums_s = ", ".join(str(n) for n in nums[:20])
        if len(nums) > 20:
            nums_s += f" … (+{len(nums) - 20})"
        lines.append(
            f"| `{m['id']}` | {m['display_name']} | {m['scene_count']} | {nums_s} |"
        )

    lines += ["", "## Scene → location map", "", "| Scene | Location ids | Setting |", "|------|--------------|---------|"]
    for r in scene_rows:
        lids = ", ".join(f"`{x}`" for x in r["location_ids"])
        setting = (r["setting"] or "").replace("|", "/")
        lines.append(f"| {r['scene_number']} | {lids} | {setting[:70]} |")

    lines += [
        "",
        "## Draft seed tokens",
        "",
        "See `location_inventory.json` → `draft_location_seed_tokens`.",
        "Fill `visual_lock` before wiring into Stage 2.",
        "",
        "## Next steps",
        "",
    ]
    for step in inventory["next_steps"]:
        lines.append(f"- {step}")
    lines.append("")

    out_md.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out_json}")
    print(f"Wrote {out_md}")
    print(f"Locations: {inventory['location_count']} · Unknown scenes: "
          f"{len(loc_meta['Loc_Unknown']['scenes'])}")


if __name__ == "__main__":
    main()
