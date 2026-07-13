"""
Film Review Console (multi-project)

Run from workspace root:
  streamlit run gui/streamlit_app.py
"""
from __future__ import annotations

import sys
from pathlib import Path

_GUI_DIR = Path(__file__).resolve().parent
_WORKSPACE = _GUI_DIR.parent
for _p in (_WORKSPACE, _GUI_DIR):
    _s = str(_p)
    if _s not in sys.path:
        sys.path.insert(0, _s)

import streamlit as st

from review_app import pipeline_api as api
from review_app.paths import project_label
from review_app.pipeline_progress import pipeline_progress, render_sidebar_progress

# PDF book embeds can exceed PIL's default ~89MP limit and crash the process
# (DecompressionBomb → native segfault under WSL /mnt/c). Raise limit early;
# UI always displays via thumbnails (see review_app.thumbnails).
try:
    from PIL import Image as _PIL_Image

    _PIL_Image.MAX_IMAGE_PIXELS = max(
        getattr(_PIL_Image, "MAX_IMAGE_PIXELS", None) or 0,
        200_000_000,
    )
except Exception:
    pass

st.set_page_config(
    page_title="Film Review Console",
    page_icon="🎬",
    layout="wide",
    initial_sidebar_state="expanded",
)

_PAGES = _GUI_DIR / "pages"


def _gen_job_banner(*, compact: bool = False, auto_refresh: bool = False) -> dict:
    """
    Show generation lock UI. Returns status dict.
    When running: warn + Cancel; project switch locked in sidebar.
    """
    try:
        job = api.gen_job_status()
    except Exception:
        job = {"status": "idle"}
    st_status = str(job.get("status") or "idle")
    if st_status == "running":
        kind = job.get("kind") or "gen"
        msg = job.get("message") or "Generating…"
        sn = job.get("scene")
        cn = job.get("clip")
        where = ""
        if sn is not None and cn is not None:
            where = f" S{int(sn):02d}C{int(cn)}"
        elif sn is not None:
            where = f" scene {sn}"
        st.warning(
            f"**Generation running** ({kind}{where}). "
            f"{msg}  \n"
            "Project switch is locked. Use **Cancel** to stop after the current clip when possible."
        )
        c1, c2 = st.columns([1, 3])
        with c1:
            if st.button("Cancel", type="primary", key="gen_job_cancel_banner"):
                try:
                    api.cancel_gen_job()
                    st.info("Cancel requested…")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
        with c2:
            if st.button("Refresh status", key="gen_job_refresh_banner"):
                st.rerun()
        if not compact:
            log = job.get("log") or []
            if log:
                with st.expander("Generation log", expanded=True):
                    st.code("\n".join(str(x) for x in log[-40:]), language="text")
        # Do not full-page sleep+rerun here (causes control flash). Scenes uses
        # fragment auto_refresh via gen_jobs.render_gen_job_banner instead.
    elif st_status == "done":
        st.success(job.get("message") or "Generation finished.")
    elif st_status == "cancelled":
        st.info(job.get("message") or "Generation cancelled.")
    elif st_status == "error":
        st.error(job.get("error") or job.get("message") or "Generation error")
    return job


