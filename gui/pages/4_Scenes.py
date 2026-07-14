"""Scene list + clip drill-down review page."""
from __future__ import annotations

import streamlit as st

import review_app  # noqa: F401 — path bootstrap for renderer
from review_app import pipeline_api as api

st.title("🎞️ Scenes & Clips")

# Background gen: banner + Cancel + auto-refresh while running
try:
    from review_app.gen_jobs import render_gen_job_banner, is_gen_running

    _gen_job = render_gen_job_banner(
        compact=False, auto_refresh=True, key_prefix="scenes_gen"
    )
    _gen_running = is_gen_running()
except Exception:
    _gen_job = {"status": "idle"}
    _gen_running = False

# ---- Stage 2 gate: empty blueprint needs clip plan from Stage 1 ----
try:
    s2 = api.stage2_status()
except Exception:
    s2 = {}

if s2.get("stage2_ready") and s2.get("stage2_stale"):
    st.warning(
        "**Stage 2 is stale** — Stage 1 changed after the last clip plan. "
        "Regenerating video will use **old** prompts until you re-plan."
    )
    if st.button(
        "▶ Re-plan Stage 2 from Stage 1",
        type="primary",
        key="scenes_replan_stage2_stale",
        disabled=_gen_running,
    ):
        try:
            with st.spinner("Re-planning Stage 2…"):
                summary = api.run_stage2_from_stage1()
            if summary.get("ok"):
                st.success(summary.get("message") or "Stage 2 updated.")
                try:
                    from review_app.pipeline_progress import invalidate_progress_cache

                    invalidate_progress_cache()
                except Exception:
                    pass
                st.rerun()
            else:
                st.error(summary.get("message") or "Stage 2 replan failed.")
        except Exception as e:
            st.error(str(e))

if not s2.get("stage2_ready"):
    st.warning(
        "No Stage 2 clip plan yet — this page lists **blueprint** scenes/clips, "
        "not the Stage 1 bible alone."
    )
    if s2.get("stage1_exists"):
        st.info(
            f"Stage 1 is ready (**{s2.get('stage1_scenes', 0)}** scenes). "
            "Generate the Grok clip plan to populate Scenes & Clips."
        )
        if st.button(
            "▶ Generate Stage 2 plan",
            type="primary",
            key="scenes_gen_stage2",
            help="Converts Stage 1 beats → clip durations, visual prompts, audio payloads",
        ):
            try:
                with st.spinner("Building Stage 2 clip plan…"):
                    summary = api.run_stage2_from_stage1()
                if summary.get("ok"):
                    st.session_state["scenes_stage2_flash"] = {
                        "ok": True,
                        "message": summary.get("message")
                        or (
                            f"Stage 2 complete: {summary.get('scenes')} scenes · "
                            f"{summary.get('clips')} clips"
                        ),
                    }
                else:
                    st.session_state["scenes_stage2_flash"] = {
                        "ok": False,
                        "message": summary.get("message") or "Stage 2 produced no clips",
                    }
                st.rerun()
            except Exception as e:
                st.error(str(e))
    else:
        st.error(
            "Stage 1 is missing. Go to **Adaptation** → Import book → Run Stage 1, "
            "then return here to generate Stage 2."
        )
    st.caption(
        "CLI: `python scripts/two_stage_adaptation/stage2_plan_grok.py "
        "--stage1 projects/<id>/scenes.json --out projects/<id>/blueprint.clips.grok.json`"
    )
    st.stop()

try:
    # Light list for navigation (no per-scene cost math × 90)
    scenes = api.list_scenes(light=True)
except Exception as e:
    st.error(str(e))
    st.stop()

if not scenes:
    st.info(
        "Blueprint reports Stage 2 ready but list is empty — try **Generate Stage 2** again "
        "or reload the app."
    )
    if st.button("▶ Re-generate Stage 2 plan", key="scenes_regen_stage2_empty"):
        try:
            with st.spinner("Building Stage 2 clip plan…"):
                summary = api.run_stage2_from_stage1()
            st.success(
                f"{summary.get('scenes')} scenes · {summary.get('clips')} clips"
            )
            st.rerun()
        except Exception as e:
            st.error(str(e))
    st.stop()

if "scene_num" not in st.session_state:
    # Prefer first incomplete / with clips
    st.session_state.scene_num = scenes[0]["scene_number"] if scenes else 1
if "clip_num" not in st.session_state:
    st.session_state.clip_num = None

