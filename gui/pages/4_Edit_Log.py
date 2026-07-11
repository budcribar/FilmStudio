"""Edit / feedback log — feed learnings back into prompts + engine notes."""
from __future__ import annotations

import streamlit as st

from review_app import edit_log
from review_app import learning as learning_mod

st.set_page_config(page_title="Edit Log", page_icon="📝", layout="wide")
st.title("📝 Edit Log")
st.caption(
    "Route feedback by **learning layer**: clip · stage2 · stage1 · verifier · engine. "
    "Apply entries to LEARNINGS.md, layer prompts, shared rules, or renderer notes."
)

data = edit_log.load_log()
entries = data.get("entries") or []

with st.expander("➕ Add manual learning", expanded=False):
    note = st.text_area("What did we learn?", key="manual_note")
    suggested = st.text_area(
        "Suggested rule (optional — auto-drafted if empty)",
        key="manual_rule",
    )
    layer = st.selectbox(
        "Learning layer",
        options=list(learning_mod.FEEDBACK_LAYERS),
        format_func=lambda k: learning_mod.LAYER_LABELS.get(k, k),
        key="manual_layer",
    )
    sc = st.number_input("Scene (optional)", min_value=0, value=0)
    cl = st.number_input("Clip (optional)", min_value=0, value=0)
    if st.button("Add to log"):
        if not note.strip():
            st.error("Note required")
        else:
            entry = edit_log.add_entry(
                "manual_learning",
                user_note=note,
                scene=int(sc) or None,
                clip=int(cl) or None,
                suggested_rule=suggested.strip(),
                action_taken="Manual log entry",
                learning_layer=layer,
            )
            st.success(f"Added {entry['id']} · layer={entry.get('learning_layer')}")
            st.rerun()

c1, c2, c3, c4 = st.columns(4)
with c1:
    type_filter = st.selectbox(
        "Type filter",
        ["(all)"]
        + sorted({e.get("type") or "" for e in entries if e.get("type")}),
    )
with c2:
    layer_filter = st.selectbox(
        "Layer filter",
        ["(all)"] + list(learning_mod.FEEDBACK_LAYERS),
    )
with c3:
    scene_filter = st.number_input("Scene filter (0=all)", min_value=0, value=0)
with c4:
    unapplied = st.checkbox("Unapplied only", value=False)

filtered = edit_log.filter_entries(
    entries,
    entry_type=None if type_filter == "(all)" else type_filter,
    scene=int(scene_filter) if int(scene_filter) > 0 else None,
    unapplied_only=unapplied,
    learning_layer=None if layer_filter == "(all)" else layer_filter,
)

st.write(f"**{len(filtered)}** entries (of {len(entries)} total)")
st.markdown(
    f"- Log file: `{edit_log.LOG_PATH}`  \n"
    f"- Learnings MD: `{edit_log.LEARNINGS_MD}`  \n"
    f"- Script notes: `{edit_log.SCRIPT_NOTES_MD}`  \n"
    f"- Adaptation prompt: `{edit_log.ADAPTATION_PROMPT}`  \n"
    f"- Stage1 / Stage2 / Verifier / Shared: `prompts/`"
)

if not filtered:
    st.info("No log entries yet. Pass/Fail/Regen on the Scenes page, or add a manual learning.")
    st.stop()

