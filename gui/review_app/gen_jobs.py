"""
Background clip generation jobs (scene / batch / single clip).

Status is mirrored to pipeline_state["gen_job"] so Streamlit can poll without
blocking navigation. Cancel is cooperative: between clips (and via engine
shutdown flag mid-poll when possible).
"""
from __future__ import annotations

import threading
import time
import traceback
from typing import Any, Callable, Dict, List, Optional

_lock = threading.RLock()
_cancel = threading.Event()
_thread: Optional[threading.Thread] = None
_generation = 0
_status: Dict[str, Any] = {
    "status": "idle",  # idle | running | done | error | cancelled
    "kind": None,  # scene | batch | clip
    "message": "",
    "generation": 0,
    "scene": None,
    "clip": None,
    "scenes": [],
    "index": 0,
    "total": 0,
    "done": [],
    "failed": [],
    "log": [],
    "started_at": None,
    "finished_at": None,
    "error": None,
    "summary": None,
}


def get_gen_job_status() -> Dict[str, Any]:
    with _lock:
        return dict(_status)


def is_gen_running() -> bool:
    """True if a gen worker is active (status or live thread)."""
    with _lock:
        if _status.get("status") == "running":
            return True
        if _thread is not None and _thread.is_alive():
            return True
        return False


def _set_status(**kwargs: Any) -> None:
    with _lock:
        _status.update(kwargs)
        # Keep log bounded
        log = _status.get("log")
        if isinstance(log, list) and len(log) > 80:
            _status["log"] = log[-80:]


def _append_log(msg: str) -> None:
    with _lock:
        log = list(_status.get("log") or [])
        log.append(str(msg))
        _status["log"] = log[-80:]
        _status["message"] = str(msg)


def _persist_status() -> None:
    try:
        from review_app import pipeline_api as api

        eng = api.get_engine()
        with _lock:
            snap = dict(_status)
            # Don't persist huge summary blobs
            if isinstance(snap.get("summary"), dict):
                s = dict(snap["summary"])
                s.pop("per_scene", None)
                snap["summary"] = s
        eng.state["gen_job"] = snap
        eng.save_state()
    except Exception:
        pass


def cancel_gen_job() -> Dict[str, Any]:
    """Request cooperative cancel. Current Grok clip may finish polling first."""
    _cancel.set()
    try:
        from review_app import pipeline_api as api

        eng = api.get_engine()
        # Best-effort mid-poll abort for Grok wait loops
        if hasattr(eng, "_shutdown_requested"):
            eng._shutdown_requested = True
    except Exception:
        pass
    _append_log("Cancel requested…")
    _set_status(message="Cancel requested — finishing current step if possible…")
    _persist_status()
    return get_gen_job_status()


def _cancel_check() -> bool:
    return _cancel.is_set()


def _make_progress_cb() -> Callable[[dict], None]:
    def _cb(ev: dict) -> None:
        if not isinstance(ev, dict):
            return
        msg = ev.get("message") or ev.get("event") or ""
        updates: Dict[str, Any] = {}
        if msg:
            updates["message"] = msg
            _append_log(msg)
        if ev.get("scene") is not None:
            updates["scene"] = ev.get("scene")
        if ev.get("clip") is not None:
            updates["clip"] = ev.get("clip")
        if ev.get("index") is not None:
            updates["index"] = ev.get("index")
        if ev.get("total") is not None:
            updates["total"] = ev.get("total")
        if updates:
            _set_status(**updates)
            # Light persist on clip boundaries only (avoid thrashing disk)
            if ev.get("event") in (
                "start",
                "clip_start",
                "clip_done",
                "clip_error",
                "scene_start",
                "done",
                "batch_done",
                "batch_stopped",
                "cancelled",
            ):
                _persist_status()

    return _cb