def _render_project_sidebar(prog: dict | None = None) -> dict:
    """Project switcher + pipeline checklist. Returns progress dict (computed once)."""
    projects = api.list_all_projects()
    proj_ids = [p["id"] for p in projects]
    active = api.active_project_info()
    active_id = active.get("id")
    labels = {p["id"]: p.get("label") or project_label(p) for p in projects}

    def fmt(pid: str) -> str:
        return labels.get(pid) or project_label(pid, projects=projects)

    if prog is None:
        try:
            prog = pipeline_progress()
        except Exception:
            prog = {"labels": {}, "steps": [], "next_id": None}

    gen_running = False
    try:
        gen_running = bool(api.gen_job_running())
    except Exception:
        gen_running = False

    with st.sidebar:
        st.header("Project")
        if gen_running:
            st.error("🔒 Gen running — project switch locked")
            try:
                job = api.gen_job_status()
                st.caption(job.get("message") or "Generating clips…")
            except Exception:
                pass
            if st.button("Cancel generation", key="sidebar_gen_cancel", type="primary"):
                try:
                    api.cancel_gen_job()
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
            if st.button("Refresh", key="sidebar_gen_refresh"):
                st.rerun()

        if proj_ids:
            try:
                idx = proj_ids.index(active_id) if active_id in proj_ids else 0
            except ValueError:
                idx = 0
            choice = st.selectbox(
                "Active project",
                options=proj_ids,
                index=idx,
                format_func=fmt,
                disabled=gen_running,
                help="Locked while clip generation is running" if gen_running else None,
            )
            if not gen_running and choice != active_id:
                api.switch_project(choice)
                # Drop progress cache on project switch
                from review_app.pipeline_progress import invalidate_progress_cache

                invalidate_progress_cache()
                st.rerun()
            if active_id and fmt(active_id) != active_id:
                st.caption(f"Folder: `{active_id}`")
        else:
            st.warning("No projects yet.")

        if active_id or proj_ids:
            try:
                render_sidebar_progress(prog)
            except Exception:
                pass

        with st.expander("➕ Create project", expanded=not proj_ids):
            new_name = st.text_input(
                "Project id / folder name",
                placeholder="MyFilm",
                disabled=gen_running,
            )
            new_title = st.text_input(
                "Title (optional)", placeholder="My Film", disabled=gen_running
            )
            if st.button("Create", type="primary", disabled=gen_running):
                if not (new_name or "").strip():
                    st.error("Name required")
                else:
                    try:
                        info = api.new_project(
                            new_name.strip(),
                            title=(new_title or "").strip() or None,
                        )
                        st.success(f"Created and activated `{info.get('id')}`")
                        st.rerun()
                    except Exception as e:
                        st.error(str(e))
    return prog


