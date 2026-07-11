"""
Phase A learning loop: feedback layers + scene dirty flags.

Layers (route feedback to one primary home):
  clip      — this take / visual_prompt only
  stage2    — shot plan / Stage 2 prompt policy
  stage1    — story bible (implies Stage 2 replan after)
  verifier  — improve detection rubric
  engine    — renderer behavior (code PR, not prompt spray)

Dirty flags live in pipeline_state.json under scene_dirty.
Cascade replan (LLM Stage 1/2 scripts) is still operator-driven in Phase A;
UI marks dirty and shows the checklist.
"""
from __future__ import annotations

import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Sequence

from review_app.paths import workspace_root

# Canonical feedback layers (primary route)
FEEDBACK_LAYERS = ("clip", "stage2", "stage1", "verifier", "engine")

LAYER_LABELS = {
    "clip": "Clip only — fix this take",
    "stage2": "Stage 2 — shot plan / prompt policy",
    "stage1": "Stage 1 — story bible (then Stage 2)",
    "verifier": "Verifier — detection rubric",
    "engine": "Engine — renderer / tooling",
}

# What cascade each layer implies for the scene
LAYER_DIRTY = {
    "clip": (),  # no scene replan required
    "stage2": ("stage2",),
    "stage1": ("stage1", "stage2"),
    "verifier": (),  # product prompt only
    "engine": (),  # code notes only
}

# Default edit-log targets per layer
LAYER_TARGETS = {
    "clip": ["nickandme.clips.grok.json", "assets/video"],
    "stage2": ["prompts/stage2_shot_planner.txt", "nickandme.clips.grok.json"],
    "stage1": [
        "prompts/stage1_scene_bible.txt",
        "nickandme.scenes.json",
        "prompts/stage2_shot_planner.txt",
        "nickandme.clips.grok.json",
    ],
    "verifier": ["prompts/verifier_clip.txt", "prompts/shared_rules.txt"],
    "engine": ["renderer/", "review_feedback/SCRIPT_NOTES.md"],
}

# Prompt files that accept GUI learnings append
PROMPT_LEARNING_TARGETS: Dict[str, Dict[str, Any]] = {
    "stage1": {
        "rel": Path("prompts") / "stage1_scene_bible.txt",
        "start": "<!-- STAGE1_LEARNINGS_START -->",
        "end": "<!-- STAGE1_LEARNINGS_END -->",
        "header": (
            "\n================================================================\n"
            "GUI LEARNINGS (appended from Streamlit edit log)\n"
            "================================================================\n"
        ),
    },
    "stage2": {
        "rel": Path("prompts") / "stage2_shot_planner.txt",
        "start": "<!-- STAGE2_LEARNINGS_START -->",
        "end": "<!-- STAGE2_LEARNINGS_END -->",
        "header": (
            "\n================================================================\n"
            "GUI LEARNINGS (appended from Streamlit edit log)\n"
            "================================================================\n"
        ),
    },
    "verifier": {
        "rel": Path("prompts") / "verifier_clip.txt",
        "start": "<!-- VERIFIER_LEARNINGS_START -->",
        "end": "<!-- VERIFIER_LEARNINGS_END -->",
        "header": (
            "\n================================================================\n"
            "GUI LEARNINGS (appended from Streamlit edit log)\n"
            "================================================================\n"
        ),
    },
    "shared": {
        "rel": Path("prompts") / "shared_rules.txt",
        "start": "<!-- SHARED_LEARNINGS_START -->",
        "end": "<!-- SHARED_LEARNINGS_END -->",
        "header": (
            "\n================================================================\n"
            "GUI LEARNINGS (appended from Streamlit edit log)\n"
            "================================================================\n"
        ),
    },
}

# Operator checklist shown when dirty (Phase A — manual replan)
CASCADE_CHECKLIST = {
    "stage2": [
        "Update Stage 2 plan for this scene (LLM + prompts/stage2_shot_planner.txt, or edit blueprint clips).",
        "Optional: python scripts/two_stage_adaptation/stage2_plan_grok.py --scenes N --merge-into …",
        "Regenerate affected clips (Scenes UI or python -m cli).",
        "Clear dirty flag when plan + renders look good.",
    ],
    "stage1": [
        "Update Stage 1 bible for this scene (LLM + prompts/stage1_scene_bible.txt, or edit scenes JSON).",
        "Re-run Stage 2 for this scene (stage2_plan_grok.py --scenes N --merge-into …).",
        "Regenerate affected clips.",
        "Clear dirty flag when bible + plan + renders check out.",
    ],
}


