"""Character design & cascade regen page."""
from __future__ import annotations

import streamlit as st

from review_app import pipeline_api as api

st.set_page_config(page_title="Characters", page_icon="👤", layout="wide")
st.title("👤 Characters")
st.caption(
    "View locked references, generate 3 variants, click the best to lock, "
    "or cascade-regenerate clips that use a character."
)

try:
    chars = api.list_characters()
except Exception as e:
    st.error(str(e))
    st.stop()

if "selected_char" not in st.session_state:
    st.session_state.selected_char = chars[0]["key"] if chars else None

keys = [c["key"] for c in chars]
col_nav, col_main = st.columns([1, 3])

with col_nav:
    st.subheader("Cast")
    for c in chars:
        badge = "✅" if c["locked"] else "⬜"
        stale_n = int(c.get("stale_clip_count") or 0)
        stale_mark = f" ⚠️{stale_n}" if stale_n else ""
        label = f"{badge} {c['key']} ({c['clip_count']} clips){stale_mark}"
        if st.button(label, key=f"nav_{c['key']}", use_container_width=True):
            st.session_state.selected_char = c["key"]
            st.rerun()

with col_main:
    key = st.session_state.selected_char
    if not key:
        st.info("No character seeds in blueprint.")
        st.stop()

    char = next((c for c in chars if c["key"] == key), None)
    if not char:
        st.warning("Character not found.")
        st.stop()

    st.header(char["key"])
    meta = []
    if char.get("age_band"):
        meta.append(f"age_band=`{char['age_band']}`")
    if char.get("variant_of"):
        meta.append(f"variant_of=`{char['variant_of']}`")
    rev = int(char.get("revision") or 0)
    meta.append(f"design rev **{rev}**")
    if char.get("revision_updated_at"):
        meta.append(f"updated `{char['revision_updated_at']}`")
    if meta:
        st.caption(" · ".join(meta))
    st.write(char.get("description") or "_No description_")

    stale_n = int(char.get("stale_clip_count") or 0)
    if stale_n:
        st.warning(
            f"**{stale_n} generated clip(s) are out of date** after this character’s last redesign. "
            f"Pipeline will not reuse them as “done” until you regen. "
            f"Stale: {', '.join(f'S{s}C{c}' for s, c in (char.get('stale_clips') or [])[:20])}"
            + ("…" if stale_n > 20 else "")
        )
        if char.get("revision_reason"):
            st.caption(f"Last change: {char['revision_reason']}")

    st.subheader("Locked reference")
    if char["locked"]:
        st.image(char["ref_path"], caption=char["ref_path"], width=320)
    else:
        st.warning("No locked reference yet — generate variants and pick one.")

    b1, b2, b3 = st.columns(3)
    with b1:
        if st.button("🎲 Generate 3 variants", type="primary", key="gen_var"):
            with st.spinner("Calling image model… this can take a minute"):
                try:
                    paths = api.generate_character_variants(key)
                    st.success(f"Saved {len(paths)} variants")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
    with b2:
        if st.button("🔓 Unlock (delete locked ref)", key="unlock"):
            api.unlock_character(key)
            st.success("Unlocked")
            st.rerun()
    with b3:
        if st.button("🔄 Refresh list", key="refresh_chars"):
            api.reload_engine()
            st.rerun()

    st.subheader("Pick best variant")
    variants = char.get("variants") or []
    if not variants:
        st.info("No open variants on disk. Click **Generate 3 variants**.")
    else:
        cols = st.columns(min(3, len(variants)))
        for i, vp in enumerate(variants):
            # variant index from filename
            idx = i + 1
            if "_variant_0" in vp:
                try:
                    idx = int(vp.split("_variant_0")[-1].split(".")[0])
                except ValueError:
                    pass
            with cols[i % 3]:
                st.image(vp, caption=f"Option {idx}", use_container_width=True)
                if st.button(f"Lock option {idx}", key=f"lock_{key}_{idx}"):
                    try:
                        path = api.lock_character_variant(key, idx)
                        st.success(f"Locked → {path}")
                        st.rerun()
                    except Exception as e:
                        st.error(str(e))

    st.divider()
    st.subheader("Clips using this character")

    only_existing = st.checkbox(
        "Only clips already generated on disk (recommended)",
        value=True,
        help="Off = every blueprint mention of this character (can include never-rendered scenes).",
        key=f"only_exist_{key}",
    )
    detail_all = api.clips_using_character_detail(key, only_existing=False)
    detail = api.clips_using_character_detail(key, only_existing=only_existing)
    n_disk = sum(1 for r in detail_all if r["on_disk"])
    st.caption(
        f"Blueprint mentions: **{len(detail_all)}** clip(s) · "
        f"Already on disk: **{n_disk}** · "
        f"Showing for cascade: **{len(detail)}**"
    )

    if not detail:
        if only_existing:
            st.info(
                "No generated clips for this character yet. "
                "Uncheck “only on disk” only if you intentionally want first-time renders."
            )
        else:
            st.write("_None found in visual prompts._")
    else:
        # Group by scene for multi-select
        by_scene: dict = {}
        for r in detail:
            by_scene.setdefault(r["scene"], []).append(r)

        scene_options = sorted(by_scene.keys())
        pick_scenes = st.multiselect(
            "Scenes to include",
            options=scene_options,
            default=scene_options,
            format_func=lambda s: f"Scene {s} ({len(by_scene[s])} clip(s))",
            key=f"cascade_scenes_{key}",
        )

        clip_options = []
        for sn in pick_scenes:
            for r in by_scene[sn]:
                disk = "on disk" if r["on_disk"] else "NOT generated"
                clip_options.append((r["scene"], r["clip"], f"S{r['scene']}C{r['clip']} ({disk})"))

        labels = [t[2] for t in clip_options]
        default_labels = labels  # all selected by default within chosen scenes
        picked_labels = st.multiselect(
            "Clips to regenerate",
            options=labels,
            default=default_labels,
            key=f"cascade_clips_{key}",
        )
        selected_pairs = [
            (t[0], t[1]) for t in clip_options if t[2] in picked_labels
        ]
        st.write(f"**{len(selected_pairs)}** clip(s) selected.")

    st.subheader("Cascade regenerate")
    st.warning(
        "This **wipes and re-renders** the selected clips only. "
        "It does not invent new story scenes — it only redoes clips that match the filters. "
        "With “only on disk” checked, never-generated clips are skipped."
    )
    cascade_feedback = st.text_area(
        "Optional feedback to append to each clip prompt",
        placeholder="e.g. match locked Character_N_Young child proportions, not adult bodybuilder",
        key="cascade_fb",
    )
    dry = st.checkbox("Dry run (list only)", value=True)
    if st.button("Run cascade", key="cascade_go"):
        if not detail:
            st.error("Nothing to regenerate with current filters.")
        elif not selected_pairs:
            st.error("Select at least one clip.")
        else:
            with st.spinner("Working…"):
                try:
                    hits = api.cascade_regen_character(
                        key,
                        feedback=cascade_feedback.strip(),
                        dry_run=dry,
                        only_existing=only_existing,
                        selected=selected_pairs,
                    )
                    if dry:
                        st.info(f"Would regenerate {len(hits)} clips: {hits}")
                    else:
                        st.success(f"Regenerated {len(hits)} clips: {hits}")
                except Exception as e:
                    st.error(str(e))