def _worker(kind: str, kwargs: Dict[str, Any], gen: int) -> None:
    from review_app import pipeline_api as api

    eng = api.get_engine()
    # Clear any prior shutdown flag from an earlier cancel
    if hasattr(eng, "_shutdown_requested"):
        eng._shutdown_requested = False

    try:
        progress_cb = _make_progress_cb()
        summary: Any = None

        if kind == "scene":
            summary = api.generate_scene_clips(
                int(kwargs["scene_num"]),
                only_missing=bool(kwargs.get("only_missing", True)),
                run_qa=bool(kwargs.get("run_qa", True)),
                remux=bool(kwargs.get("remux", True)),
                rebuild_wip=False,
                progress_cb=progress_cb,
                cancel_check=_cancel_check,
                clip_numbers=kwargs.get("clip_numbers"),
            )
        elif kind == "clips":
            # Explicit multi-select regen within one scene
            summary = api.generate_scene_clips(
                int(kwargs["scene_num"]),
                only_missing=False,
                run_qa=bool(kwargs.get("run_qa", True)),
                remux=bool(kwargs.get("remux", True)),
                rebuild_wip=False,
                progress_cb=progress_cb,
                cancel_check=_cancel_check,
                clip_numbers=list(kwargs.get("clip_numbers") or []),
            )
        elif kind == "batch":
            summary = api.generate_scenes_clips(
                list(kwargs.get("scene_nums") or []),
                only_missing=bool(kwargs.get("only_missing", True)),
                run_qa=bool(kwargs.get("run_qa", True)),
                remux=bool(kwargs.get("remux", True)),
                rebuild_wip=False,
                stop_on_fail=bool(kwargs.get("stop_on_fail", True)),
                progress_cb=progress_cb,
                cancel_check=_cancel_check,
            )
        elif kind == "clip":
            sn = int(kwargs["scene_num"])
            cn = int(kwargs["clip_num"])
            _set_status(
                message=f"Generating S{sn:02d} C{cn}…",
                scene=sn,
                clip=cn,
                index=1,
                total=1,
            )
            _persist_status()
            if _cancel_check():
                raise RuntimeError("cancelled")
            path = api.regen_clip(
                sn,
                cn,
                feedback=str(kwargs.get("feedback") or ""),
                apply_to_prompt=bool(kwargs.get("apply_to_prompt", True)),
                run_qa=bool(kwargs.get("run_qa", True)),
                rebuild_wip=False,
                mark_dirty=bool(kwargs.get("mark_dirty", False)),
            )
            summary = {
                "scene": sn,
                "clip": cn,
                "path": path,
                "done": [cn],
                "failed": [],
            }
            progress_cb(
                {
                    "event": "done",
                    "scene": sn,
                    "clip": cn,
                    "index": 1,
                    "total": 1,
                    "message": f"Done S{sn:02d} C{cn}",
                    "path": path,
                }
            )
        else:
            raise ValueError(f"Unknown gen job kind: {kind}")

        if _cancel.is_set():
            _set_status(
                status="cancelled",
                message="Cancelled by user",
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                summary=summary if isinstance(summary, dict) else None,
                error=None,
            )
            _append_log("Cancelled")
        else:
            done_n = 0
            fail_n = 0
            first_err = None
            if isinstance(summary, dict):
                done_n = len(summary.get("done") or []) or int(
                    summary.get("clips_done") or 0
                )
                failed = summary.get("failed") or []
                fail_n = len(failed) if isinstance(failed, list) else 0
                if failed and isinstance(failed[0], dict):
                    first_err = str(failed[0].get("error") or failed[0])[:400]
                elif failed:
                    first_err = str(failed[0])[:400]
            if fail_n and not done_n:
                msg = f"Generation failed ({fail_n} clip(s))"
                if first_err:
                    msg = f"{msg}: {first_err}"
                _set_status(
                    status="error",
                    message=msg,
                    finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                    summary=summary if isinstance(summary, dict) else None,
                    error=first_err or msg,
                )
                _append_log(msg)
            elif fail_n:
                msg = f"Generation finished with errors ({done_n} ok, {fail_n} failed)"
                if first_err:
                    msg = f"{msg}. First error: {first_err}"
                _set_status(
                    status="done",
                    message=msg,
                    finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                    summary=summary if isinstance(summary, dict) else None,
                    error=first_err,
                )
                _append_log(msg)
            else:
                _set_status(
                    status="done",
                    message=f"Generation finished ({done_n} clip(s))",
                    finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                    summary=summary if isinstance(summary, dict) else None,
                    error=None,
                )
                _append_log("Finished")
    except Exception as e:
        if _cancel.is_set() or "cancel" in str(e).lower():
            _set_status(
                status="cancelled",
                message="Cancelled by user",
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                error=None,
            )
            _append_log(f"Cancelled ({e})")
        else:
            _set_status(
                status="error",
                message=str(e),
                finished_at=time.strftime("%Y-%m-%dT%H:%M:%S"),
                error=str(e),
            )
            _append_log(f"Error: {e}")
            _append_log(traceback.format_exc()[-500:])
    finally:
        try:
            if hasattr(eng, "_shutdown_requested"):
                eng._shutdown_requested = False
        except Exception:
            pass
        _persist_status()
        with _lock:
            global _thread
            _thread = None