def _now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def normalize_layer(layer: Optional[str]) -> str:
    layer = (layer or "clip").strip().lower()
    if layer not in FEEDBACK_LAYERS:
        return "clip"
    return layer


def targets_for_layer(layer: str) -> List[str]:
    return list(LAYER_TARGETS.get(normalize_layer(layer), LAYER_TARGETS["clip"]))


def dirty_keys_for_layer(layer: str) -> tuple:
    return LAYER_DIRTY.get(normalize_layer(layer), ())


def prompt_path(key: str) -> Path:
    meta = PROMPT_LEARNING_TARGETS[key]
    return workspace_root() / meta["rel"]


def append_prompt_learning(key: str, entry: Dict[str, Any]) -> str:
    """Append a bullet into a prompt file's GUI learnings markers."""
    if key not in PROMPT_LEARNING_TARGETS:
        raise KeyError(f"Unknown prompt learning key: {key}")
    meta = PROMPT_LEARNING_TARGETS[key]
    path = workspace_root() / meta["rel"]
    rule = (entry.get("suggested_rule") or entry.get("user_note") or "").strip()
    if not rule:
        raise ValueError("No rule text to append")
    if not path.is_file():
        raise FileNotFoundError(str(path))

    text = path.read_text(encoding="utf-8")
    bullet = (
        f"- [{entry.get('id')} {entry.get('ts')}] "
        f"layer={entry.get('learning_layer') or '?'} "
        f"S{entry.get('scene')}C{entry.get('clip')}: {rule}\n"
    )
    start, end = meta["start"], meta["end"]
    if start in text and end in text:
        pre, rest = text.split(start, 1)
        mid, post = rest.split(end, 1)
        mid = mid.rstrip() + "\n" + bullet
        new_text = pre + start + mid + "\n" + end + post
    else:
        new_text = text.rstrip() + "\n" + meta["header"] + start + "\n" + bullet + end + "\n"

    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(new_text, encoding="utf-8")
    import os

    os.replace(tmp, path)
    return str(path)


def apply_entry_to_layer_prompts(entry: Dict[str, Any]) -> List[str]:
    """
    Apply suggested rule to the prompt files implied by learning_layer.
    Returns list of paths written.
    """
    layer = normalize_layer(entry.get("learning_layer"))
    written: List[str] = []
    if layer == "stage1":
        written.append(append_prompt_learning("stage1", entry))
    elif layer == "stage2":
        written.append(append_prompt_learning("stage2", entry))
    elif layer == "verifier":
        written.append(append_prompt_learning("verifier", entry))
        # Durable detection patterns often belong in shared rules too if user wants —
        # Phase A: only verifier file unless they also hit "shared".
    elif layer == "clip":
        # Clip-only: no pack prompt by default
        pass
    elif layer == "engine":
        pass
    return written


# ---------- Dirty flags (pipeline_state via engine) ----------


def get_scene_dirty_map(state: Dict[str, Any]) -> Dict[str, Any]:
    raw = state.get("scene_dirty")
    return raw if isinstance(raw, dict) else {}


def get_scene_dirty(state: Dict[str, Any], scene_num: int) -> Optional[Dict[str, Any]]:
    return get_scene_dirty_map(state).get(str(int(scene_num)))