for e in filtered:
    applied = e.get("applied") or {}
    flags = "".join(
        [
            "B" if applied.get("blueprint") else "·",
            "A" if applied.get("adaptation_prompt") else "·",
            "S" if applied.get("script_notes") else "·",
            "L" if applied.get("learnings_md") else "·",
            "P" if applied.get("layer_prompts") else "·",
            "R" if applied.get("shared_rules") else "·",
            "D" if applied.get("dirty_marked") else "·",
        ]
    )
    layer = e.get("learning_layer") or "clip"
    title = (
        f"`{e.get('id')}` · {e.get('ts')} · **{e.get('type')}** · "
        f"**{layer}** · S{e.get('scene')}C{e.get('clip')} · applied[{flags}]"
    )
    with st.expander(title, expanded=False):
        st.markdown(f"**Layer:** `{layer}` — {learning_mod.LAYER_LABELS.get(layer, '')}")
        st.markdown(f"**User note:** {e.get('user_note')}")
        st.markdown(f"**Action:** {e.get('action_taken')}")
        if e.get("character"):
            st.markdown(f"**Character:** `{e.get('character')}`")
        st.markdown(f"**Suggested rule:** {e.get('suggested_rule')}")
        st.caption(f"Targets: {', '.join(e.get('targets') or [])}")
        if e.get("before") or e.get("after"):
            bc1, bc2 = st.columns(2)
            with bc1:
                st.text_area("Before", e.get("before") or "", height=120, key=f"b_{e['id']}")
            with bc2:
                st.text_area("After", e.get("after") or "", height=120, key=f"a_{e['id']}")

        new_rule = st.text_area(
            "Edit suggested rule before applying",
            value=e.get("suggested_rule") or "",
            key=f"rule_{e['id']}",
        )
        if new_rule != (e.get("suggested_rule") or ""):
            if st.button("Update suggested rule", key=f"upd_{e['id']}"):
                edit_log.update_entry(e["id"], suggested_rule=new_rule)
                st.rerun()

        x1, x2, x3, x4, x5, x6 = st.columns(6)
        with x1:
            if st.button("→ LEARNINGS.md", key=f"learn_{e['id']}"):
                try:
                    path = edit_log.append_learnings_md({**e, "suggested_rule": new_rule})
                    edit_log.mark_applied(e["id"], "learnings_md")
                    st.success(f"Appended to {path}")
                except Exception as ex:
                    st.error(str(ex))
        with x2:
            if st.button("→ Layer prompts", key=f"layerp_{e['id']}"):
                try:
                    paths = learning_mod.apply_entry_to_layer_prompts(
                        {**e, "suggested_rule": new_rule, "learning_layer": layer}
                    )
                    if not paths:
                        st.info(
                            f"Layer `{layer}` has no auto prompt target "
                            "(clip/engine use other actions)."
                        )
                    else:
                        edit_log.mark_applied(e["id"], "layer_prompts")
                        st.success("Wrote: " + ", ".join(paths))
                except Exception as ex:
                    st.error(str(ex))
        with x3:
            if st.button("→ Shared rules", key=f"shared_{e['id']}"):
                try:
                    path = learning_mod.append_prompt_learning(
                        "shared", {**e, "suggested_rule": new_rule}
                    )
                    edit_log.mark_applied(e["id"], "shared_rules")
                    st.success(f"Appended to {path}")
                except Exception as ex:
                    st.error(str(ex))
        with x4:
            if st.button("→ Adaptation V16", key=f"v16_{e['id']}"):
                try:
                    path = edit_log.append_adaptation_prompt({**e, "suggested_rule": new_rule})
                    edit_log.mark_applied(e["id"], "adaptation_prompt")
                    st.success(f"Appended to {path}")
                except Exception as ex:
                    st.error(str(ex))
        with x5:
            if st.button("→ Script notes", key=f"scr_{e['id']}"):
                try:
                    path = edit_log.append_script_notes({**e, "suggested_rule": new_rule})
                    edit_log.mark_applied(e["id"], "script_notes")
                    st.success(f"Appended to {path}")
                except Exception as ex:
                    st.error(str(ex))
        with x6:
            if st.button("Mark blueprint applied", key=f"bp_{e['id']}"):
                edit_log.mark_applied(e["id"], "blueprint")
                st.success("Marked")
                st.rerun()

        if st.button("Apply recommended for layer", key=f"all_{e['id']}"):
            errors = []
            try:
                edit_log.append_learnings_md({**e, "suggested_rule": new_rule})
                edit_log.mark_applied(e["id"], "learnings_md")
            except Exception as ex:
                errors.append(str(ex))
            try:
                paths = learning_mod.apply_entry_to_layer_prompts(
                    {**e, "suggested_rule": new_rule, "learning_layer": layer}
                )
                if paths:
                    edit_log.mark_applied(e["id"], "layer_prompts")
            except Exception as ex:
                errors.append(str(ex))
            if layer == "engine":
                try:
                    edit_log.append_script_notes({**e, "suggested_rule": new_rule})
                    edit_log.mark_applied(e["id"], "script_notes")
                except Exception as ex:
                    errors.append(str(ex))
            if layer == "verifier":
                try:
                    learning_mod.append_prompt_learning(
                        "shared", {**e, "suggested_rule": new_rule}
                    )
                    edit_log.mark_applied(e["id"], "shared_rules")
                except Exception as ex:
                    errors.append(str(ex))
            edit_log.mark_applied(e["id"], "blueprint")
            if errors:
                st.error("; ".join(errors))
            else:
                st.success("Applied recommended targets for this layer")
            st.rerun()

st.divider()
st.subheader("How feedback flows (Phase A)")
st.markdown(
    """
1. **Scenes → feedback layer** — choose clip / stage2 / stage1 / verifier / engine.
2. **stage1 / stage2** — marks scene **dirty** in `pipeline_state.json` and shows cascade checklist.
3. **Edit Log → Layer prompts** — appends durable rules into `stage1_scene_bible.txt`,
   `stage2_shot_planner.txt`, or `verifier_clip.txt` (GUI LEARNINGS markers).
4. **Shared rules** — optional promote to `prompts/shared_rules.txt` for all stages.
5. **Engine** — SCRIPT_NOTES only (do not auto-edit `renderer/`).
6. **Phase A does not auto-run** Stage 1/2 LLMs — replan is operator-driven, then clear dirty.
"""
)
