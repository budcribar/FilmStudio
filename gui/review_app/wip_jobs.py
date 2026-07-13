"""
Background WIP movie rebuild / append jobs.

Smart scheduling after scene approve:
  - If idle → start work (prefer append when safe, else full rebuild)
  - If a full rebuild is running and another scene is approved → cancel & restart full
  - If an append is running and the new scene can also append → queue it
  - If an append is running but the new scene cannot append → cancel & full rebuild

Status is written to pipeline_state["wip_job"] so Streamlit can poll without
blocking the approve click.
"""
from __future__ import annotations

import os
import threading
import time
import traceback
from typing import Any, Dict, List, Optional, Set

_lock = threading.RLock()
_cancel = threading.Event()
_thread: Optional[threading.Thread] = None
_generation = 0
_mode: str = "idle"  # idle | full | append
_queue: List[int] = []  # scene numbers waiting for append chain
_status: Dict[str, Any] = {
    "status": "idle",
    "mode": None,
    "message": "",
    "generation": 0,
    "queued": [],
    "current_scene": None,
    "started_at": None,
    "finished_at": None,
    "error": None,
    "path": None,
}


def get_wip_job_status() -> Dict[str, Any]:
    with _lock:
        return dict(_status)


def _set_status(**kwargs: Any) -> None:
    with _lock:
        _status.update(kwargs)


def _persist_status(eng: Any) -> None:
    """Mirror job status into pipeline_state for UI after page reloads."""
    try:
        with _lock:
            snap = dict(_status)
        eng.state["wip_job"] = snap
        eng.save_state()
    except Exception:
        pass


def _ordered_approved_composites(eng: Any) -> List[tuple]:
    """Return [(scene_num, path), ...] for approved scenes with composites."""
    out: List[tuple] = []
    completed = eng.state.get("scenes_completed") or {}
    for s in eng.blueprint.get("scenes", []):
        sn = int(s.get("scene_number") or 0)
        if not sn:
            continue
        if not (completed.get(str(sn)) is True or completed.get(sn) is True):
            continue
        path = eng._resolve_scene_composite_path(sn)
        if path:
            out.append((sn, path))
    return out


def _wip_scene_numbers(eng: Any) -> List[int]:
    meta = eng.state.get("wip_movie") or {}
    nums = meta.get("scene_numbers")
    if isinstance(nums, list) and nums:
        try:
            return [int(x) for x in nums]
        except (TypeError, ValueError):
            pass
    # Unknown composition — treat as empty so we do a full rebuild
    return []


def _wip_path(eng: Any) -> str:
    return str(eng.config.get("wip_movie_path") or "assets/movie_wip.mp4")


def _projected_wip_scenes(eng: Any) -> List[int]:
    """Scene numbers WIP will contain after finishing current append + queue."""
    prev = _wip_scene_numbers(eng)
    projected = list(prev)
    with _lock:
        cur = _status.get("current_scene")
        q = list(_queue)
    if cur is not None and int(cur) not in projected:
        projected.append(int(cur))
    for n in q:
        n = int(n)
        if n not in projected:
            projected.append(n)
    return projected


def _can_append(eng: Any, scene_num: int, *, use_projection: bool = True) -> bool:
    """
    Append is safe when scene_num is the next approved composite after the
    current WIP (and optional in-flight append queue).
    """
    from renderer import file_is_usable

    entries = _ordered_approved_composites(eng)
    if not entries:
        return False
    sns = [e[0] for e in entries]
    if scene_num not in sns:
        return False  # no composite yet — caller should remux first

    wip = _wip_path(eng)
    base = _projected_wip_scenes(eng) if use_projection else _wip_scene_numbers(eng)
    if not base:
        # No recorded composition: only append if WIP missing and this is first approved
        if not file_is_usable(wip, min_bytes=1024) and sns[0] == scene_num:
            return True
        return False

    if len(base) >= len(sns):
        return False
    if sns[: len(base)] != base:
        return False  # order/content drift
    return sns[len(base)] == scene_num