def page_home() -> None:
    st.title("🎬 Film Review Console")
    st.caption(
        "Multi-project review UI. Each project lives under `projects/<id>/` with its own "
        "blueprint, config, state, and assets."
    )
    _gen_job_banner(compact=True)

    projects = api.list_all_projects()
    active = api.active_project_info()
    active_id = active.get("id")
    if not active_id and not projects:
        st.info("Create a project in the sidebar to begin.")
        return

    try:
        dash = api.home_dashboard()
        prog = pipeline_progress()
    except Exception as e:
        st.error(str(e))
        return

    proj = dash.get("project") or {}
    display_title = (
        dash.get("title")
        or project_label(proj)
        or proj.get("id")
        or "Untitled"
    )
    st.subheader(display_title)

    actual_usd = float(dash.get("actual_usd") or 0)
    m1, m2, m3, m4 = st.columns(4)
    m1.metric("Actual spend", f"${actual_usd:,.2f}")
    m2.metric(
        "Clips",
        f"{dash.get('clips_on_disk', 0)}/{dash.get('clips_total', 0)}",
    )
    m3.metric(
        "Scenes approved",
        f"{dash.get('approved', 0)}/{dash.get('scene_count', 0)}",
    )
    m4.metric(
        "Chars locked",
        f"{dash.get('chars_locked', 0)}/{dash.get('char_count', 0)}",
    )
    st.caption(
        f"Ledger events {dash.get('actual_events', 0)} · "
        f"Video jobs {dash.get('actual_video_jobs', 0)} · "
        f"{dash.get('actual_video_sec', 0):g}s video billed · "
        f"Hero {dash.get('hero_count', 0)} · "
        f"Stale {dash.get('stale_count', 0)} · "
        f"Dirty {dash.get('dirty_count', 0)} · "
        "Open **Cost** for breakdown / estimates."
    )

    # Next-step callout
    next_id = prog.get("next_id")
    if next_id == "adaptation":
        st.info("**Next:** **Adaptation** — import the book and run Stage 1.")
    elif next_id == "configuration":
        st.success(
            "**Adaptation complete.** Next: **Configuration** — review providers/duration "
            "and click **Save** (marks this step done in the sidebar)."
        )
    elif next_id == "characters":
        st.info(
            "**Configuration done.** Next: **Characters** — generate and lock references."
        )
    elif next_id == "scenes":
        st.info(
            "**Characters ready.** Next: **Scenes** — Stage 2 plan + generate/review clips."
        )
    elif prog.get("adapt_done"):
        st.caption("Core pipeline steps look complete — refine on Scenes / Cost as needed.")

    dirty_n = int(dash.get("dirty_count") or 0)
    if dirty_n:
        dirty_rows = dash.get("dirty_scenes") or []
        labels_d = [
            f"S{r.get('scene')}({r.get('cascade')})"
            for r in dirty_rows[:12]
        ]
        st.warning(
            f"**{dirty_n} scene(s) need replan** "
            f"(stage1: {dash.get('dirty_need_stage1', 0)}, "
            f"stage2: {dash.get('dirty_need_stage2', 0)}) — "
            f"{', '.join(labels_d)}"
            + ("…" if dirty_n > len(labels_d) else "")
            + ". Open **Scenes**, follow cascade checklist, then clear dirty."
        )

    stale_n = int(dash.get("stale_count") or 0)
    if stale_n:
        labels_s = dash.get("stale_labels") or []
        st.warning(
            f"**{stale_n} clip(s) out of date** after character redesigns — "
            f"{', '.join(str(x) for x in labels_s)}"
            + ("…" if stale_n > len(labels_s) else "")
            + ". Regenerate them (Scenes or character cascade)."
        )

    wip = dash.get("wip_path")
    if wip:
        meta_bits = []
        if dash.get("wip_updated_at"):
            meta_bits.append(str(dash["wip_updated_at"]))
        if dash.get("wip_scene_count") is not None:
            meta_bits.append(f"{dash['wip_scene_count']} scenes")
        st.success(
            f"WIP movie: `{wip}`"
            + (f" ({', '.join(meta_bits)})" if meta_bits else "")
        )
        st.video(wip)
    else:
        st.caption("No WIP movie yet — approve a scene after clips exist.")

    if st.button("Rebuild WIP from approved scenes"):
        try:
            path = api.rebuild_wip_movie(approved_only=True)
            st.success(f"Rebuilt: `{path}`")
            st.rerun()
        except Exception as e:
            st.error(str(e))


def main() -> None:
    # One lightweight progress pass (file reads only, session-cached) for
    # sidebar checklist + nav titles — avoids loading the film engine on every click.
    try:
        prog = pipeline_progress()
    except Exception:
        prog = {"labels": {}, "steps": [], "next_id": None}
    _render_project_sidebar(prog)
    labels = prog.get("labels") or {}

    def L(step_id: str, fallback: str) -> str:
        return labels.get(step_id) or fallback

    pages = [
        st.Page(page_home, title="Home", icon="🎬", default=True),
        st.Page(
            str(_PAGES / "1_Adaptation.py"),
            title=L("adaptation", "Adaptation"),
            icon="📖",
        ),
        st.Page(
            str(_PAGES / "2_Configuration.py"),
            title=L("configuration", "Configuration"),
            icon="⚙️",
        ),
        st.Page(
            str(_PAGES / "3_Characters.py"),
            title=L("characters", "Characters"),
            icon="👤",
        ),
        st.Page(
            str(_PAGES / "4_Scenes.py"),
            title=L("scenes", "Scenes"),
            icon="🎞️",
        ),
        st.Page(
            str(_PAGES / "5_Edit_Log.py"),
            title=L("edit_log", "Edit Log"),
            icon="📝",
        ),
        st.Page(
            str(_PAGES / "6_Cost.py"),
            title=L("cost", "Cost"),
            icon="💰",
        ),
    ]

    pg = st.navigation(pages, position="sidebar")
    pg.run()


if __name__ == "__main__":
    main()
else:
    # Streamlit runs the file as a script (not __main__)
    main()