# ---- Scene picker ----
if st.session_state.clip_num is None:
    st.subheader("All scenes")
    _s2_flash = st.session_state.pop("scenes_stage2_flash", None)
    if isinstance(_s2_flash, dict):
        if _s2_flash.get("ok"):
            st.success(f"**Stage 2 ran successfully.** {_s2_flash.get('message') or ''}")
        else:
            st.error(_s2_flash.get("message") or "Stage 2 failed")
    with st.expander("Stage 2 plan", expanded=False):
        when = s2.get("last_completed_at") or ""
        st.caption(
            f"{s2.get('stage2_scenes', 0)} scenes · {s2.get('stage2_clips', 0)} clips"
            + (f" · last plan **{when}**" if when else "")
            + f" · Stage 1 bible ~{s2.get('stage1_scenes', 0)} scenes."
        )
        if s2.get("last_run_message"):
            st.caption(s2["last_run_message"])
        if st.button(
            "🔁 Re-generate Stage 2 plan",
            key="scenes_regen_stage2",
            help="Replaces blueprint clip plans from current Stage 1 (backs up previous file)",
        ):
            try:
                with st.spinner("Rebuilding Stage 2 clip plan…"):
                    summary = api.run_stage2_from_stage1()
                st.session_state["scenes_stage2_flash"] = {
                    "ok": bool(summary.get("ok")),
                    "message": summary.get("message")
                    or (
                        f"{summary.get('scenes')} scenes · {summary.get('clips')} clips"
                    ),
                }
                st.rerun()
            except Exception as e:
                st.error(str(e))
    if "playing_scene" not in st.session_state:
        st.session_state.playing_scene = None

    q = st.text_input("Filter setting text", "")
    rows = []
    for s in scenes:
        if q and q.lower() not in (s.get("setting") or "").lower():
            continue
        rows.append(s)

    # ---- Multi-select batch generate ----
    # Options are stable scene numbers (not labels with "0/4" counts — those change
    # after generate and break st.multiselect defaults).
    incomplete = [
        int(s["scene_number"])
        for s in rows
        if int(s.get("clips_on_disk") or 0) < int(s.get("clip_count") or 0)
    ]
    all_sns = [int(s["scene_number"]) for s in rows]
    label_by_sn = {
        int(s["scene_number"]): (
            f"S{int(s['scene_number']):02d} — {(s.get('setting') or '')[:40]} "
            f"({s.get('clips_on_disk', 0)}/{s.get('clip_count', 0)})"
        )
        for s in rows
    }

    # Sanitize session selection: drop unknown ids / migrate old string labels
    raw_pick = st.session_state.get("batch_scene_pick")
    if raw_pick is None:
        st.session_state["batch_scene_pick"] = []
    else:
        cleaned: list[int] = []
        for item in raw_pick:
            if isinstance(item, int) and item in label_by_sn:
                cleaned.append(item)
            elif isinstance(item, str):
                # old format was full label string — extract SNN if possible
                sn_try = None
                if item.startswith("S") and len(item) >= 3:
                    try:
                        sn_try = int(item[1:3])
                    except ValueError:
                        sn_try = None
                if sn_try is not None and sn_try in label_by_sn:
                    cleaned.append(sn_try)
        # de-dupe preserve order
        seen: set[int] = set()
        uniq: list[int] = []
        for n in cleaned:
            if n not in seen:
                seen.add(n)
                uniq.append(n)
        st.session_state["batch_scene_pick"] = uniq

    with st.container(border=True):
        st.markdown("**Batch generate**")
        st.caption(
            "Select one or more scenes, then generate. "
            "Uses Grok API (needs `XAI_API_KEY`). Can take a long time."
        )
        b1, b2, b3 = st.columns(3)
        with b1:
            if st.button("Select incomplete", key="sel_incomplete", width="stretch"):
                st.session_state["batch_scene_pick"] = list(incomplete)
                st.rerun()
        with b2:
            if st.button("Select all", key="sel_all", width="stretch"):
                st.session_state["batch_scene_pick"] = list(all_sns)
                st.rerun()
        with b3:
            if st.button("Clear selection", key="sel_clear", width="stretch"):
                st.session_state["batch_scene_pick"] = []
                st.rerun()

        selected_sns = st.multiselect(
            "Scenes to generate",
            options=all_sns,
            format_func=lambda n: label_by_sn.get(int(n), f"Scene {n}"),
            key="batch_scene_pick",
            help="Select scenes by number; labels show current clip progress",
        )
        selected_sns = sorted({int(n) for n in (selected_sns or [])})

        only_missing_batch = st.checkbox(
            "Only missing clips (skip clips already on disk)",
            value=True,
            key="batch_only_missing",
        )
        run_qa_batch = st.checkbox(
            "Run QA after each clip",
            value=True,
            key="batch_run_qa",
        )
        stop_on_fail = st.checkbox(
            "Stop batch on first failure",
            value=True,
            key="batch_stop_fail",
        )

        missing_clips_est = 0
        for s in rows:
            sn = int(s["scene_number"])
            if sn not in selected_sns:
                continue
            total_c = int(s.get("clip_count") or 0)
            on_c = int(s.get("clips_on_disk") or 0)
            if only_missing_batch:
                missing_clips_est += max(0, total_c - on_c)
            else:
                missing_clips_est += total_c

        gen_disabled = not selected_sns or (only_missing_batch and missing_clips_est == 0)
        gen_label = (
            f"▶ Generate {len(selected_sns)} scene(s)"
            + (f" · ~{missing_clips_est} clip(s)" if selected_sns else "")
        )
        if st.button(
            gen_label,
            type="primary",
            key="batch_gen_go",
            disabled=gen_disabled or _gen_running,
            width="stretch",
        ):
            try:
                job = api.start_batch_gen_job(
                    selected_sns,
                    only_missing=bool(only_missing_batch),
                    run_qa=bool(run_qa_batch),
                    remux=True,
                    stop_on_fail=bool(stop_on_fail),
                )
                st.success(
                    f"Started background generation for {len(selected_sns)} scene(s) "
                    f"({job.get('status')}). Use **Cancel** above to stop after the current clip."
                )
                st.rerun()
            except Exception as e:
                st.error(str(e))

    st.caption("Click a scene row to open it. Use the batch box above to generate several at once.")

    # Inline player for the scene chosen via ▶
    play_sn = st.session_state.playing_scene
    if play_sn is not None:
        play_row = next((r for r in scenes if r["scene_number"] == play_sn), None)
        play_path = (play_row or {}).get("play_path") or (play_row or {}).get("composite_path")
        if not play_path:
            # light list may omit per-clip paths — resolve first on-disk clip
            from renderer import clip_output_path, file_is_usable

            for cn in range(1, int((play_row or {}).get("clip_count") or 0) + 1):
                p = clip_output_path(play_sn, cn)
                if file_is_usable(p, min_bytes=1024):
                    play_path = p
                    break
        with st.container(border=True):
            pc1, pc2 = st.columns([5, 1])
            with pc1:
                st.markdown(f"**Playing Scene {play_sn:02d}**")
                if play_path:
                    st.video(play_path)
                    src = "composite" if str(play_path).endswith("complete.mp4") else "clip"
                    st.caption(f"`{play_path}` ({src})")
                else:
                    st.info("No video on disk for this scene yet.")
            with pc2:
                if st.button("Close player", key="close_player"):
                    st.session_state.playing_scene = None
                    st.rerun()

    for s in rows:
        sn = s["scene_number"]
        stale_n = int(s.get("stale_clips") or 0)
        on_disk = int(s.get("clips_on_disk") or 0)
        n_clips = int(s.get("clip_count") or 0)
        incomplete = n_clips > 0 and on_disk < n_clips
        if s.get("dirty"):
            status = "🔁"
        elif s.get("approved_incomplete") or (
            s.get("approved_flag") and incomplete
        ):
            # Was marked approved but clips missing — never show ✅
            status = "⚠️"
        elif stale_n:
            status = "⚠️"
        elif s.get("is_hero"):
            status = "⭐"
        elif s.get("approved") and not incomplete:
            status = "✅"
        elif on_disk:
            status = "📦"
        else:
            status = "·"
        stale_txt = f" · {stale_n} stale" if stale_n else ""
        dirty_txt = f" · dirty {s.get('dirty_cascade')}" if s.get("dirty") else ""
        hero_txt = f" · hero {s.get('hero_resolution')}" if s.get("is_hero") else ""
        miss_txt = f" · incomplete" if incomplete else ""
        if s.get("approved_incomplete") or (s.get("approved_flag") and incomplete):
            miss_txt = " · approved but missing clips"
        label = (
            f"{status} Scene {sn:02d} — {s.get('setting', '')[:56]} "
            f"({on_disk}/{n_clips} clips{stale_txt}{dirty_txt}{hero_txt}{miss_txt})"
        )
        # open | play | badges
        selected_mark = "☑ " if int(sn) in selected_sns else ""
        cols = st.columns([3.6, 0.5, 0.9])
        with cols[0]:
            if st.button(
                f"{selected_mark}{label}",
                key=f"open_s{sn}",
                width="stretch",
            ):
                st.session_state.scene_num = sn
                st.session_state.clip_num = 0  # 0 = scene overview
                st.session_state.playing_scene = None
                st.rerun()
        with cols[1]:
            if s.get("play_path") or s.get("composite_path"):
                if st.button("▶", key=f"play_s{sn}", help=f"Play scene {sn}", width="stretch"):
                    st.session_state.playing_scene = sn
                    st.rerun()
            else:
                st.caption("")
        with cols[2]:
            help_bits = []
            if int(sn) in selected_sns:
                help_bits.append("sel")
            if s.get("dirty"):
                help_bits.append("dirty")
            if incomplete:
                help_bits.append("missing")
            if s.get("composite_path") or s.get("composite_exists"):
                help_bits.append("mux")
            if s.get("approved") and not incomplete:
                help_bits.append("ok")
            elif s.get("approved_flag") and incomplete:
                help_bits.append("need clips")
            st.caption(" · ".join(help_bits) if help_bits else "")

    st.stop()