def _run_full(eng: Any, reason: str, gen: int) -> Optional[str]:
    _set_status(
        status="running",
        mode="full",
        message=f"Full WIP rebuild… ({reason})",
        generation=gen,
        current_scene=None,
        started_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
        finished_at=None,
        error=None,
    )
    _persist_status(eng)

    if _cancel.is_set():
        return None

    # Collect and record scene numbers on success
    entries = _ordered_approved_composites(eng)
    if not entries:
        # fallback: any composites
        for s in eng.blueprint.get("scenes", []):
            sn = int(s.get("scene_number") or 0)
            path = eng._resolve_scene_composite_path(sn)
            if path:
                entries.append((sn, path))
    if not entries:
        _set_status(status="idle", mode=None, message="No composites to stitch")
        _persist_status(eng)
        return None

    paths = [p for _, p in entries]
    sns = [n for n, _ in entries]
    out = _wip_path(eng)

    if _cancel.is_set():
        return None

    path = eng._concat_videos(
        paths, out, label="wip_movie", cancel_event=_cancel
    )
    eng.state["wip_movie"] = {
        "path": path,
        "scene_count": len(sns),
        "scene_numbers": sns,
        "approved_only": True,
        "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "reason": reason,
        "mode": "full",
    }
    eng.save_state()
    return path


def _run_append(eng: Any, scene_num: int, gen: int) -> Optional[str]:
    from renderer import file_is_usable

    _set_status(
        status="running",
        mode="append",
        message=f"Appending scene {scene_num} to WIP…",
        generation=gen,
        current_scene=scene_num,
        started_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
        finished_at=None,
        error=None,
    )
    _persist_status(eng)

    if _cancel.is_set():
        return None

    comp = eng._resolve_scene_composite_path(scene_num)
    if not comp:
        raise RuntimeError(f"No composite for scene {scene_num} — remux first")

    wip = _wip_path(eng)
    prev = _wip_scene_numbers(eng)

    if not file_is_usable(wip, min_bytes=1024) or not prev:
        # First scene: just copy / single-file concat
        path = eng._concat_videos(
            [comp], wip, label="wip_movie", cancel_event=_cancel
        )
        eng.state["wip_movie"] = {
            "path": path,
            "scene_count": 1,
            "scene_numbers": [scene_num],
            "approved_only": True,
            "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
            "reason": f"append first S{scene_num}",
            "mode": "append",
        }
        eng.save_state()
        return path

    # Concat existing WIP + new scene (stream copy when possible)
    path = eng._concat_videos(
        [wip, comp], wip, label="wip_movie_append", cancel_event=_cancel
    )
    new_nums = list(prev) + [scene_num]
    eng.state["wip_movie"] = {
        "path": path,
        "scene_count": len(new_nums),
        "scene_numbers": new_nums,
        "approved_only": True,
        "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "reason": f"append S{scene_num}",
        "mode": "append",
    }
    eng.save_state()
    return path


def _worker(project_dir: str, reason: str, gen: int, initial_mode: str, seed_queue: List[int]) -> None:
    global _thread, _mode, _queue
    try:
        # Build a fresh engine bound to project (thread-local)
        os.chdir(project_dir)
        from renderer import AgenticGenerationEngine

        eng = AgenticGenerationEngine(install_signals=False, project_dir=project_dir)

        path: Optional[str] = None
        if initial_mode == "full":
            path = _run_full(eng, reason, gen)
        else:
            # Drain append queue
            while True:
                if _cancel.is_set() or gen != _generation:
                    break
                with _lock:
                    if not _queue:
                        break
                    sn = _queue.pop(0)
                    _status["queued"] = list(_queue)
                try:
                    if _can_append(eng, sn):
                        path = _run_append(eng, sn, gen)
                    else:
                        # Fall back to full rebuild mid-queue
                        path = _run_full(eng, f"fallback after S{sn}", gen)
                        # After full rebuild, remaining queue may already be included
                        with _lock:
                            _queue.clear()
                            _status["queued"] = []
                        break
                except Exception as e:
                    if _cancel.is_set() or gen != _generation:
                        break
                    raise e
                _persist_status(eng)

        if _cancel.is_set() or gen != _generation:
            _set_status(
                status="cancelled",
                message="WIP job cancelled (superseded)",
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
            )
        else:
            _set_status(
                status="done",
                mode=None,
                message="WIP up to date",
                path=path,
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                current_scene=None,
                queued=[],
            )
        try:
            _persist_status(eng)
        except Exception:
            pass
    except Exception as e:
        if not _cancel.is_set():
            _set_status(
                status="error",
                message=str(e),
                error=str(e),
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
            )
            print(f"[WIP job] Error: {e}\n{traceback.format_exc()}", flush=True)
            try:
                os.chdir(project_dir)
                from renderer import AgenticGenerationEngine

                eng = AgenticGenerationEngine(
                    install_signals=False, project_dir=project_dir
                )
                _persist_status(eng)
            except Exception:
                pass
    finally:
        with _lock:
            if gen == _generation:
                _mode = "idle"
                _thread = None


