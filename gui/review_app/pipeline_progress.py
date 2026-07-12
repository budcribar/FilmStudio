"""
Pipeline progress for sidebar navigation markers.

Fast path: read project JSON/files only (no AgenticGenerationEngine init).
Cached in st.session_state keyed by file mtimes so menu switches stay snappy.
"""
from __future__ import annotations

import json
import os
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

from review_app.paths import get_active_project_dir, load_project_meta


def _mtime(path: Path) -> float:
    try:
        return path.stat().st_mtime
    except OSError:
        return 0.0


def _read_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except (json.JSONDecodeError, OSError):
        return {}


def _project_files(proj: Path, meta: Dict[str, Any]) -> Dict[str, Path]:
    scenes_name = meta.get("scenes_file") or "scenes.json"
    bp_name = meta.get("blueprint_file") or "blueprint.clips.grok.json"
    scenes = proj / scenes_name
    if not scenes.is_file():
        alt = proj / "nickandme.scenes.json"
        if alt.is_file():
            scenes = alt
    return {
        "scenes": scenes,
        "blueprint": proj / bp_name,
        "state": proj / (meta.get("state_file") or "pipeline_state.json"),
        "config": proj / (meta.get("config_file") or "pipeline_config.json"),
        "extract_meta": proj / "source" / "extract_meta.json",
        "book": proj / "source" / "book_full.txt",
        "edit_log": proj / "edit_feedback_log.json",
        "chars_dir": proj / "assets" / "characters",
    }


def _fingerprint(files: Dict[str, Path]) -> str:
    parts = []
    for key in (
        "scenes",
        "blueprint",
        "state",
        "config",
        "extract_meta",
        "book",
        "edit_log",
        "chars_dir",
    ):
        p = files.get(key)
        if p is None:
            continue
        parts.append(f"{key}:{_mtime(p):.0f}")
    return "|".join(parts)


def _count_locked_refs(chars_dir: Path, seed_keys: List[str]) -> Tuple[int, int, int]:
    """
    Return (locked_on_screen, need_on_screen, locked_all).

    Skips Narrator / never_on_screen for "need" count when we only have keys.
    """
    if not seed_keys:
        return 0, 0, 0
    locked_all = 0
    locked_need = 0
    need = 0
    for key in seed_keys:
        is_narr = key.endswith("_Narrator") or key == "Character_Narrator"
        if not is_narr:
            need += 1
        # common ref naming from engine
        slug = key.lower().replace("character_", "character_")
        candidates = [
            chars_dir / f"{slug}_ref.png",
            chars_dir / f"{key.lower()}_ref.png",
            chars_dir / f"character_{key.replace('Character_', '').lower()}_ref.png",
        ]
        # also scan for *buster*_ref.png style
        rest = key.replace("Character_", "").lower()
        candidates.append(chars_dir / f"character_{rest}_ref.png")
        found = any(c.is_file() for c in candidates)
        if not found and chars_dir.is_dir():
            # fallback: any file starting with character_<rest>
            try:
                for f in chars_dir.iterdir():
                    if f.is_file() and rest in f.name.lower() and "_ref" in f.name.lower():
                        if "variant" in f.name.lower():
                            continue
                        found = True
                        break
            except OSError:
                pass
        if found:
            locked_all += 1
            if not is_narr:
                locked_need += 1
    return locked_need, need, locked_all