# ---- Scene overview or clip detail ----
sn = int(st.session_state.scene_num)
cn = st.session_state.clip_num

scene = api.get_scene(sn)
if not scene:
    st.error(f"Scene {sn} not found")
    st.stop()

# Prefer clip counts from light scene list when possible (avoids second full scan)
scene_meta = next((x for x in scenes if x["scene_number"] == sn), {})
# list_clips only when drilling into a scene (overview + clip detail both need it)
clips = api.list_clips(sn)
on_disk_n = sum(1 for r in clips if r.get("on_disk"))
total_n = len(clips)
missing_n = total_n - on_disk_n
try:
    draft_res = str((api.get_engine().config or {}).get("resolution", "480p"))
except Exception:
    draft_res = "480p"

# =====================================================================
# SCENE OVERVIEW — simple path first; advanced tools collapsed
# =====================================================================
if cn is None or cn == 0:
    if st.button("← All scenes"):
        st.session_state.clip_num = None
        st.rerun()

    st.header(f"Scene {sn}: {scene.get('setting', '')}")
    st.caption(
        f"{total_n} clips · {on_disk_n} on disk · "
        f"~{scene.get('total_estimated_duration_seconds')}s · draft **{draft_res}**"
    )

    dirty = api.get_scene_dirty(sn)
    if dirty and (dirty.get("stage1") or dirty.get("stage2")):
        cascade = "stage1→stage2" if dirty.get("stage1") else "stage2"
        st.warning(
            f"Needs replan ({cascade}): {dirty.get('reason') or 'marked dirty'}"
        )

    # ---- Primary: generate video ----
    st.subheader("Generate video")
    if missing_n > 0:
        st.info(
            f"**{missing_n} of {total_n}** clips have no video yet. "
            "Click the button below to call Grok and create them (needs `XAI_API_KEY`)."
        )
    else:
        st.success(f"All **{total_n}** clips are on disk. Open a clip to review or re-generate one.")

    gen_missing = st.checkbox(
        "Only missing clips (skip ones already on disk)",
        value=True,
        key=f"gen_missing_{sn}",
    )
    run_qa_scene = st.checkbox(
        "Run QA after each clip",
        value=True,
        key=f"gen_qa_{sn}",
    )
    # Cost estimate is deferred — computing it every rerun felt sluggish on WSL
    if st.checkbox("Show cost estimate", value=False, key=f"show_cost_{sn}"):
        try:
            est = api.scene_cost_estimate(
                sn, mode="all" if not gen_missing else "missing"
            ) or {}
            if est:
                st.caption(
                    f"Est. ~**${float(est.get('total_usd') or 0):.2f}** · "
                    f"{est.get('clip_count', '?')} clips @ {est.get('resolution') or draft_res}"
                )
        except Exception:
            st.caption("Cost estimate unavailable.")

    gen_label = (
        f"▶ Generate missing clips ({missing_n})"
        if gen_missing and missing_n > 0
        else (
            f"▶ Generate all {total_n} clips"
            if not gen_missing
            else "▶ Generate scene clips"
        )
    )
    gen_disabled = total_n == 0 or (gen_missing and missing_n == 0) or _gen_running
    if _gen_running:
        st.caption("Generation job is running — use **Cancel** at the top of this page.")
    if st.button(
        gen_label,
        type="primary",
        key=f"gen_scene_{sn}",
        disabled=gen_disabled,
        help="Starts background Grok generation (cancelable). Remuxes the scene when done.",
    ):
        try:
            job = api.start_scene_gen_job(
                sn,
                only_missing=bool(gen_missing),
                run_qa=bool(run_qa_scene),
                remux=True,
            )
            st.success(
                f"Started background generation for scene {sn} ({job.get('status')}). "
                "Use **Cancel** to stop after the current clip."
            )
            st.rerun()
        except Exception as e:
            st.error(str(e))

    # Play composite if any
    if scene_meta.get("composite_path") or scene_meta.get("play_path"):
        play_p = scene_meta.get("composite_path") or scene_meta.get("play_path")
        with st.expander("Play scene composite", expanded=on_disk_n == total_n and total_n > 0):
            st.video(play_p)

    # ---- Clip list (main navigation) ----
    st.subheader("Clips")
    st.caption(
        "Open a clip to review, or multi-select clips below to regen several at once."
    )

    # Multi-select batch regen for this scene
    clip_opts = [int(r["clip_number"]) for r in clips]
    if clip_opts:
        pick_key = f"clip_pick_{sn}"
        if pick_key not in st.session_state:
            st.session_state[pick_key] = []
        # Sanitize selection if clip list changed
        raw_pick = st.session_state.get(pick_key) or []
        try:
            cleaned = [int(x) for x in raw_pick if int(x) in clip_opts]
        except (TypeError, ValueError):
            cleaned = []
        if cleaned != list(raw_pick):
            st.session_state[pick_key] = cleaned

        with st.container(border=True):
            st.markdown("**Multi-select regen**")
            cqa, c_all, c_miss, c_clr = st.columns(4)
            with cqa:
                multi_qa = st.checkbox(
                    "QA after each",
                    value=True,
                    key=f"multi_clip_qa_{sn}",
                    disabled=_gen_running,
                )
            with c_all:
                if st.button("Select all", key=f"clip_sel_all_{sn}", disabled=_gen_running):
                    st.session_state[pick_key] = list(clip_opts)
                    st.rerun()
            with c_miss:
                missing_ids = [
                    int(r["clip_number"]) for r in clips if not r.get("on_disk")
                ]
                if st.button(
                    "Select missing",
                    key=f"clip_sel_miss_{sn}",
                    disabled=_gen_running or not missing_ids,
                ):
                    st.session_state[pick_key] = missing_ids
                    st.rerun()
            with c_clr:
                if st.button("Clear", key=f"clip_sel_clr_{sn}", disabled=_gen_running):
                    st.session_state[pick_key] = []
                    st.rerun()

            def _fmt_clip(cn: int) -> str:
                row = next((r for r in clips if int(r["clip_number"]) == int(cn)), None)
                if not row:
                    return f"C{cn}"
                disk = "ready" if row.get("on_disk") else "missing"
                stale = " · stale" if row.get("stale") else ""
                return f"C{cn} ({disk}{stale})"

            selected_clips = st.multiselect(
                "Clips to regenerate",
                options=clip_opts,
                format_func=_fmt_clip,
                key=pick_key,
                disabled=_gen_running,
                help="Selected clips are force-regenerated (even if already on disk).",
            )
            n_sel = len(selected_clips or [])
            go = st.button(
                f"▶ Regen selected ({n_sel})" if n_sel else "▶ Regen selected",
                type="primary",
                key=f"clip_multi_go_{sn}",
                disabled=_gen_running or n_sel == 0,
                width="stretch",
            )
            if go and selected_clips:
                try:
                    job = api.start_clips_gen_job(
                        sn,
                        [int(c) for c in selected_clips],
                        run_qa=bool(multi_qa),
                        remux=True,
                    )
                    st.success(
                        f"Started regen for C{', C'.join(str(c) for c in sorted(selected_clips))} "
                        f"({job.get('status')}). Use **Cancel** to stop after the current clip."
                    )
                    st.rerun()
                except Exception as e:
                    st.error(str(e))

    for row in clips:
        cnum = row["clip_number"]
        disk = "🟢 ready" if row["on_disk"] else "⚪ not generated"
        rev = row.get("review_status") or "pending"
        if row.get("stale"):
            rev_s = "stale"
        else:
            rev_s = {"pass": "pass", "fail": "fail", "pending": "pending"}.get(rev, rev)
        preview = (row.get("visual_prompt") or "")[:64].replace("\n", " ")
        c1, c2 = st.columns([4, 1])
        with c1:
            if st.button(
                f"Clip {cnum} · {disk} · {rev_s} — {preview}",
                key=f"open_c{cnum}",
                width="stretch",
            ):
                st.session_state.clip_num = cnum
                st.rerun()
        with c2:
            if not row["on_disk"]:
                if st.button(
                    "▶ Gen",
                    key=f"quick_gen_{sn}_{cnum}",
                    help=f"Generate clip {cnum} only (background)",
                    disabled=_gen_running,
                ):
                    try:
                        api.start_clip_gen_job(sn, int(cnum), run_qa=True)
                        st.rerun()
                    except Exception as e:
                        st.error(str(e))

    # ---- Secondary: finish scene ----
    st.divider()
    st.subheader("When clips look good")
    st.caption(
        "**Approve** marks the scene done and remuxes this scene (if checked). "
        "The full **WIP movie** rebuilds in the **background** (append when possible; "
        "cancels & restarts a full rebuild if you approve another scene mid-job)."
    )
    remux_on_approve = st.checkbox(
        "Remux scene when approving",
        value=True,
        key=f"approve_remux_{sn}",
        help="FFmpeg concat of this scene's clips into one file (needed before WIP can include it)",
    )

    # Background WIP status banner
    try:
        wip_job = api.wip_job_status()
    except Exception:
        wip_job = {}
    wip_st = (wip_job or {}).get("status") or "idle"
    if wip_st == "running":
        q = wip_job.get("queued") or []
        st.info(
            f"🎬 WIP job running (**{wip_job.get('mode') or '…'}**): "
            f"{wip_job.get('message') or ''}"
            + (f" · queue {q}" if q else "")
        )
        if st.button("Refresh WIP status", key=f"wip_refresh_{sn}"):
            st.rerun()
    elif wip_st == "done":
        st.caption(f"WIP up to date · {wip_job.get('path') or wip_job.get('message') or ''}")
    elif wip_st == "error":
        st.warning(f"WIP job error: {wip_job.get('error') or wip_job.get('message')}")
    elif wip_st == "cancelled":
        st.caption("WIP job was cancelled (usually superseded by a newer approval).")

    if missing_n > 0:
        st.warning(
            f"**{missing_n} clip(s) missing** ({on_disk_n}/{total_n} on disk). "
            "Generate them before approving — ✅ only applies when all clips exist."
        )
        if scene_meta.get("approved_flag") or scene_meta.get("approved_incomplete"):
            st.caption(
                "This scene was previously marked approved, but clips are incomplete — "
                "the list no longer shows a full checkmark."
            )
            if st.button("Clear incomplete approval flag", key=f"clr_appr_{sn}"):
                try:
                    eng = api.get_engine()
                    eng.state.setdefault("scenes_completed", {}).pop(str(sn), None)
                    eng.save_state()
                    st.success("Approval flag cleared.")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))

    a1, a2, a3 = st.columns(3)
    with a1:
        if st.button(
            "Approve scene",
            type="primary" if on_disk_n == total_n and total_n else "secondary",
            key=f"approve_{sn}",
            help="Requires all clips on disk. Approve + optional remux; WIP in background.",
            disabled=missing_n > 0 or total_n == 0,
        ):
            steps = ["saving approval"]
            if remux_on_approve:
                steps.append("remux")
            steps.append("queue WIP")
            with st.spinner(" · ".join(steps) + "…"):
                try:
                    result = api.approve_scene(
                        sn,
                        remux=bool(remux_on_approve),
                        rebuild_wip=False,
                        background_wip=True,
                        require_all_clips=True,
                    )
                    msg = "Scene approved."
                    if result.get("remuxed"):
                        msg += " Remuxed."
                    job = result.get("wip_job") or {}
                    if job:
                        msg += (
                            f" WIP **{job.get('mode') or 'job'}** started in background"
                            f" ({job.get('status')})."
                        )
                    if result.get("remux_error"):
                        msg += f" Remux warning: {result['remux_error']}"
                    if result.get("wip_job_error"):
                        msg += f" WIP schedule warning: {result['wip_job_error']}"
                    st.success(msg)
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
    with a2:
        if st.button("Remux scene", key=f"remux_{sn}"):
            with st.spinner("FFmpeg remux (this scene only)…"):
                try:
                    path = api.remux_scene(sn)
                    st.success(path or "No clips to remux")
                except Exception as e:
                    st.error(str(e))
    with a3:
        if st.button("Rebuild WIP now", key=f"wip_{sn}", help="Queue a full background WIP rebuild"):
            try:
                from review_app.wip_jobs import schedule_wip_update

                job = schedule_wip_update(
                    api.get_engine(),
                    scene_num=None,
                    reason=f"manual full rebuild S{sn}",
                    force_full=True,
                )
                st.success(
                    f"WIP job: {job.get('status')} / {job.get('mode')} — {job.get('message')}"
                )
                st.rerun()
            except Exception as e:
                st.error(str(e))

    # ---- Advanced (collapsed) ----
    with st.expander("Cost estimate", expanded=False):
        if st.button("Compute cost", key=f"compute_cost_{sn}"):
            c_all = api.scene_cost_estimate(sn, mode="all") or {}
            c_ex = api.scene_cost_estimate(sn, mode="existing") or {}
            k1, k2 = st.columns(2)
            k1.metric("All clips", f"${float(c_all.get('total_usd') or 0):.2f}")
            k2.metric("On disk only", f"${float(c_ex.get('total_usd') or 0):.2f}")
            st.caption(
                f"`{c_all.get('model_name')}` @ {c_all.get('resolution')} · "
                f"{c_all.get('total_duration_sec')}s"
            )
        else:
            st.caption("Click **Compute cost** when you want an estimate (skipped by default for speed).")

    with st.expander("Learning cascade (replan flags)", expanded=False):
        st.caption(
            "Mark for Stage 1/2 replan when the failure is not a single bad take. "
            "Does not auto-run planners."
        )
        d1, d2 = st.columns(2)
        with d1:
            if st.button("Needs Stage 2 replan", key=f"dirty_s2_{sn}", width="stretch"):
                try:
                    api.mark_scene_needs_replan(
                        sn, cascade="stage2", reason=f"Manual Stage 2 replan for scene {sn}"
                    )
                    st.success("Marked dirty (stage2).")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
        with d2:
            if st.button("Needs Stage 1→2 re-bible", key=f"dirty_s1_{sn}", width="stretch"):
                try:
                    api.mark_scene_needs_replan(
                        sn, cascade="stage1", reason=f"Manual Stage 1→2 for scene {sn}"
                    )
                    st.success("Marked dirty (stage1→stage2).")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
        if dirty and (dirty.get("stage1") or dirty.get("stage2")):
            c1, c2 = st.columns(2)
            with c1:
                if st.button("Clear Stage 2 dirty", key=f"clr_s2_{sn}"):
                    api.clear_scene_replan_flag(sn, keys=["stage2"])
                    st.rerun()
            with c2:
                if st.button("Clear all dirty", key=f"clr_all_{sn}"):
                    api.clear_scene_replan_flag(sn)
                    st.rerun()

    with st.expander("Hero / delivery pass (later)", expanded=False):
        hero_info = scene_meta.get("hero")
        if hero_info is None:
            try:
                hero_info = api.get_engine().get_scene_hero(sn)
            except Exception:
                hero_info = None
        if hero_info:
            st.success(
                f"Hero locked @ **{hero_info.get('resolution')}** "
                f"({hero_info.get('clip_count')} clips)"
            )
        else:
            st.caption(
                f"Draft is **{draft_res}**. After you like the scene, hero regen "
                "re-renders on-disk clips at delivery resolution."
            )
        h1, h2, h3 = st.columns(3)
        with h1:
            hero_res = st.selectbox(
                "Hero resolution",
                options=["720p", "1080p", "480p"],
                index=0,
                key=f"hero_res_{sn}",
            )
        with h2:
            hero_only_disk = st.checkbox(
                "Only clips on disk", value=True, key=f"hero_disk_{sn}"
            )
        with h3:
            hero_qa = st.checkbox("Run QA", value=True, key=f"hero_qa_{sn}")
        hero_approve = st.checkbox(
            "Approve after success", value=True, key=f"hero_appr_{sn}"
        )
        try:
            hero_est = api.hero_cost_note(sn, resolution=hero_res)
            st.caption(
                f"Est. @ {hero_res}: ${float(hero_est.get('total_usd') or 0):.2f} "
                f"({hero_est.get('clip_count')} clips)"
            )
        except Exception:
            pass
        hb1, hb2 = st.columns(2)
        with hb1:
            if st.button(f"Hero regen at {hero_res}", key=f"hero_go_{sn}"):
                with st.spinner(f"Hero regen @ {hero_res}…"):
                    try:
                        meta = api.hero_regen_scene(
                            sn,
                            resolution=hero_res,
                            only_existing=hero_only_disk,
                            run_qa=hero_qa,
                            approve_after=hero_approve,
                        )
                        failed = meta.get("failed") or []
                        if failed:
                            st.warning(f"Partial failures: {failed}")
                        else:
                            st.success(f"Hero complete @ {meta.get('resolution')}")
                        st.rerun()
                    except Exception as e:
                        st.error(str(e))
        with hb2:
            if hero_info and st.button("Clear hero flag", key=f"hero_clear_{sn}"):
                try:
                    api.clear_scene_hero(sn)
                    st.success("Hero flag cleared.")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))

    with st.expander("Model comparison / variants (optional)", expanded=False):
        pref = api.scene_video_settings(sn)
        models = api.available_video_models()
        model_labels = [
            f"{m.get('label') or m.get('model_name')} ({m.get('provider')}/{m.get('model_name')})"
            for m in models
        ]
        label_to_model = dict(zip(model_labels, models))
        st.caption(
            f"Preferred: **{pref.get('provider')}** / `{pref.get('model_name')}`"
        )
        pick = st.selectbox(
            "Set preferred model",
            options=["(use global default)"] + model_labels,
            key=f"pref_model_{sn}",
        )
        if st.button("Save preferred model", key=f"save_pref_{sn}"):
            try:
                if pick.startswith("(use"):
                    api.set_scene_video_settings(sn, clear=True)
                else:
                    m = label_to_model[pick]
                    api.set_scene_video_settings(
                        sn, provider=m.get("provider"), model_name=m.get("model_name")
                    )
                st.success("Saved.")
                st.rerun()
            except Exception as e:
                st.error(str(e))
        if st.button("Snapshot main", key=f"snap_{sn}"):
            try:
                vid = api.snapshot_main_variant(sn)
                st.success(vid or "Nothing to snapshot")
                st.rerun()
            except Exception as e:
                st.error(str(e))
        gen_pick = st.selectbox(
            "Model for new variant", options=model_labels, key=f"gen_model_{sn}"
        )
        only_exist = st.checkbox(
            "Only clips already on disk", value=True, key=f"var_exist_{sn}"
        )
        if st.button("Generate variant for comparison", key=f"gen_var_{sn}"):
            m = label_to_model[gen_pick]
            with st.spinner("Generating variant…"):
                try:
                    meta = api.generate_scene_variant(
                        sn,
                        provider=str(m.get("provider")),
                        model_name=str(m.get("model_name")),
                        only_existing=only_exist,
                        run_qa=False,
                        label=m.get("label"),
                    )
                    st.success(f"Variant: {meta.get('label')}")
                    st.rerun()
                except Exception as e:
                    st.error(str(e))
        vinfo = api.list_scene_variants(sn)
        variants = vinfo.get("variants") or {}
        playable = {
            vid: meta
            for vid, meta in variants.items()
            if meta.get("composite_path")
        }
        if len(playable) >= 1:
            ids = list(playable.keys())
            left_id = st.selectbox(
                "Compare A",
                options=ids,
                format_func=lambda i: playable[i].get("label") or i,
                key=f"cmp_a_{sn}",
            )
            st.video(playable[left_id]["composite_path"])
            right_id = st.selectbox(
                "Compare B",
                options=ids,
                index=min(1, len(ids) - 1),
                format_func=lambda i: playable[i].get("label") or i,
                key=f"cmp_b_{sn}",
            )
            st.video(playable[right_id]["composite_path"])
        else:
            st.caption("No variant composites yet.")

    st.stop()