def _start(kind: str, **kwargs: Any) -> Dict[str, Any]:
    global _thread, _generation
    with _lock:
        if _status.get("status") == "running" and _thread is not None and _thread.is_alive():
            raise RuntimeError(
                "A generation job is already running. Cancel it first or wait for it to finish."
            )
        _cancel.clear()
        _generation += 1
        gen = _generation
        scenes = list(kwargs.get("scene_nums") or [])
        scene = kwargs.get("scene_num")
        clip = kwargs.get("clip_num")
        _status.clear()
        _status.update(
            {
                "status": "running",
                "kind": kind,
                "message": "Starting generation…",
                "generation": gen,
                "scene": scene,
                "clip": clip,
                "scenes": scenes,
                "index": 0,
                "total": 0,
                "done": [],
                "failed": [],
                "log": ["Starting…"],
                "started_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
                "finished_at": None,
                "error": None,
                "summary": None,
            }
        )
        t = threading.Thread(
            target=_worker,
            args=(kind, dict(kwargs), gen),
            name=f"clip-gen-{kind}-{gen}",
            daemon=True,
        )
        _thread = t
        t.start()
    _persist_status()
    return get_gen_job_status()


def start_scene_gen(
    scene_num: int,
    *,
    only_missing: bool = True,
    run_qa: bool = True,
    remux: bool = True,
) -> Dict[str, Any]:
    return _start(
        "scene",
        scene_num=int(scene_num),
        only_missing=only_missing,
        run_qa=run_qa,
        remux=remux,
    )


def start_batch_gen(
    scene_nums: List[int],
    *,
    only_missing: bool = True,
    run_qa: bool = True,
    remux: bool = True,
    stop_on_fail: bool = True,
) -> Dict[str, Any]:
    return _start(
        "batch",
        scene_nums=[int(n) for n in scene_nums],
        only_missing=only_missing,
        run_qa=run_qa,
        remux=remux,
        stop_on_fail=stop_on_fail,
    )


def start_clip_gen(
    scene_num: int,
    clip_num: int,
    *,
    feedback: str = "",
    apply_to_prompt: bool = True,
    run_qa: bool = True,
) -> Dict[str, Any]:
    return _start(
        "clip",
        scene_num=int(scene_num),
        clip_num=int(clip_num),
        feedback=feedback or "",
        apply_to_prompt=apply_to_prompt,
        run_qa=run_qa,
        mark_dirty=False,
    )


def start_clips_gen(
    scene_num: int,
    clip_numbers: List[int],
    *,
    run_qa: bool = True,
    remux: bool = True,
) -> Dict[str, Any]:
    """Regen an explicit list of clips in one scene (multi-select)."""
    nums = sorted({int(c) for c in (clip_numbers or [])})
    if not nums:
        raise ValueError("No clips selected.")
    return _start(
        "clips",
        scene_num=int(scene_num),
        clip_numbers=nums,
        run_qa=run_qa,
        remux=remux,
    )


