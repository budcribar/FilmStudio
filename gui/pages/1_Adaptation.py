"""Stage 1 adaptation tools — PDF/book → scenes bible from the UI."""
from __future__ import annotations

import os
from pathlib import Path

import streamlit as st

from review_app import pipeline_api as api

st.title("📖 Adaptation")
st.caption("Import book → prepare → Stage 1 scene bible.")


def _apply_prepare_defaults(prep: dict) -> None:
    if prep.get("suggested_total_minutes"):
        st.session_state.stage1_total_minutes = max(
            3, min(180, int(prep["suggested_total_minutes"]))
        )
    if prep.get("suggested_chunk_pages"):
        st.session_state.stage1_chunk_pages = max(
            5, min(30, int(prep["suggested_chunk_pages"]))
        )


def _show_prepare_result(prep: dict) -> None:
    _apply_prepare_defaults(prep)
    ready = prep.get("ready_for_stage1")
    mins = prep.get("suggested_total_minutes")
    if ready:
        st.success(
            f"Book ready · suggested **{mins} min** film"
            + (f" · {prep.get('text_words')} words" if prep.get("text_words") else "")
        )
    elif prep.get("needs_user"):
        st.warning(prep.get("message") or "Book needs attention before Stage 1.")
        if prep.get("user_hint"):
            st.caption(prep["user_hint"])
    else:
        st.info(prep.get("message") or "Prepare finished.")


def _kind_label(kind: str | None) -> str:
    return {
        "picture_book": "picture book",
        "short": "short story",
        "novel": "novel",
    }.get(str(kind or ""), str(kind or "book"))


# ---- Status ----
try:
    status = api.stage1_status()
    img_status = api.book_images_status()
    book_meta = api.book_source_meta()
except Exception as e:
    st.error(str(e))
    st.stop()

paths = status.get("paths") or {}
pdf_ok = paths.get("pdf_exists") == "True"
book_ok = paths.get("book_exists") == "True"
key_ok = bool((os.environ.get("XAI_API_KEY") or "").strip())

_sc = status.get("scene_count") if status.get("present") else None
_img = img_status.get("count") or 0
_bits = []
if _sc is not None:
    _bits.append(f"{_sc} scenes")
    if status.get("beat_count") is not None:
        _bits.append(f"{status.get('beat_count')} beats")
    if status.get("locations") is not None:
        _bits.append(f"{status.get('locations')} locations")
    if status.get("characters") is not None:
        _bits.append(f"{status.get('characters')} characters")
if _img:
    _bits.append(f"{_img} book images")
if _bits:
    st.caption(" · ".join(_bits))

if status.get("present") and (status.get("scene_count") or 0) > 0:
    runtime_sec = status.get("runtime_sec")
    runtime_txt = (
        f" · ~{int(runtime_sec) // 60} min runtime"
        if runtime_sec
        else ""
    )
    mtime = status.get("mtime") or ""
    st.success(
        f"**Adaptation complete** — Stage 1 on disk{runtime_txt}"
        + (f" · updated {mtime}" if mtime else "")
        + ". **Next:** Configuration (providers / duration), then Characters."
    )
elif status.get("present"):
    st.info("Stage 1 file exists but has no scenes yet — re-run Stage 1.")
else:
    st.caption("No Stage 1 scene bible yet.")

# Book readiness (short)
if book_meta.get("present"):
    q = book_meta.get("text_quality") or "unknown"
    ready = book_meta.get("ready_for_stage1") or q == "good"
    kind = _kind_label(book_meta.get("book_kind"))
    mins = book_meta.get("suggested_total_minutes")
    if ready:
        st.caption(
            f"Book ready · {kind}"
            + (f" · ~{mins} min suggested" if mins else "")
            + (f" · {book_meta.get('text_words') or 0} words" if book_meta.get("text_words") else "")
        )
    elif q in ("poor", "empty"):
        st.warning(
            "Book text looks weak. Use **Prepare book** "
            "(needs API key for vision cleanup on picture books)."
        )
    elif q == "sparse":
        st.caption(
            f"Book text is thin ({book_meta.get('text_words') or 0} words) — "
            f"OK for a picture book; ~{mins} min suggested."
        )
elif not pdf_ok and not book_ok:
    st.warning("No book yet — upload a PDF or TXT below.")

