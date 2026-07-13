"""
Navigation lock while clip generation is running.

Streamlit's st.navigation page links cannot be disabled individually, so the
app (1) shrinks the nav to Scenes-only while gen runs, and (2) pages call
block_if_gen_running() as a backstop if somehow reached.
"""
from __future__ import annotations

from typing import Any, Dict


def gen_is_running() -> bool:
    """True when a background clip gen job is active."""
    try:
        from review_app import pipeline_api as api

        return bool(api.gen_job_running())
    except Exception:
        return False


def gen_status() -> Dict[str, Any]:
    try:
        from review_app import pipeline_api as api

        return dict(api.gen_job_status() or {})
    except Exception:
        return {"status": "idle"}


def block_if_gen_running(*, page_label: str = "This page") -> None:
    """
    Stop page render when generation is running (except Scenes, which should
    not call this). Shows Cancel + link back to Scenes.
    """
    if not gen_is_running():
        return

    import streamlit as st

    job = gen_status()
    msg = job.get("message") or "Generating clips…"
    kind = job.get("kind") or "gen"
    sn, cn = job.get("scene"), job.get("clip")
    where = ""
    try:
        if sn is not None and cn is not None:
            where = f" S{int(sn):02d}C{int(cn)}"
        elif sn is not None:
            where = f" scene {sn}"
    except (TypeError, ValueError):
        where = ""

    st.error(
        f"🔒 **Menu locked while generation is running** ({kind}{where}).  \n"
        f"{msg}  \n"
        f"**{page_label}** is unavailable until the job finishes or you cancel."
    )
    c1, c2, c3 = st.columns(3)
    with c1:
        if st.button("Cancel generation", type="primary", key=f"gen_lock_cancel_{page_label}"):
            try:
                from review_app import pipeline_api as api

                api.cancel_gen_job()
                st.info("Cancel requested…")
                st.rerun()
            except Exception as e:
                st.error(str(e))
    with c2:
        if st.button("Open Scenes", key=f"gen_lock_scenes_{page_label}"):
            try:
                st.switch_page("pages/4_Scenes.py")
            except Exception:
                st.warning("Use the **Scenes** item in the sidebar.")
    with c3:
        if st.button("Refresh", key=f"gen_lock_refresh_{page_label}"):
            st.rerun()
    st.stop()