def render_gen_job_banner(
    *,
    compact: bool = False,
    auto_refresh: bool = False,
    key_prefix: str = "gen_banner",
) -> Dict[str, Any]:
    """
    Streamlit banner + Cancel for multipage scripts.

    When auto_refresh=True and a job is running, only this banner re-renders on a
    timer (st.fragment) — the rest of the page is NOT full-rerun, so controls do
    not flash enabled/disabled. When the job finishes, one full st.rerun() unlocks UI.
    """
    import streamlit as st
    from datetime import timedelta

    def _fetch() -> Dict[str, Any]:
        try:
            from review_app import pipeline_api as api

            return api.gen_job_status()
        except Exception:
            return {"status": "idle"}

    def _paint(job: Dict[str, Any], *, live_running: bool) -> None:
        st_status = str(job.get("status") or "idle")
        if live_running or st_status == "running":
            kind = job.get("kind") or "gen"
            msg = job.get("message") or "Generating…"
            sn, cn = job.get("scene"), job.get("clip")
            where = ""
            if sn is not None and cn is not None:
                try:
                    where = f" S{int(sn):02d}C{int(cn)}"
                except (TypeError, ValueError):
                    where = f" S{sn}C{cn}"
            elif sn is not None:
                where = f" scene {sn}"
            idx, total = job.get("index"), job.get("total")
            prog = ""
            try:
                if idx and total:
                    prog = f" · {int(idx)}/{int(total)}"
            except (TypeError, ValueError):
                pass
            st.warning(
                f"**Generation running** ({kind}{where}{prog}). {msg}  \n"
                "Sidebar **menu pages** and project switch are locked. "
                "**Cancel** stops after the current clip when possible."
            )
            b1, b2 = st.columns([1, 3])
            with b1:
                if st.button("Cancel", type="primary", key=f"{key_prefix}_cancel"):
                    try:
                        from review_app import pipeline_api as api

                        api.cancel_gen_job()
                        st.info("Cancel requested…")
                        st.rerun()
                    except Exception as e:
                        st.error(str(e))
            with b2:
                if st.button("Refresh page", key=f"{key_prefix}_refresh"):
                    st.rerun()
            if not compact:
                log = job.get("log") or []
                if log:
                    with st.expander("Generation log", expanded=True):
                        st.code("\n".join(str(x) for x in log[-40:]), language="text")
            return
        if st_status == "done":
            st.success(job.get("message") or "Generation finished.")
        elif st_status == "cancelled":
            st.info(job.get("message") or "Generation cancelled.")
        elif st_status == "error":
            st.error(job.get("error") or job.get("message") or "Generation error")

    job = _fetch()
    running = bool(is_gen_running() or str(job.get("status") or "") == "running")

    # One full-page unlock when job ends (buttons re-enable). Do this only once.
    flag = f"_{key_prefix}_was_running"
    prev = bool(st.session_state.get(flag))
    if prev and not running:
        st.session_state[flag] = False
        st.rerun()
    st.session_state[flag] = running

    if auto_refresh and running and hasattr(st, "fragment"):
        # Fragment-only polling — rest of page stays put (no enable/disable flash)
        @st.fragment(run_every=timedelta(seconds=2))
        def _live_banner() -> None:
            j = get_gen_job_status()
            still = is_gen_running() or str(j.get("status") or "") == "running"
            if still:
                j = dict(j)
                j["status"] = "running"
            _paint(j, live_running=still)
            if not still:
                st.session_state[flag] = False
                st.rerun()  # unlock disabled controls on parent page

        _live_banner()
    else:
        _paint(job, live_running=running)

    if running:
        live = get_gen_job_status()
        out = dict(live)
        out["status"] = "running"
        return out
    return job if isinstance(job, dict) else {"status": "idle"}