# =====================================================================
# SINGLE CLIP — no scene-level clutter
# =====================================================================
if st.button("← Back to scene"):
    st.session_state.clip_num = 0
    st.rerun()

st.header(f"Scene {sn} · Clip {cn}")
st.caption(scene.get("setting") or "")

row = api.get_clip(sn, int(cn))
if not row:
    st.error("Clip not found")
    st.stop()

st.subheader(f"Clip {cn} · {row.get('timestamp')}")
m1, m2, m3, m4 = st.columns(4)
m1.metric("On disk", "yes" if row["on_disk"] else "no")
m2.metric("Review", row.get("review_status") or "pending")
m3.metric("QA", str(row.get("qa_approved")))
m4.metric("Continuation", row.get("continuation") or "none")

if row.get("stale"):
    st.error(
        "**Out of date** — a character reference used in this clip was redesigned after this render. "
        f"Characters: {', '.join(row.get('stale_characters') or []) or '—'}. "
        f"Reasons: {'; '.join(row.get('stale_reasons') or []) or '—'}. "
        "Regenerate to clear; CLI/pipeline will not treat this file as reusable “done.”"
    )

if not row["on_disk"]:
    st.warning("No video yet for this clip.")
    if st.button(
        f"▶ Generate clip {cn}",
        type="primary",
        key=f"gen_one_{sn}_{cn}",
        help="Background Grok generate (needs XAI_API_KEY). Use Cancel at top while running.",
        disabled=_gen_running,
    ):
        try:
            api.start_clip_gen_job(sn, int(cn), run_qa=True)
            st.success("Started background generation.")
            st.rerun()
        except Exception as e:
            st.error(str(e))

