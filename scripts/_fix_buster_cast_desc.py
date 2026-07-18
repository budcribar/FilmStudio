#!/usr/bin/env python3
"""One-shot fix for Buster cast description (nickname leak)."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = ROOT / "projects" / "Buster2" / "source" / "cast_seeds.json"
if not p.is_file():
    raise SystemExit(f"missing {p}")

root = json.loads(p.read_text(encoding="utf-8"))
seeds = root["character_seed_tokens"]
b = seeds["Character_Buster"]
b["description"] = (
    "Small black-and-white dog, short coat, floppy ears, bright eyes, "
    "soft rounded head shape, compact bouncy build; later wears snug "
    "black-and-white pajama top and bottoms."
)
b["visual_lock"] = (
    "Always black-and-white coat, floppy ears, bright eyes, compact dog proportions. "
    "Once pajamas are on, black-and-white pajama top and bottoms stay on through bed and dream."
)
text = json.dumps(root, indent=2, ensure_ascii=False) + "\n"
p.write_text(text, encoding="utf-8")
(p.parent / "cast.json").write_text(text, encoding="utf-8")
print(b["description"])
print(b["visual_lock"])
