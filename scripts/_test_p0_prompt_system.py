#!/usr/bin/env python3
"""P0.1–P0.4 regression tests: wardrobe purity, clip cast, priority packer, fingerprint."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from scripts.two_stage_adaptation.stage2_plan_grok import (  # noqa: E402
    _always_on_wardrobe_from_seed,
    _build_visual_prompt,
    _clip_cast_tokens,
    _coerce_wardrobe_phrases,
    _looks_like_wardrobe_item,
    plan_scene,
    stage1_content_fingerprint,
)


def main() -> None:
    # P0.1 — identity text is not wardrobe
    bad = "small black-and-white dog only—never brown, tan, gray, or solid-color redesign"
    assert not _looks_like_wardrobe_item(bad), bad
    assert _looks_like_wardrobe_item("signature silly noodle-head hat")
    assert _looks_like_wardrobe_item("black-and-white dog pajamas")
    assert _coerce_wardrobe_phrases([bad, "signature silly noodle-head hat"]) == [
        "signature silly noodle-head hat"
    ]
    assert _always_on_wardrobe_from_seed(
        {
            "wardrobe_always": [],
            "visual_lock": "Always small black-and-white dog only—never brown redesign. Always wears signature silly noodle-head hat.",
            "description": bad,
        }
    ) == []  # structured empty → no harvest
    assert _always_on_wardrobe_from_seed(
        {"wardrobe_always": ["signature silly noodle-head hat"], "visual_lock": bad}
    ) == ["signature silly noodle-head hat"]
    print("P0.1 wardrobe purity OK")

    # P0.3 — clip-local cast
    scene = {
        "characters_on_screen": [
            "Character_Buster",
            "Character_Mom",
            "Character_Daddy",
        ]
    }
    dream = {
        "primary_subject": "Character_Buster",
        "visual_event": "Character_Buster chases a bunny in a dream yard",
        "action_class": "montage",
        "environment_mode": "dream",
    }
    cast_d = _clip_cast_tokens(scene, dream)
    assert cast_d == ["Character_Buster"], cast_d
    two_shot = {
        "primary_subject": "Character_Mom",
        "visual_event": "Character_Mom talks to Character_Buster on the sofa",
        "action_class": "dialogue",
    }
    cast_t = _clip_cast_tokens(scene, two_shot)
    assert "Character_Mom" in cast_t and "Character_Buster" in cast_t
    assert "Character_Daddy" not in cast_t
    explicit = {
        "primary_subject": "Character_Buster",
        "characters_on_screen": ["Character_Buster", "Character_Mom"],
        "visual_event": "Climbing stairs",
        "action_class": "big_action",
    }
    cast_e = _clip_cast_tokens(scene, explicit)
    assert cast_e == ["Character_Buster", "Character_Mom"]
    print("P0.3 clip cast OK")

    # P0.4 — packer keeps wardrobe/hat under soft limit
    seeds = {
        "Character_Buster": {
            "wardrobe_always": ["signature silly noodle-head hat"],
            "description": "black-and-white dog",
            "visual_lock": "Always wears signature silly noodle-head hat",
        }
    }
    planned = plan_scene(
        {
            "scene_number": 1,
            "setting": "Living room evening",
            "characters_on_screen": ["Character_Buster"],
            "wardrobe_by_character": {
                "Character_Buster": ["signature silly noodle-head hat"]
            },
            "story_beats": [
                {
                    "beat_id": "b1",
                    "visual_event": "Character_Buster hops and bounces around the living room like a frog",
                    "primary_subject": "Character_Buster",
                    "action_class": "big_action",
                    "continuity": "new_setup",
                    "delivery": "voiceover_internal",
                    "speaker": "Character_Narrator",
                    "dialogue": "He jumps around like a frog",
                    "location_id": "Loc_House",
                }
            ],
            "location_ids": ["Loc_House"],
            "duration_target_seconds": 10,
        },
        character_seeds=seeds,
        location_seeds={
            "Loc_House": {
                "visual_lock": "Warm cream walls, soft evening lamps, front door, sofa, treat dish, carpet"
            }
        },
        prompt_soft=700,
    )
    vp = planned["veo_clips"][0]["visual_prompt"]
    print("packed vp:", vp[:220], "... len", len(vp))
    assert "hat" in vp.lower() or "wearing" in vp.lower(), vp
    assert "never brown" not in vp.lower(), vp
    assert len(vp) <= 750
    print("P0.4 packer OK")

    # Fingerprint stable + sensitive
    s1a = {
        "global_production_variables": {
            "character_seed_tokens": {
                "Character_A": {"wardrobe_always": ["red hat"], "description": "x"}
            }
        },
        "scenes": [
            {
                "scene_number": 1,
                "story_beats": [
                    {"beat_id": "b1", "visual_event": "walks", "dialogue": "hi"}
                ],
            }
        ],
    }
    s1b = json_deepcopy = __import__("copy").deepcopy(s1a)
    s1b["scenes"][0]["story_beats"][0]["visual_event"] = "runs"
    fp1 = stage1_content_fingerprint(s1a)
    fp2 = stage1_content_fingerprint(s1a)
    fp3 = stage1_content_fingerprint(s1b)
    assert fp1 == fp2 and fp1 != fp3
    print("P0.2 fingerprint OK")
    print("PASS")


if __name__ == "__main__":
    main()