if not key_ok:
    st.error(
        "**XAI_API_KEY** is not set. Needed for Stage 1 (and vision cleanup).\n\n"
        "Set it in the shell before starting Streamlit, then restart the app."
    )

# ---- 1) Import / prepare ----
st.divider()
st.subheader("1) Book")

uploaded = st.file_uploader(
    "Upload PDF or TXT",
    type=["pdf", "txt"],
    accept_multiple_files=False,
    key="adapt_book_upload",
)

with st.expander("Advanced", expanded=False):
    st.selectbox(
        "PDF page stills",
        options=["cover,sparse", "cover", "sparse", "cover,sparse,all", "none"],
        index=0,
        key="adapt_render_mode",
    )
    st.checkbox("Force re-extract", value=True, key="adapt_force_extract")
    st.checkbox("Force Grok vision", value=False, key="adapt_force_vision")
    st.checkbox(
        "Auto vision when text is weak",
        value=True,
        key="adapt_auto_vision",
    )
    if img_status.get("present") and img_status.get("images"):
        st.caption(f"{img_status.get('count') or 0} page images extracted")
        cols = st.columns(4)
        proj = Path(paths.get("project") or ".")
        for i, im in enumerate(img_status["images"][:12]):
            rel = im.get("path") or ""
            fp = proj / "source" / rel if not Path(rel).is_absolute() else Path(rel)
            if not fp.is_file():
                fp = proj / rel
            with cols[i % 4]:
                if fp.is_file():
                    st.image(str(fp), caption=f"p{im.get('page')}", width="stretch")

render_mode = st.session_state.get("adapt_render_mode", "cover,sparse")
force_extract = bool(st.session_state.get("adapt_force_extract", True))
force_vision = bool(st.session_state.get("adapt_force_vision", False))
auto_vision = bool(st.session_state.get("adapt_auto_vision", True))

c_imp, c_prep = st.columns(2)
with c_imp:
    do_import = st.button(
        "Import & prepare",
        type="primary",
        width="stretch",
        disabled=uploaded is None,
    )
with c_prep:
    do_prepare = st.button(
        "Re-prepare existing book",
        width="stretch",
        disabled=not (pdf_ok or book_ok),
    )

if do_import and uploaded is not None:
    prog = st.progress(0.0, text="Importing…")
    log_box = st.empty()
    lines: list[str] = []

    def on_prep(ev: dict) -> None:
        event = ev.get("event") or ""
        total = max(1, int(ev.get("total") or 1))
        chunk = int(ev.get("chunk") or 0)
        frac = 1.0 if event == "done" else min(0.95, chunk / total if total else 0.2)
        msg = ev.get("message") or event
        prog.progress(frac, text=msg)
        lines.append(f"[{event}] {msg}")
        log_box.code("\n".join(lines[-20:]), language="text")

    try:
        result = api.import_book_upload(
            filename=uploaded.name,
            data=uploaded.getvalue(),
            extract_pdf=True,
            render_pages=render_mode,
            force=force_extract,
            auto_prepare=True,
            progress_cb=on_prep,
        )
        prog.progress(1.0, text="Done")
        if result.get("prepare"):
            _show_prepare_result(result["prepare"])
        else:
            st.success("Imported.")
            if result.get("extract"):
                _apply_prepare_defaults(result["extract"])
        st.rerun()
    except Exception as e:
        st.error(str(e))
        if lines:
            log_box.code("\n".join(lines[-20:]), language="text")

if do_prepare:
    prog = st.progress(0.0, text="Preparing…")
    log_box = st.empty()
    lines = []

    def on_prep2(ev: dict) -> None:
        event = ev.get("event") or ""
        total = max(1, int(ev.get("total") or 1))
        chunk = int(ev.get("chunk") or 0)
        if event in ("page_done", "page_start"):
            frac = min(0.95, 0.2 + 0.7 * (chunk / max(total, 1)))
        elif event == "done":
            frac = 1.0
        else:
            frac = min(0.9, chunk / max(total, 1) * 0.5 + 0.1)
        msg = ev.get("message") or event
        prog.progress(frac, text=msg)
        lines.append(f"[{event}] {msg}")
        log_box.code("\n".join(lines[-30:]), language="text")

    try:
        prep = api.prepare_book_source(
            force_extract=force_extract,
            force_vision=force_vision,
            render_pages=render_mode,
            auto_vision=auto_vision,
            vision_model=os.environ.get("STAGE1_MODEL", "grok-4.5"),
            progress_cb=on_prep2,
        )
        prog.progress(1.0, text="Done")
        _show_prepare_result(prep)
        st.rerun()
    except Exception as e:
        st.error(str(e))
        if lines:
            log_box.code("\n".join(lines[-30:]), language="text")

