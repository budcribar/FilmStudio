# Nick and Me

Film generation pipeline (Grok Imagine primary) plus a Streamlit review console.

## Pipeline (CLI)

```bash
python generation_script.py
```

Scene / clip selectors support e.g. `2 clip 3 regen`.

Requires `XAI_API_KEY`, `ffmpeg` on PATH (or `FFMPEG_PATH`), and `nickandme.json`.

## Streamlit review app

Use a **project virtualenv** (do not `pip install` into system Python — Ubuntu/WSL blocks that with `externally-managed-environment`).

A `.venv` is already set up in this repo. In **WSL**:

```bash
cd /mnt/c/Users/budcr/source/repos/NickAndMe
source .venv/bin/activate          # prompt shows (.venv)
pip install -r requirements-review.txt   # only if packages missing
streamlit run streamlit_app.py
```

**Windows PowerShell** (separate venv if you prefer native Windows Python):

```powershell
cd C:\Users\budcr\source\repos\NickAndMe
py -3 -m venv .venv-win
.\.venv-win\Scripts\Activate.ps1
pip install -r requirements-review.txt
streamlit run streamlit_app.py
```

Deactivate later with: `deactivate`

Opens a local browser UI with:

| Page | Purpose |
|------|---------|
| **Home** | Status, WIP movie |
| **Configuration** | Edit `pipeline_config.json` |
| **Characters** | Locked refs, 3-variant pick, cascade regen |
| **Scenes** | Scene list → clips → Pass / Fail / Regen / edit prompts |
| **Edit Log** | Feedback diary; apply to learnings, V16, script notes |

### Edit feedback loop

1. On **Scenes**, describe what’s wrong and **Regen** (appends into that clip’s `visual_prompt` in `nickandme.json`).
2. Open **Edit Log** — each action is stored in `edit_feedback_log.json`.
3. Apply an entry to:
   - `review_feedback/LEARNINGS.md`
   - `ClaudeAdaptationPromptV16.txt` (GUI learnings section)
   - `review_feedback/SCRIPT_NOTES.md` (for intentional `generation_script.py` changes)

## Key files

- `generation_script.py` — engine (V9.5+)
- `nickandme.json` — production blueprint
- `pipeline_config.json` / `pipeline_state.json`
- `ClaudeAdaptationPromptV16.txt` — adaptation rules for full blueprint regen
- `streamlit_app.py` + `pages/` + `review_app/` — review UI