left, right = st.columns([1, 1])
with left:
    if row["on_disk"]:
        st.video(row["path"])
        st.caption(row["path"])
    else:
        st.caption("Video will appear here after generate.")

with right:
    st.markdown("**Dialogue**")
    st.write(row.get("dialogue") or "_none_")
    st.caption(f"speaker=`{row.get('speaker')}` · delivery=`{row.get('delivery')}`")

st.markdown("**Visual prompt**")
vp = st.text_area(
    "visual_prompt",
    value=row.get("visual_prompt") or "",
    height=140,
    key=f"vp_{sn}_{cn}",
)
neg = st.text_area(
    "negative_prompt",
    value=row.get("negative_prompt") or "",
    height=80,
    key=f"neg_{sn}_{cn}",
)
if st.button("Save prompts to blueprint"):
    try:
        old, new = api.update_clip_prompts(sn, int(cn), visual_prompt=vp, negative_prompt=neg)
        from review_app import edit_log

        edit_log.add_entry(
            "prompt_edit",
            user_note="Manual prompt edit from Scenes UI",
            scene=sn,
            clip=int(cn),
            action_taken="Updated visual/negative prompts",
            before=old,
            after=new,
            targets=["nickandme.clips.grok.json", "prompts/adaptation_v16.txt", "renderer"],
        )
        st.success("Saved blueprint.")
    except Exception as e:
        st.error(str(e))