def _pipeline_progress_uncached() -> Dict[str, Any]:
    proj = get_active_project_dir()
    if proj is None or not proj.is_dir():
        steps = _empty_steps()
        return {
            "steps": steps,
            "next_id": "adaptation",
            "labels": {s["id"]: nav_label(s) for s in steps},
            "adapt_done": False,
            "config_ready": False,
            "config_done": False,
            "chars_done": False,
            "scenes_done": False,
            "cached": False,
        }

    meta = load_project_meta(proj)
    files = _project_files(proj, meta)
    s1 = _read_json(files["scenes"])
    bp = _read_json(files["blueprint"])
    state = _read_json(files["state"])
    extract = _read_json(files["extract_meta"])
    log = _read_json(files["edit_log"])
    ui = state.get("ui_progress") if isinstance(state.get("ui_progress"), dict) else {}

    s1_scenes = s1.get("scenes") if isinstance(s1.get("scenes"), list) else []
    adapt_done = len(s1_scenes) > 0
    adapt_detail = f"{len(s1_scenes)} scenes" if adapt_done else ""
    s1_gpv = s1.get("global_production_variables") or {}
    s1_seeds = s1_gpv.get("character_seed_tokens") or {}
    if not isinstance(s1_seeds, dict):
        s1_seeds = {}

    book_ready = bool(
        extract.get("ready_for_stage1")
        or extract.get("text_quality") == "good"
        or files["book"].is_file()
    )
    if not adapt_done and book_ready:
        adapt_detail = "book ready"

    bp_gpv = bp.get("global_production_variables") or {}
    bp_seeds = bp_gpv.get("character_seed_tokens") or {}
    if not isinstance(bp_seeds, dict):
        bp_seeds = {}
    seed_keys = list(bp_seeds.keys()) or list(s1_seeds.keys())

    locked_need, need_lock, locked_all = _count_locked_refs(files["chars_dir"], seed_keys)
    chars_done = bool(
        (need_lock > 0 and locked_need >= need_lock)
        or (need_lock == 0 and locked_all > 0 and locked_all >= len(seed_keys))
        or (ui.get("characters") or {}).get("done")
    )
    if need_lock > 0:
        char_detail = f"{locked_need}/{need_lock} on-screen locked"
    elif seed_keys:
        char_detail = f"{locked_all}/{len(seed_keys)} locked"
    elif adapt_done:
        char_detail = "needs refs"
    else:
        char_detail = ""

    bp_scenes = bp.get("scenes") if isinstance(bp.get("scenes"), list) else []
    scene_count = len(bp_scenes)
    clips_total = 0
    for sc in bp_scenes:
        clips_total += len(sc.get("veo_clips") or [])

    # Approved / clips from state + filesystem (cheap)
    approved_map = state.get("scenes_completed") or state.get("scene_approval") or {}
    if not isinstance(approved_map, dict):
        approved_map = {}
    # engine uses scenes_completed str keys -> bool sometimes; also heroes
    approved = 0
    for sc in bp_scenes:
        sn = sc.get("scene_number")
        if sn is None:
            continue
        key = str(sn)
        if approved_map.get(key) is True or approved_map.get(sn) is True:
            approved += 1
        # alternate: state["approved_scenes"] list
    appr_list = state.get("approved_scenes")
    if isinstance(appr_list, list) and appr_list:
        approved = max(approved, len(appr_list))

    video_dir = proj / "assets" / "video"
    clips_on = 0
    if video_dir.is_dir():
        try:
            clips_on = sum(
                1
                for f in video_dir.iterdir()
                if f.is_file() and f.suffix.lower() == ".mp4" and f.stat().st_size > 1024
            )
        except OSError:
            clips_on = 0

    scenes_done = bool(
        (approved > 0 and clips_on > 0) or (ui.get("scenes") or {}).get("done")
    )
    if scenes_done:
        scene_detail = f"{approved} approved · {clips_on} clips"
    elif scene_count > 0:
        scene_detail = f"{clips_on}/{clips_total or '?'} clips"
    elif adapt_done:
        scene_detail = "needs Stage 2 plan"
    else:
        scene_detail = ""

    config_marked = bool((ui.get("configuration") or {}).get("done"))
    config_done = bool(
        config_marked
        or (adapt_done and (chars_done or locked_all > 0 or clips_on > 0))
    )
    config_ready = adapt_done
    if config_done:
        config_detail = "saved" if config_marked else "ok"
    elif config_ready:
        config_detail = "save to complete"
    else:
        config_detail = ""

    log_count = len(log.get("entries") or []) if isinstance(log.get("entries"), list) else 0
    ledger = state.get("cost_ledger")
    cost_events = len(ledger) if isinstance(ledger, list) else 0

    steps: List[Dict[str, Any]] = [
        {
            "id": "adaptation",
            "title": "Adaptation",
            "done": adapt_done,
            "detail": adapt_detail,
            "core": True,
        },
        {
            "id": "configuration",
            "title": "Configuration",
            "done": config_done,
            "detail": config_detail,
            "core": True,
        },
        {
            "id": "characters",
            "title": "Characters",
            "done": chars_done,
            "detail": char_detail,
            "core": True,
        },
        {
            "id": "scenes",
            "title": "Scenes",
            "done": scenes_done,
            "detail": scene_detail,
            "core": True,
        },
        {
            "id": "edit_log",
            "title": "Edit Log",
            "done": log_count > 0,
            "detail": f"{log_count} notes" if log_count else "",
            "core": False,
        },
        {
            "id": "cost",
            "title": "Cost",
            "done": cost_events > 0,
            "detail": f"{cost_events} events" if cost_events else "",
            "core": False,
        },
    ]

    next_id: Optional[str] = None
    if not adapt_done:
        next_id = "adaptation"
    else:
        for s in steps:
            if s.get("core") and s["id"] != "adaptation" and not s["done"]:
                next_id = s["id"]
                break

    for s in steps:
        s["next"] = s["id"] == next_id and not s["done"]

    return {
        "steps": steps,
        "next_id": next_id,
        "labels": {s["id"]: nav_label(s) for s in steps},
        "adapt_done": adapt_done,
        "config_ready": config_ready,
        "config_done": config_done,
        "chars_done": chars_done,
        "scenes_done": scenes_done,
        "fingerprint": _fingerprint(files),
        "cached": False,
    }