# ---- 2) Stage 1 ----
st.divider()
st.subheader("2) Stage 1")

if "stage1_chunk_pages" not in st.session_state:
    st.session_state.stage1_chunk_pages = max(
        5, min(30, int(book_meta.get("suggested_chunk_pages") or 10))
    )
if "stage1_total_minutes" not in st.session_state:
    st.session_state.stage1_total_minutes = max(
        3, min(180, int(book_meta.get("suggested_total_minutes") or 90))
    )
if "stage1_model" not in st.session_state:
    st.session_state.stage1_model = os.environ.get("STAGE1_MODEL", "grok-4.5")
if "stage1_resume" not in st.session_state:
    st.session_state.stage1_resume = False
if "stage1_max_chunks" not in st.session_state:
    st.session_state.stage1_max_chunks = 0

with st.expander("Stage 1 options", expanded=False):
    st.number_input(
        "Pages per chunk",
        min_value=5,
        max_value=30,
        key="stage1_chunk_pages",
    )
    st.number_input(
        "Target runtime (minutes)",
        min_value=3,
        max_value=180,
        key="stage1_total_minutes",
    )
    st.text_input("Model", key="stage1_model")
    st.checkbox("Resume / merge into existing scenes", key="stage1_resume")
    st.number_input(
        "Max chunks (0 = all)",
        min_value=0,
        max_value=50,
        key="stage1_max_chunks",
    )

chunk_pages = int(st.session_state.get("stage1_chunk_pages") or 10)
total_minutes = int(st.session_state.get("stage1_total_minutes") or 90)
model = str(st.session_state.get("stage1_model") or "grok-4.5")
resume = bool(st.session_state.get("stage1_resume", False))
max_chunks = int(st.session_state.get("stage1_max_chunks") or 0)

st.caption(
    f"Will run ~**{total_minutes} min** target · "
    f"**{chunk_pages}** pages/chunk"
    + (" · resume" if resume else "")
)

can_run = key_ok and (book_ok or pdf_ok)
run = st.button(
    "Run Stage 1",
    type="primary",
    disabled=not can_run,
    width="stretch",
)

if run:
    prog = st.progress(0.0, text="Starting Stage 1…")
    log_box = st.empty()
    lines = []

    def on_progress(ev: dict) -> None:
        event = ev.get("event") or ""
        total = max(1, int(ev.get("total") or 1))
        chunk = int(ev.get("chunk") or 0)
        if event == "chunk_done":
            frac = min(1.0, chunk / total)
        elif event == "done":
            frac = 1.0
        elif event in ("normalize", "verify"):
            frac = min(0.99, max(chunk / total, 0.9))
        elif event == "chunk_start":
            frac = min(0.99, max(0.0, (chunk - 1) / total))
        else:
            frac = min(0.05, chunk / max(total, 1))
        msg = ev.get("message") or event
        if ev.get("scenes") is not None:
            msg = f"{msg} · scenes so far: {ev.get('scenes')}"
        prog.progress(frac, text=msg)
        lines.append(f"[{event}] {msg}")
        log_box.code("\n".join(lines[-40:]), language="text")

    try:
        with st.spinner("Stage 1 running…"):
            summary = api.run_stage1_from_book(
                chunk_pages=chunk_pages,
                total_minutes=total_minutes,
                model=model.strip() or "grok-4.5",
                resume=resume,
                max_chunks=max_chunks,
                extract_pdf_if_needed=True,
                progress_cb=on_progress,
            )
        prog.progress(1.0, text="Complete")
        if summary.get("ok"):
            st.success(
                f"Stage 1 done: **{summary.get('scenes')}** scenes · "
                f"{summary.get('locations')} locations · "
                f"~{int(summary.get('runtime_sec') or 0) // 60} min target"
            )
        else:
            st.warning("Stage 1 wrote with remaining normalize issues.")
            if summary.get("hard_errors"):
                st.code("\n".join(summary["hard_errors"][:30]))
        st.rerun()
    except Exception as e:
        st.error(str(e))
        if lines:
            log_box.code("\n".join(lines[-40:]), language="text")