st.divider()
st.subheader("Review actions")
from review_app import learning as learning_mod

feedback = st.text_area(
    "What's wrong? (optional — appended to prompt on regen)",
    placeholder="e.g. Nick faces Mrs. Engel window, camera behind him, not facing camera",
    key=f"fb_{sn}_{cn}",
)
# Heuristic suggestion only (do not force selectbox — typing would reset user choice)
_suggested = learning_mod.suggest_layer_from_note(feedback)
_layer_opts = list(learning_mod.FEEDBACK_LAYERS)
st.caption(
    f"Suggested layer from note: **`{_suggested}`** "
    f"({learning_mod.LAYER_LABELS.get(_suggested, '')})"
)
learning_layer = st.selectbox(
    "Feedback route (learning layer)",
    options=_layer_opts,
    format_func=lambda k: learning_mod.LAYER_LABELS.get(k, k),
    key=f"layer_{sn}_{cn}",
    help=(
        "clip = this take only · stage2 = replan shots · stage1 = re-bible then stage2 · "
        "verifier = detection rubric · engine = renderer code notes"
    ),
)
if st.button("Use suggested layer", key=f"use_sug_{sn}_{cn}"):
    st.session_state[f"layer_{sn}_{cn}"] = _suggested
    st.rerun()
if learning_layer in ("stage1", "stage2"):
    st.caption(
        f"Selecting **{learning_layer}** will mark this scene dirty for cascade replan "
        f"({'+'.join(learning_mod.dirty_keys_for_layer(learning_layer))})."
    )
