#!/usr/bin/env python3
"""Smoke tests for Stage 1 structured wardrobe + Stage 2 sticky restatement."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from scripts.two_stage_adaptation.stage2_plan_grok import (  # noqa: E402
    _always_on_wardrobe_from_seed,
    _init_scene_wardrobe_state,
    _update_wardrobe_from_beat,
    plan_scene,
)


def main() -> None:
    # Structured always-on beats free-text heuristics
    seed = {
        "wardrobe_always": ["glowing alien badge", "signature silly noodle-head hat"],
        "visual_lock": "Always wearing something else entirely",
        "description": "creature",
    }
    always = _always_on_wardrobe_from_seed(seed)
    assert any("alien badge" in x.lower() for x in always), always
    assert any("hat" in x.lower() for x in always), always
    print("structured always_on:", always)

    # Scene structured map + beat put_on (no keyword enum needed for alien badge)
    cast = ["Character_Hero"]
    seeds = {
        "Character_Hero": {
            "wardrobe_always": ["blue baseball cap"],
            "visual_lock": "Always wears blue baseball cap",
            "description": "kid",
        }
    }
    scene = {
        "scene_number": 1,
        "setting": "Park",
        "characters_on_screen": cast,
        "wardrobe_by_character": {
            "Character_Hero": ["blue baseball cap"],
        },
        "story_beats": [
            {
                "beat_id": "b1",
                "visual_event": "Character_Hero puts on a yellow raincoat",
                "primary_subject": "Character_Hero",
                "action_class": "small_motion",
                "continuity": "new_setup",
                "wardrobe_put_on": ["yellow raincoat"],
                "delivery": "none",
                "speaker": "none",
                "dialogue": "",
                "location_id": "Loc_P",
            },
            {
                "beat_id": "b2",
                "visual_event": "Character_Hero runs through puddles",
                "primary_subject": "Character_Hero",
                "action_class": "big_action",
                "continuity": "continuous_from_previous_beat",
                "delivery": "none",
                "speaker": "none",
                "dialogue": "",
                "location_id": "Loc_P",
            },
        ],
        "location_ids": ["Loc_P"],
        "primary_location_id": "Loc_P",
        "duration_target_seconds": 16,
    }
    st = _init_scene_wardrobe_state(cast, seeds, scene=scene)
    assert any("cap" in x.lower() for x in st["Character_Hero"]), st
    st = _update_wardrobe_from_beat(st, scene["story_beats"][0], scene, cast=cast)
    assert any("raincoat" in x.lower() for x in st["Character_Hero"]), st
    print("after put_on:", st)

    planned = plan_scene(
        scene,
        character_seeds=seeds,
        location_seeds={"Loc_P": {"visual_lock": "Rainy park"}},
    )
    c2 = planned["veo_clips"][1]["visual_prompt"].lower()
    assert "raincoat" in c2, planned["veo_clips"][1]["visual_prompt"]
    assert "cap" in c2 or "hat" in c2, planned["veo_clips"][1]["visual_prompt"]
    print("C2 prompt has raincoat+cap")

    # S5-style structured PJs
    buster_seeds = {
        "Character_Buster": {
            "wardrobe_always": ["signature silly noodle-head hat"],
            "visual_lock": "Always wearing signature silly noodle-head hat",
            "description": "black-and-white dog",
        },
        "Character_Mom": {
            "wardrobe_always": ["soft blue cardigan", "cream pants"],
            "visual_lock": "Always soft blue cardigan",
            "description": "mom",
        },
    }
    s5 = {
        "scene_number": 5,
        "setting": "Night stairs",
        "characters_on_screen": ["Character_Buster", "Character_Mom"],
        "wardrobe_by_character": {
            "Character_Buster": [
                "signature silly noodle-head hat",
                "black-and-white dog pajamas",
            ],
            "Character_Mom": ["soft blue cardigan", "cream pants"],
        },
        "wardrobe_notes": "Buster in pajamas for stairs",
        "story_beats": [
            {
                "beat_id": "b16",
                "visual_event": "Character_Buster now wears black-and-white dog pajamas",
                "primary_subject": "Character_Buster",
                "action_class": "small_motion",
                "continuity": "new_setup",
                "wardrobe_put_on": ["black-and-white dog pajamas"],
                "delivery": "voiceover_internal",
                "speaker": "Character_Narrator",
                "dialogue": "Pajamas",
                "location_id": "Loc_A",
            },
            {
                "beat_id": "b17",
                "visual_event": "Character_Buster climbs stairs",
                "primary_subject": "Character_Buster",
                "action_class": "big_action",
                "continuity": "continuous_from_previous_beat",
                "delivery": "voiceover_internal",
                "speaker": "Character_Narrator",
                "dialogue": "Climbs",
                "location_id": "Loc_B",
            },
        ],
        "location_ids": ["Loc_A", "Loc_B"],
        "primary_location_id": "Loc_A",
        "duration_target_seconds": 20,
    }
    planned5 = plan_scene(
        s5,
        character_seeds=buster_seeds,
        location_seeds={
            "Loc_A": {"visual_lock": "Living room"},
            "Loc_B": {"visual_lock": "Stairs"},
        },
    )
    c2b = planned5["veo_clips"][1]["visual_prompt"].lower()
    assert "pajama" in c2b, planned5["veo_clips"][1]["visual_prompt"]
    assert "hat" in c2b, planned5["veo_clips"][1]["visual_prompt"]
    print("S5 C2 structured OK")
    print("PASS")


if __name__ == "__main__":
    main()
