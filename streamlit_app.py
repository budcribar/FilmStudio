"""
Nick and Me — Streamlit Review Console

Run from repo root:
  pip install -r requirements-review.txt
  streamlit run streamlit_app.py
"""
from __future__ import annotations

import os

import streamlit as st

from review_app import pipeline_api as api

st.set_page_config(
    page_title="Nick and Me Review",
    page_icon="🎬",
    layout="wide",
    initial_sidebar_state="expanded",
)

st.title("🎬 Nick and Me — Review Console")
st.caption(
    "Browse characters and scenes, pass/fail clips, regenerate with feedback, "
    "and log learnings for the blueprint, adaptation prompt, and pipeline."
)


def _home_cache_key() -> str:
    parts = []
    for p in (
        "nickandme.json",
        "pipeline_state.json",
        "pipeline_config.json",
        "assets/movie_wip.mp4",
    ):
        try:
            parts.append(f"{p}:{os.path.getmtime(p):.0f}")
        except OSError:
            parts.append(f"{p}:0")
    try:
        parts.append(f"video:{os.path.getmtime('assets/video'):.0f}")
    except OSError:
        parts.append("video:0")
    return "|".join(parts)


@st.cache_data(show_spinner=False, ttl=60)
def _cached_home(cache_key: str) -> dict:
    _ = cache_key
    return api.home_dashboard()


try:
    with st.spinner("Loading dashboard…"):
        dash = _cached_home(_home_cache_key())
except Exception as e:
    st.error(f"Failed to load pipeline: {e}")
    st.info(
        "Run from the repo root with `nickandme.json` and `pipeline_config.json` present."
    )
    st.stop()

title = dash.get("title") or "Nick and Me"
c1, c2, c3, c4 = st.columns(4)
c1.metric("Movie", title[:28] + ("…" if len(title) > 28 else ""))
c2.metric("Scenes", dash.get("scene_count", 0))
c3.metric("Characters", f"{dash.get('chars_locked', 0)}/{dash.get('char_count', 0)} locked")
c4.metric(
    "Scenes approved",
    f"{dash.get('approved', 0)}/{dash.get('scene_count', 0)}",
)

st.divider()
st.subheader("Quick status")

done_clips = int(dash.get("clips_on_disk") or 0)
total_clips = int(dash.get("clips_total") or 0)
st.progress(
    done_clips / total_clips if total_clips else 0,
    text=f"Clips on disk: {done_clips} / {total_clips}",
)
st.caption(
    f"Hero scenes: **{dash.get('hero_count', 0)}** · "
    f"WIP scenes in last build: **{dash.get('wip_scene_count') or '—'}** · "
    f"WIP updated: `{dash.get('wip_updated_at') or '—'}`"
)

stale_n = int(dash.get("stale_count") or 0)
if stale_n:
    labels = dash.get("stale_labels") or []
    st.warning(
        f"**{stale_n} clip(s) out of date** after character redesigns — "
        f"{', '.join(labels)}"
        + ("…" if stale_n > 15 else "")
        + ". Regenerate them (Scenes or character cascade)."
    )
else:
    st.caption("No stale clips (on-disk renders match current character revisions).")

wip = dash.get("wip_path")
if wip:
    st.success(f"WIP movie: `{wip}`")
    # Do NOT auto-embed ~30–40MB video on every home load — that dominates latency
    with st.expander("▶ Play WIP movie", expanded=False):
        st.video(wip)
else:
    st.info("No WIP movie yet — remux scenes or run **Rebuild WIP** after clips exist.")

rw1, rw2 = st.columns(2)
with rw1:
    if st.button(
        "🔄 Rebuild WIP from scene composites",
        help="Stitches existing scene_XX_complete.mp4 files (no remux of clips)",
    ):
        with st.spinner("Building movie_wip.mp4…"):
            try:
                path = api.rebuild_wip_movie(reason="home manual rebuild")
                _cached_home.clear()
                if path:
                    st.success(f"Updated `{path}`")
                    st.rerun()
                else:
                    st.warning("No scene composites found to stitch.")
            except Exception as e:
                st.error(str(e))
with rw2:
    if st.button(
        "🔄 Remux all scenes with clips + rebuild WIP",
        help="Rebuild each scene_XX_complete.mp4 from clips, then stitch WIP",
    ):
        with st.spinner("Remux + WIP… can take a minute"):
            try:
                # light list is enough to find which scenes have clips
                scenes = api.list_scenes(light=True)
                nums = [s["scene_number"] for s in scenes if s.get("clips_on_disk")]
                path = api.remux_scenes_and_rebuild_wip(
                    nums, reason="home remux all + WIP"
                )
                _cached_home.clear()
                st.success(path or "Done (check console if path empty)")
                st.rerun()
            except Exception as e:
                st.error(str(e))

st.divider()
st.markdown(
    """
### Pages (sidebar)
| Page | What it does |
|------|----------------|
| **Configuration** | Edit `pipeline_config.json` |
| **Characters** | View refs, generate 3 variants, lock best, cascade-regen clips |
| **Scenes** | Clips, Pass/Fail/Regen, hero 720, model compare |
| **Edit Log** | Reviewer notes → learnings / V16 / script notes |
| **Cost** | Spent vs remaining, per-scene $, model & resolution what-ifs |

### Tips
- After fixing a prompt, use **Regen** (not just Pass).
- Draft at 480 → **Approve (draft)** → **Hero regen at 720** when locked.
- Use **Cost** to plan the rest of the film before big regens.
- Home stays light: open **Play WIP** only when you want the video.
"""
)

if st.button("Reload pipeline from disk"):
    api.reload_engine()
    _cached_home.clear()
    st.success("Reloaded config, blueprint, and state.")
    st.rerun()