apply_fb = st.checkbox("Append feedback to visual_prompt when regenerating", value=True)
run_qa = st.checkbox("Run QA after regen", value=True)
mark_dirty = st.checkbox(
    "Mark scene dirty when layer needs replan",
    value=True,
    help="Only stage1/stage2 layers create dirty flags",
)

b1, b2, b3, b4 = st.columns(4)
with b1:
    if st.button("✅ Pass", width="stretch"):
        api.pass_clip(sn, int(cn), feedback, learning_layer="clip")
        st.success("Passed")
        st.rerun()
with b2:
    if st.button("❌ Fail", width="stretch"):
        result = api.fail_clip(
            sn,
            int(cn),
            feedback,
            learning_layer=learning_layer,
            mark_dirty=mark_dirty,
        )
        msg = f"Failed · layer={result.get('learning_layer')}"
        if result.get("dirty_row"):
            msg += " · scene marked dirty"
        st.warning(msg)
        st.rerun()
with b3:
    if _gen_running:
        if st.button("Cancel", type="primary", width="stretch", key=f"cancel_regen_{sn}_{cn}"):
            try:
                api.cancel_gen_job()
                st.rerun()
            except Exception as e:
                st.error(str(e))
    elif st.button("♻️ Regen", type="primary", width="stretch"):
        try:
            fb = feedback if (apply_fb and feedback.strip()) else ""
            # Optional: mark dirty for stage1/stage2 layers (same as before)
            if mark_dirty and learning_layer in ("stage1", "stage2") and fb:
                try:
                    api.log_clip_feedback(
                        sn,
                        int(cn),
                        fb,
                        learning_layer=learning_layer,
                        mark_dirty=True,
                    )
                except Exception:
                    pass
            api.start_clip_gen_job(
                sn,
                int(cn),
                feedback=fb,
                apply_to_prompt=bool(fb),
                run_qa=run_qa,
            )
            st.success("Started background regen. Use **Cancel** while running.")
            st.rerun()
        except Exception as e:
            st.error(str(e))
with b4:
    if st.button("Log note only", width="stretch"):
        result = api.log_clip_feedback(
            sn,
            int(cn),
            feedback or "Note",
            learning_layer=learning_layer,
            mark_dirty=mark_dirty,
            before=row.get("visual_prompt") or "",
        )
        st.success(
            f"Logged `{result['entry']['id']}` · layer={result.get('learning_layer')}"
            + (" · dirty" if result.get("dirty_row") else "")
        )
        st.rerun()

# Neighbor navigation
nav_l, nav_r = st.columns(2)
nums = [c["clip_number"] for c in clips]
idx = nums.index(int(cn)) if int(cn) in nums else 0
with nav_l:
    if idx > 0 and st.button(f"← Clip {nums[idx - 1]}"):
        st.session_state.clip_num = nums[idx - 1]
        st.rerun()
with nav_r:
    if idx < len(nums) - 1 and st.button(f"Clip {nums[idx + 1]} →"):
        st.session_state.clip_num = nums[idx + 1]
        st.rerun()
