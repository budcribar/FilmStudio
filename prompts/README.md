# Prompts

Shared operator prompts, schemas, and tiny examples for book → film adaptation.
Paths are relative to the **workspace root** (repo root with `renderer/`, `gui/`, `cli/`).

## Naming

`snake_case` + role + optional version:

| File | Role |
|------|------|
| `adaptation_v16.txt` | Legacy full-film adaptation rules (optional GUI append) |
| `shared_rules.txt` | Rules Stage 1 + Stage 2 + verifier must all respect |
| `stage1_scene_bible.txt` | Stage 1: book → scene bible (+ `location_seed_tokens`, scene `location_ids[]`) |
| `stage1_scene_bible.schema.json` | Stage 1 JSON Schema |
| `stage2_shot_planner.txt` | Stage 2: scene bible → clip plan (+ clip `location_id`, place locks) |
| `verifier_clip.txt` | Clip QA verifier (routing hints for learning layers) |
| `compare_json_to_book.txt` | Fidelity audit against book text |
| `examples/scene_bible_minimal.json` | Minimal Stage 1 sample |
| `examples/clip_plan_minimal.json` | Minimal Stage 2 sample |

## Learning loop (Phase A)

Feedback is **routed** by layer — not sprayed into every prompt:

| Layer | Effect |
|-------|--------|
| `clip` | This take / visual_prompt |
| `stage2` | Stage 2 prompt + scene **dirty** for replan |
| `stage1` | Stage 1 prompt + dirty **stage1→stage2** |
| `verifier` | `verifier_clip.txt` (+ optional shared rules) |
| `engine` | `review_feedback/SCRIPT_NOTES.md` only |

Dirty flags live in project `pipeline_state.json` → `scene_dirty`.  
Phase A does **not** auto-run Stage 1/2 LLMs; UI shows a cascade checklist.

## Usage

- **Scenes** → choose feedback layer on Fail / Regen / Log.
- **Edit Log** → apply to layer prompts, shared rules, LEARNINGS, or script notes.
- **Scripts:** `scripts/two_stage_adaptation/` — see `docs/two_stage_adaptation/README.md`.