def mark_scene_dirty(
    state: Dict[str, Any],
    scene_num: int,
    *,
    keys: Sequence[str],
    reason: str = "",
    entry_id: Optional[str] = None,
    learning_layer: Optional[str] = None,
) -> Dict[str, Any]:
    """
    Mark scene dirty for stage1 and/or stage2 replan.
    Mutates state['scene_dirty']; caller must save_state().
    """
    dirty = state.setdefault("scene_dirty", {})
    if not isinstance(dirty, dict):
        dirty = {}
        state["scene_dirty"] = dirty
    key = str(int(scene_num))
    row = dirty.get(key) if isinstance(dirty.get(key), dict) else {}
    row = dict(row)
    notes = list(row.get("notes") or [])
    entry_ids = list(row.get("entry_ids") or [])
    for k in keys:
        if k in ("stage1", "stage2"):
            row[k] = True
    if reason:
        notes.append(f"{_now()}: {reason}")
        notes = notes[-20:]
        row["reason"] = reason
    if entry_id:
        if entry_id not in entry_ids:
            entry_ids.append(entry_id)
        entry_ids = entry_ids[-30:]
    if learning_layer:
        row["last_layer"] = normalize_layer(learning_layer)
    row["notes"] = notes
    row["entry_ids"] = entry_ids
    row["updated_at"] = _now()
    row["stage1"] = bool(row.get("stage1"))
    row["stage2"] = bool(row.get("stage2"))
    dirty[key] = row
    # Un-approve scene so pipeline doesn't treat it as final
    completed = state.setdefault("scenes_completed", {})
    if isinstance(completed, dict):
        completed[str(int(scene_num))] = False
    return row


def clear_scene_dirty(
    state: Dict[str, Any],
    scene_num: int,
    *,
    keys: Optional[Sequence[str]] = None,
) -> None:
    dirty = state.setdefault("scene_dirty", {})
    if not isinstance(dirty, dict):
        return
    key = str(int(scene_num))
    row = dirty.get(key)
    if not isinstance(row, dict):
        return
    if keys is None:
        dirty.pop(key, None)
        return
    for k in keys:
        row[k] = False
    if not row.get("stage1") and not row.get("stage2"):
        dirty.pop(key, None)
    else:
        row["updated_at"] = _now()
        dirty[key] = row


def list_dirty_scenes(state: Dict[str, Any]) -> List[Dict[str, Any]]:
    out: List[Dict[str, Any]] = []
    for sk, row in sorted(get_scene_dirty_map(state).items(), key=lambda x: int(x[0]) if str(x[0]).isdigit() else 0):
        if not isinstance(row, dict):
            continue
        s1, s2 = bool(row.get("stage1")), bool(row.get("stage2"))
        if not s1 and not s2:
            continue
        out.append(
            {
                "scene": int(sk) if str(sk).isdigit() else sk,
                "stage1": s1,
                "stage2": s2,
                "reason": row.get("reason") or "",
                "updated_at": row.get("updated_at"),
                "last_layer": row.get("last_layer"),
                "entry_ids": list(row.get("entry_ids") or []),
                "cascade": "stage1→stage2" if s1 else "stage2",
                "checklist": (
                    CASCADE_CHECKLIST["stage1"] if s1 else CASCADE_CHECKLIST["stage2"]
                ),
            }
        )
    return out


def dirty_summary(state: Dict[str, Any]) -> Dict[str, Any]:
    rows = list_dirty_scenes(state)
    return {
        "dirty_count": len(rows),
        "need_stage1": sum(1 for r in rows if r.get("stage1")),
        "need_stage2": sum(1 for r in rows if r.get("stage2")),
        "scenes": rows,
    }


def suggest_layer_from_note(note: str) -> str:
    """Cheap heuristic default for UI (user can override)."""
    low = (note or "").lower()
    if any(w in low for w in ("remux", "wip", "stale flag", "crash", "ffmpeg", "pipeline bug")):
        return "engine"
    if any(w in low for w in ("missed fail", "should have caught", "qa missed", "verifier")):
        return "verifier"
    if any(
        w in low
        for w in (
            "spoiler",
            "name tag",
            "name badge",
            "revealed too early",
            "withheld name",
            "said the name",
            "showed the name",
        )
    ):
        # Early name/plot leak: often Stage 2 prompt; bible timing → stage1 if constraint missing
        return "stage2"
    if any(w in low for w in ("wrong event", "missing beat", "book says", "not in story", "bible", "reveal scene")):
        return "stage1"
    if any(
        w in low
        for w in (
            "extend",
            "continuation",
            "framing",
            "shot",
            "duration",
            "cut too early",
            "replan",
            "facing",
            "eyeline",
            "ots",
        )
    ):
        return "stage2"
    return "clip"