def schedule_wip_update(
    eng: Any,
    *,
    scene_num: Optional[int] = None,
    reason: str = "approve",
    force_full: bool = False,
) -> Dict[str, Any]:
    """
    Schedule background WIP update after approving scene_num (or full rebuild if None).

    Returns a status snapshot for the UI.
    """
    global _thread, _generation, _mode, _queue

    project_dir = str(getattr(eng, "project_dir", os.getcwd()))

    with _lock:
        want_append = (
            (not force_full)
            and scene_num is not None
            and _can_append(eng, int(scene_num))
        )
        prev = _thread
        running = prev is not None and prev.is_alive()

        # --- Case A: append job running + new scene can append → queue only ---
        if running and _mode == "append" and want_append and scene_num is not None:
            sn = int(scene_num)
            if sn not in _queue:
                _queue.append(sn)
            _status["queued"] = list(_queue)
            _status["message"] = f"Append queued: {_queue}"
            try:
                _persist_status(eng)
            except Exception:
                pass
            return get_wip_job_status()

        # --- Case B: something running → cancel, then start new work ---
        if running:
            _cancel.set()
            prev_thread = prev
        else:
            prev_thread = None

        _generation += 1
        gen = _generation
        initial_mode = "append" if want_append else "full"
        _mode = initial_mode
        if initial_mode == "append" and scene_num is not None:
            seed = [int(scene_num)]
            _queue = list(seed)
        else:
            seed = []
            _queue = []

        _status.update(
            {
                "status": "running",
                "mode": initial_mode,
                "message": (
                    f"Background {initial_mode} WIP"
                    + (f" (S{scene_num})" if scene_num is not None else "")
                    + (" — cancelled previous job" if running else "")
                ),
                "generation": gen,
                "queued": list(seed),
                "current_scene": int(scene_num) if initial_mode == "append" and scene_num else None,
                "started_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
                "finished_at": None,
                "error": None,
                "path": None,
            }
        )

        def _start() -> None:
            if prev_thread is not None:
                prev_thread.join(timeout=3.0)
            _cancel.clear()
            with _lock:
                global _queue
                if initial_mode == "append":
                    # Keep any scenes queued while we were starting
                    merged: List[int] = []
                    for n in seed + list(_queue):
                        if n not in merged:
                            merged.append(n)
                    _queue = merged
                    _status["queued"] = list(_queue)
            _worker(project_dir, reason, gen, initial_mode, list(_queue) if initial_mode == "append" else [])

        t = threading.Thread(target=_start, name=f"wip-job-{gen}", daemon=True)
        _thread = t
        t.start()
        try:
            _persist_status(eng)
        except Exception:
            pass
        return get_wip_job_status()


def cancel_wip_job() -> Dict[str, Any]:
    global _generation
    with _lock:
        _cancel.set()
        _generation += 1
        _queue.clear()
        _mode = "idle"
        _status.update(
            {
                "status": "cancelled",
                "message": "Cancelled by user",
                "queued": [],
                "finished_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
            }
        )
        return dict(_status)
