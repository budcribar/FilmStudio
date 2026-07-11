"""
Nick and Me — Streamlit Review Console

Run from repo root:
  pip install -r requirements-review.txt
  streamlit run streamlit_app.py
"""
from __future__ import annotations

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

try:
    eng = api.get_engine()
    title = api.movie_title()
    scenes = api.list_scenes()
    chars = api.list_characters()
except Exception as e:
    st.error(f"Failed to load pipeline: {e}")
    st.info("Run from the repo root with `nickandme.json` and `pipeline_config.json` present.")
    st.stop()

c1, c2, c3, c4 = st.columns(4)
c1.metric("Movie", title[:28] + ("…" if len(title) > 28 else ""))
c2.metric("Scenes", len(scenes))
c3.metric("Characters", len(chars))
approved = sum(1 for s in scenes if s["approved"])
c4.metric("Scenes approved", f"{approved}/{len(scenes)}")

st.divider()
st.subheader("Quick status")

done_clips = sum(s["clips_on_disk"] for s in scenes)
total_clips = sum(s["clip_count"] for s in scenes)
st.progress(
    done_clips / total_clips if total_clips else 0,
    text=f"Clips on disk: {done_clips} / {total_clips}",
)

locked = sum(1 for c in chars if c["locked"])
st.write(f"**Characters locked:** {locked} / {len(chars)}")

try:
    stale = api.list_stale_clips(only_existing=True)
except Exception:
    stale = []
if stale:
    st.warning(
        f"**{len(stale)} clip(s) out of date** after character redesigns — "
        f"{', '.join(r['label'] for r in stale[:15])}"
        + ("…" if len(stale) > 15 else "")
        + ". Regenerate them (Scenes or character cascade). Pipeline will not reuse them as done."
    )
else:
    st.caption("No stale clips (all on-disk renders match current character revisions).")

wip = api.wip_path()
if wip:
    st.success(f"WIP movie: `{wip}`")
    st.video(wip)
else:
    st.info("No WIP movie yet — approve a scene after clips exist.")

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
"""
)

if st.button("Reload pipeline from disk"):
    api.reload_engine()
    st.success("Reloaded config, blueprint, and state.")
    st.rerun()