def _empty_steps() -> List[Dict[str, Any]]:
    return [
        {"id": "adaptation", "title": "Adaptation", "done": False, "detail": "", "core": True, "next": True},
        {"id": "configuration", "title": "Configuration", "done": False, "detail": "", "core": True, "next": False},
        {"id": "characters", "title": "Characters", "done": False, "detail": "", "core": True, "next": False},
        {"id": "scenes", "title": "Scenes", "done": False, "detail": "", "core": True, "next": False},
        {"id": "edit_log", "title": "Edit Log", "done": False, "detail": "", "core": False, "next": False},
        {"id": "cost", "title": "Cost", "done": False, "detail": "", "core": False, "next": False},
    ]


def pipeline_progress(*, force: bool = False) -> Dict[str, Any]:
    """
    Fast progress for nav/sidebar. Uses session cache + file mtimes.
    Does not construct the film engine.
    """
    try:
        import streamlit as st

        proj = get_active_project_dir()
        proj_id = proj.name if proj else ""
        meta = load_project_meta(proj) if proj else {}
        files = _project_files(proj, meta) if proj else {}
        fp = _fingerprint(files) if files else "none"
        cache_key = f"pipeline_progress::{proj_id}"
        cached = st.session_state.get(cache_key)
        if (
            not force
            and isinstance(cached, dict)
            and cached.get("fingerprint") == fp
            and (time.time() - float(cached.get("_ts") or 0)) < 30.0
        ):
            out = dict(cached)
            out["cached"] = True
            return out
        result = _pipeline_progress_uncached()
        result["_ts"] = time.time()
        st.session_state[cache_key] = result
        return result
    except Exception:
        # Outside Streamlit or paths unavailable
        return _pipeline_progress_uncached()


def nav_label(step: Dict[str, Any]) -> str:
    title = step.get("title") or step.get("id") or "?"
    if step.get("done"):
        return f"✓ {title}"
    if step.get("next"):
        return f"→ {title}"
    return str(title)


def render_sidebar_progress(progress: Optional[Dict[str, Any]] = None) -> None:
    """Compact checklist under the project switcher (expects progress already computed)."""
    import streamlit as st

    prog = progress if progress is not None else pipeline_progress()
    lines = []
    for s in prog.get("steps") or []:
        if s.get("done"):
            mark = "✓"
        elif s.get("next"):
            mark = "→"
        else:
            mark = "·"
        detail = s.get("detail") or ""
        extra = f" · {detail}" if detail else ""
        lines.append(f"{mark} {s.get('title')}{extra}")
    st.caption("Pipeline")
    st.markdown(
        "<div style='font-size:0.8rem;line-height:1.45;opacity:0.9'>"
        + "<br/>".join(lines)
        + "</div>",
        unsafe_allow_html=True,
    )
    nxt = prog.get("next_id")
    if nxt:
        title = next(
            (s.get("title") for s in (prog.get("steps") or []) if s.get("id") == nxt),
            nxt,
        )
        st.caption(f"Next up: **{title}**")


def invalidate_progress_cache() -> None:
    """Call after save/lock/stage1 so next nav refresh is accurate."""
    try:
        import streamlit as st

        for k in list(st.session_state.keys()):
            if str(k).startswith("pipeline_progress::"):
                del st.session_state[k]
    except Exception:
        pass
