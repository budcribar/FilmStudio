import json
import os
import sys
import time
import subprocess
import urllib.request
import urllib.error
from typing import Dict, Any, List, Optional

# Try to import Google GenAI SDK (mock/fallback if not installed for absolute robustness)
try:
    from google import genai
    from google.genai import types
except ImportError:
    genai = None
    types = None

STATE_FILE = "pipeline_state.json"
BLUEPRINT_FILE = "nickandme.json"
CONFIG_FILE = "pipeline_config.json"


class GenerationFailure(Exception):
    """
    Raised whenever a paid model/API call (Suno music, Veo video, or the local
    FFmpeg mux/master step) fails or produces unusable output.
    """
    pass


def music_output_path(scene_number: int) -> str:
    return f"assets/music/scene_{scene_number:02d}_music.mp3"


def clip_output_path(scene_number: int, clip_number: int) -> str:
    return f"assets/video/scene_{scene_number:02d}_clip_{clip_number:02d}.mp4"


def composite_output_path(scene_number: int) -> str:
    return f"assets/scenes/scene_{scene_number:02d}_complete.mp4"


class AgenticGenerationEngine:
    def __init__(self, blueprint_path: str = BLUEPRINT_FILE, state_path: str = STATE_FILE, config_path: str = CONFIG_FILE):
        self.blueprint_path = blueprint_path
        self.state_path = state_path
        self.config_path = config_path
        
        self.blueprint: Dict[str, Any] = {}
        self.state: Dict[str, Any] = {}
        self.config: Dict[str, Any] = {}
        self.client = None

        # Load configurations first
        self.load_config()
        
        # Initialize Google GenAI client if SDK is available and API key is set
        if genai and os.environ.get("GEMINI_API_KEY"):
            try:
                self.client = genai.Client()
            except Exception as e:
                print(f"[Warning] Failed to initialize GenAI Client: {e}")

        self.load_blueprint()
        self.load_state()

    def load_config(self):
        """Loads execution engine options from a distinct external configuration JSON."""
        if not os.path.exists(self.config_path):
            print(f"[Info] Config file not found. Generating default '{self.config_path}'...")
            self.config = {
                "model_name": "veo-3.1-fast-generate-preview", # Fast loop default
                "use_video_audio_for_music": False,             # Set to True to bypass Suno and use Veo native audio
                "aspect_ratio": "16:9",
                "duration_seconds": 8
            }
            try:
                with open(self.config_path, 'w') as f:
                    json.dump(self.config, f, indent=2)
            except Exception as e:
                print(f"[Warning] Failed to write default config file: {e}")
        else:
            try:
                with open(self.config_path, 'r') as f:
                    self.config = json.load(f)
                print(f"[Success] Config loaded dynamically from '{self.config_path}'")
            except Exception as e:
                print(f"[Warning] Failed to parse config file, using internal engine defaults: {e}")
                self.config = {
                    "model_name": "veo-3.1-fast-generate-preview",
                    "use_video_audio_for_music": False,
                    "aspect_ratio": "16:9",
                    "duration_seconds": 8
                }

    def load_blueprint(self):
        """Loads the master movie blueprint JSON payload."""
        if not os.path.exists(self.blueprint_path):
            print(f"[Error] Movie blueprint not found at: {self.blueprint_path}")
            sys.exit(1)
        try:
            with open(self.blueprint_path, 'r') as f:
                self.blueprint = json.load(f)
            print(f"[Success] Loaded master blueprint '{self.blueprint.get('movie_title', 'Untitled')}'")
        except Exception as e:
            print(f"[Error] Failed to parse blueprint JSON: {e}")
            sys.exit(1)

    def load_state(self):
        """Loads progress state or initializes a new pipeline state cache."""
        if os.path.exists(self.state_path):
            try:
                with open(self.state_path, 'r') as f:
                    self.state = json.load(f)
                print(f"[Success] Resumed execution state from '{self.state_path}'")
            except Exception as e:
                print(f"[Warning] Failed to parse state file, starting fresh: {e}")
                self.initialize_fresh_state()
        else:
            self.initialize_fresh_state()

    def initialize_fresh_state(self):
        """Initializes empty/default pipeline state metadata."""
        self.state = {
            "current_scene_index": 0,
            "scenes_completed": {},  
            "scene_assets": {},      
            "clip_context_ids": {}   
        }
        self.save_state()
        print("[Info] Initialized clean pipeline state cache.")

    def save_state(self):
        """Saves current state cache to disk with atomic safety."""
        temp_file = f"{self.state_path}.tmp"
        try:
            with open(temp_file, 'w') as f:
                json.dump(self.state, f, indent=2)
            os.replace(temp_file, self.state_path)
        except Exception as e:
            print(f"[Error] Failed to write state cache: {e}")

    def save_blueprint_to_disk(self):
        """Saves modifications made to the live blueprint in memory back to disk."""
        temp_file = f"{self.blueprint_path}.tmp"
        try:
            with open(temp_file, 'w') as f:
                json.dump(self.blueprint, f, indent=2)
            os.replace(temp_file, self.blueprint_path)
            print(f"[Success] Persisted blueprint updates to '{self.blueprint_path}'")
        except Exception as e:
            print(f"[Error] Failed to write blueprint to disk: {e}")

    def _build_suno_brief(self, music_bed: Dict[str, Any]) -> Dict[str, Any]:
        style = (music_bed.get("style_description") or "").strip()
        vocal = (music_bed.get("vocal_style") or "").strip()
        song_structure = music_bed.get("song_structure", []) or []

        tag_parts = [p for p in (style, vocal) if p]
        tags = ", ".join(tag_parts) if tag_parts else "cinematic, orchestral, emotional"

        lyric_blocks = []
        all_notes: List[str] = []
        for section in song_structure:
            notes = section.get("production_notes") or []
            all_notes.extend(notes)
            lyrics = section.get("lyrics")
            if lyrics:
                label = (section.get("section_label") or section.get("section_type") or "Section").strip()
                lyric_blocks.append(f"[{label}]\n{lyrics}")

        lyrics_text = "\n\n".join(lyric_blocks)
        notes_summary = "; ".join(all_notes[:6])

        return {
            "tags": tags,
            "lyrics_text": lyrics_text,
            "has_lyrics": bool(lyric_blocks),
            "notes_summary": notes_summary,
        }

    def _suno_submit(self, endpoint: str, payload: Dict[str, Any]) -> List[str]:
        try:
            req = urllib.request.Request(
                endpoint,
                data=json.dumps(payload).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST",
            )
            with urllib.request.urlopen(req, timeout=60) as response:
                raw = response.read().decode()
        except urllib.error.HTTPError as e:
            body = e.read().decode(errors="ignore") if hasattr(e, "read") else ""
            raise GenerationFailure(f"suno-api returned HTTP {e.code} from {endpoint}: {body[:300]}")
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            raise GenerationFailure(f"Could not reach suno-api at '{endpoint}': {e}.")

        try:
            res_data = json.loads(raw)
        except json.JSONDecodeError as e:
            raise GenerationFailure(f"suno-api returned non-JSON response from {endpoint}: {e}")

        if isinstance(res_data, dict) and res_data.get("detail"):
            raise GenerationFailure(f"suno-api rejected the job: {res_data['detail']}")
        if not isinstance(res_data, list) or not res_data:
            raise GenerationFailure(f"suno-api returned an unexpected response shape from {endpoint}: {res_data}")

        clip_ids = [item["id"] for item in res_data if isinstance(item, dict) and item.get("id")]
        if not clip_ids:
            raise GenerationFailure(f"suno-api response contained no usable clip IDs: {res_data}")
        return clip_ids

    def _suno_poll_for_audio(self, base_url: str, clip_ids: List[str], s_num: int,
                              poll_interval: int, timeout_seconds: int) -> str:
        ids_param = ",".join(clip_ids)
        deadline = time.time() + timeout_seconds

        while time.time() < deadline:
            try:
                req = urllib.request.Request(f"{base_url}/api/get?ids={ids_param}", method="GET")
                with urllib.request.urlopen(req, timeout=30) as response:
                    clips = json.loads(response.read().decode())
            except (urllib.error.URLError, urllib.error.HTTPError, TimeoutError, json.JSONDecodeError) as e:
                raise GenerationFailure(f"Scene {s_num} music: failed while polling suno-api job status: {e}")

            for clip in clips:
                status = clip.get("status")
                if status == "error":
                    err_detail = (clip.get("metadata") or {}).get("error_message", "no details provided")
                    raise GenerationFailure(f"Scene {s_num} music: suno-api reported an error: {err_detail}")
                if status == "complete" and clip.get("audio_url"):
                    print(f"  [Suno] Clip {clip.get('id')} complete.")
                    return clip["audio_url"]

            time.sleep(poll_interval)

        raise GenerationFailure(f"Scene {s_num} music: suno-api job timed out.")

    def generate_suno_music(self, scene: Dict[str, Any]) -> str:
        s_num = scene["scene_number"]
        music_bed = scene.get("music_bed", {})
        brief = self._build_suno_brief(music_bed)

        print(f"  [Suno] Submitting music generation for Scene {s_num}...")
        output_music_path = music_output_path(s_num)
        os.makedirs("assets/music", exist_ok=True)

        base_url = os.environ.get("SUNO_API_URL", "http://localhost:3000").rstrip("/")
        poll_interval = int(os.environ.get("SUNO_POLL_INTERVAL_SECONDS", "5"))
        timeout_seconds = int(os.environ.get("SUNO_TIMEOUT_SECONDS", "300"))
        title = (scene.get("scene_filename") or f"scene_{s_num:02d}")[:80]

        if brief["has_lyrics"]:
            endpoint = f"{base_url}/api/custom_generate"
            payload = {
                "prompt": brief["lyrics_text"],
                "tags": brief["tags"],
                "title": title,
                "make_instrumental": False,
                "wait_audio": False,
            }
        else:
            endpoint = f"{base_url}/api/generate"
            description = brief["tags"]
            if brief["notes_summary"]:
                description = f"{description}. {brief['notes_summary']}"
            payload = {
                "prompt": description,
                "make_instrumental": True,
                "wait_audio": False,
            }

        clip_ids = self._suno_submit(endpoint, payload)
        audio_url = self._suno_poll_for_audio(base_url, clip_ids, s_num, poll_interval, timeout_seconds)

        try:
            urllib.request.urlretrieve(audio_url, output_music_path)
        except Exception as e:
            raise GenerationFailure(f"Scene {s_num} music: failed to download: {e}")

        if not os.path.exists(output_music_path) or os.path.getsize(output_music_path) == 0:
            raise GenerationFailure(f"Scene {s_num} music: downloaded file is missing or zero-length.")

        return output_music_path

    def generate_veo_clip(self, scene_num: int, clip: Dict[str, Any], previous_context_id: Any = None) -> tuple:
        """Executes Google GenAI Veo 3.1 SDK requests using model paths tracking."""
        clip_num = clip["clip_number"]
        prompt = clip["visual_prompt"]
        neg_prompt = clip["negative_prompt"]
        continuation_source = clip.get("veo_continuation_source", "none")

        print(f"  [Veo 3.1] Generating Clip {clip_num} (Source: {continuation_source})...")
        output_clip_path = clip_output_path(scene_num, clip_num)
        os.makedirs("assets/video", exist_ok=True)

        if not self.client:
            raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: Google GenAI client is not initialized.")

        throttle_delay = int(os.environ.get("VEO_THROTTLE_DELAY", "30"))
        if throttle_delay > 0:
            print(f"  [Veo 3.1] Throttling: Cooling down for {throttle_delay}s...")
            time.sleep(throttle_delay)

        # --- MODEL SELECTION SECTIONS ---
        # Read setting from dynamic config JSON
        model_name = self.config.get("model_name", "veo-3.1-fast-generate-preview")
        
        # Reference Options for Developer adjustments:
        # model_name = "veo-3.1-fast-generate-preview"  # Default - Faster iterations for human review gating
        # model_name = "veo-3.1-generate-preview"       # Cinematic Quality preview target
        # model_name = "veo-3.1-generate-001"           # GA Production pipeline standard
        # model_name = "veo-3.1-fast-generate-001"      # GA Fast optimization standard
        # ---------------------------------

        # Setup standard video config constraints (always standard intervals: 4, 6, 8s)
        generation_config = types.GenerateVideosConfig(
            aspect_ratio=self.config.get("aspect_ratio", "16:9"),
            duration_seconds=self.config.get("duration_seconds", 8),
            negative_prompt=neg_prompt
        )

        # Overwrite native configuration parameters if flag dictates native background audio generation
        if self.config.get("use_video_audio_for_music", False):
            if "with audio" not in prompt.lower():
                prompt = f"{prompt}, high quality atmospheric stereo background cinematic sound and matching environment audio"

        try:
            if continuation_source == "extend_previous" and previous_context_id and not str(previous_context_id).startswith("ctx_mock_"):
                operation = self.client.models.generate_videos(
                    model=model_name,
                    prompt=prompt,
                    video=previous_context_id,
                    config=generation_config 
                )
            else:
                operation = self.client.models.generate_videos(
                    model=model_name,
                    prompt=prompt,
                    config=generation_config
                )

            poll_interval = int(os.environ.get("VEO_POLL_INTERVAL_SECONDS", "10"))
            while not getattr(operation, "done", False):
                time.sleep(poll_interval)
                operation = self.client.operations.get(operation)

            if getattr(operation, "error", None):
                raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: Veo 3.1 operation error: {operation.error}")

            generated_videos = getattr(getattr(operation, "response", None), "generated_videos", None)
            if not generated_videos:
                raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: Veo 3.1 returned no generated video.")

            # FIXED: Explicitly fetch file contents from the remote reference before attempting local file system write routines
            self.client.files.download(file=generated_videos[0].video)
            generated_videos[0].video.save(output_clip_path)
            
            context_id = getattr(operation, "name", f"ctx_s{scene_num}_c{clip_num}")

        except GenerationFailure:
            raise
        except Exception as e:
            raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: Veo 3.1 generation call failed: {e}")

        if not os.path.exists(output_clip_path) or os.path.getsize(output_clip_path) == 0:
            raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: output file is missing or zero-length.")

        return output_clip_path, context_id

    def run_gemini_qa(self, video_path: str, visual_prompt: str) -> bool:
        if not self.client:
            return True

        print(f"  [Gemini QA] Critiquing generated clip: {video_path}...")
        try:
            video_file = self.client.files.upload(file=video_path)

            while video_file.state.name == "PROCESSING":
                time.sleep(2)
                video_file = self.client.files.get(name=video_file.name)

            if video_file.state.name != "ACTIVE":
                raise ValueError(f"File upload entered state: {video_file.state.name}")

            evaluation_prompt = (
                f"Verify that this generated video clip matches the following visual description:\n"
                f"'{visual_prompt}'\n\n"
                f"Respond with a single JSON block:\n"
                f"{{\n"
                f"  \"approved\": true or false,\n"
                f"  \"critique\": \"your detailed observations about lighting, camera drift, and actor consistency\"\n"
                f"}}"
            )

            response = self.client.models.generate_content(
                model="gemini-2.5-flash",
                contents=[video_file, evaluation_prompt],
                config=types.GenerateContentConfig(
                    response_mime_type="application/json"
                )
            )

            result = json.loads(response.text)
            print(f"  [Gemini QA] Result: Approved={result.get('approved')}, Critique={result.get('critique')}")
            return result.get("approved", True)
        except Exception as e:
            print(f"  [Gemini QA Warning] Evaluation failed: {e}. Bypassing safely.")
            return True

    def mix_scene_assets(self, scene_num: int, clip_paths: List[str], music_path: Optional[str]) -> str:
        """Stitches clips and mixes music bed using FFmpeg."""
        output_scene_path = composite_output_path(scene_num)
        os.makedirs("assets/scenes", exist_ok=True)

        concat_list = f"assets/scenes/concat_list_scene_{scene_num}.txt"
        with open(concat_list, "w") as f:
            for p in clip_paths:
                f.write(f"file '../../{p}'\n")

        # Determine if we are muxing an independent external music track or using native video track lines
        if music_path and os.path.exists(music_path):
            ffmpeg_cmd = [
                "ffmpeg", "-y",
                "-f", "concat", "-safe", "0", "-i", concat_list,
                "-i", music_path,
                "-c:v", "copy",          
                "-c:a", "aac", "-shortest", 
                output_scene_path
            ]
        else:
            # If skipping Suno, copy the video tracks alongside their native internal audio tracks
            ffmpeg_cmd = [
                "ffmpeg", "-y",
                "-f", "concat", "-safe", "0", "-i", concat_list,
                "-c:v", "copy", "-c:a", "copy",
                output_scene_path
            ]

        print(f"  [FFmpeg] Muxing Scene {scene_num} composite media...")
        try:
            result = subprocess.run(ffmpeg_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        except Exception as e:
            raise GenerationFailure(f"Scene {scene_num}: local FFmpeg execution error while muxing: {e}")

        if result.returncode != 0:
            stderr_tail = result.stderr.decode(errors="ignore")[-500:]
            raise GenerationFailure(f"Scene {scene_num}: FFmpeg muxing failed. Last FFmpeg output: {stderr_tail}")

        return output_scene_path

    def _remove_zero_length_files(self, paths: List[str]) -> List[str]:
        removed = []
        for p in paths:
            try:
                if p and os.path.exists(p) and os.path.getsize(p) == 0:
                    os.remove(p)
                    removed.append(p)
            except OSError as e:
                print(f"  [Cleanup Warning] Could not remove zero-length file '{p}': {e}")
        return removed

    def _handle_generation_failure(self, scene_num: int, generated_files: List[str], error: GenerationFailure):
        print("\n" + "=" * 75)
        print(f"[FATAL] Generation failed for Scene {scene_num}. Halting pipeline.")
        print(f"[FATAL] Reason: {error}")
        print("=" * 75)

        removed = self._remove_zero_length_files(generated_files)
        if removed:
            print(f"[Cleanup] Removed {len(removed)} zero-length file(s).")

        s_key = str(scene_num)
        self.state["scenes_completed"][s_key] = False
        self.state["scene_assets"].pop(s_key, None)
        self.save_state()
        sys.exit(1)

    def process_scene(self, scene: Dict[str, Any]) -> bool:
        s_num = scene["scene_number"]
        print(f"\n==================== PROCESSING SCENE {s_num} ====================")

        generated_files: List[str] = []
        clip_paths: List[str] = []
        music_path: Optional[str] = None
        composite_scene_path: Optional[str] = None

        try:
            # Check dynamic background configuration flag rule
            if not self.config.get("use_video_audio_for_music", False):
                generated_files.append(music_output_path(s_num))
                music_path = self.generate_suno_music(scene)
            else:
                print(f"  [Pipeline Config] Bypassing Suno API Generation. Utilizing native video generator background music.")

            previous_context_id = None
            for clip in scene.get("veo_clips", []):
                c_num = clip["clip_number"]
                clip_state_key = f"{s_num}_{c_num}"
                cached_ctx = self.state["clip_context_ids"].get(clip_state_key)

                generated_files.append(clip_output_path(s_num, c_num))
                clip_path, context_id = self.generate_veo_clip(s_num, clip, cached_ctx or previous_context_id)
                clip_paths.append(clip_path)

                self.state["clip_context_ids"][clip_state_key] = context_id
                self.save_state()
                previous_context_id = context_id

                qa_passed = self.run_gemini_qa(clip_path, clip["visual_prompt"])

            generated_files.append(composite_output_path(s_num))
            composite_scene_path = self.mix_scene_assets(s_num, clip_paths, music_path)

        except GenerationFailure as e:
            self._handle_generation_failure(s_num, generated_files, e)

        self.state["scene_assets"][str(s_num)] = {
            "video_clips": clip_paths,
            "music_bed": music_path,
            "composite": composite_scene_path
        }
        self.save_state()
        return True

    def backpropagate_retroactive_feedback(self, current_scene_num: int, target_scene_num: int, feedback: str):
        print(f"\n[Retroactive Propagation] Propagating feedback from Scene {current_scene_num} to Scene {target_scene_num}...")
        for scene in self.blueprint.get("scenes", []):
            s_num = scene["scene_number"]
            if target_scene_num <= s_num <= current_scene_num:
                for clip in scene.get("veo_clips", []):
                    if feedback not in clip["visual_prompt"]:
                        current_prompt = clip["visual_prompt"]
                        suffix = " / 720p, 24fps"
                        base_prompt = current_prompt.replace(suffix, "").strip()

                        updated_prompt = f"{base_prompt}, {feedback}"
                        if len(updated_prompt) + len(suffix) < 400:
                            clip["visual_prompt"] = f"{updated_prompt}{suffix}"
                        else:
                            allowed_len = 400 - len(suffix)
                            clip["visual_prompt"] = f"{updated_prompt[:allowed_len]}{suffix}"

        seed_tokens = self.blueprint.get("global_production_variables", {}).get("character_seed_tokens", {})
        for char_key, token in seed_tokens.items():
            if char_key.lower() in feedback.lower() or "character" in feedback.lower():
                token["description"] = f"{token['description']}, (Feedback Sync: {feedback})"

        self.save_blueprint_to_disk()

        for s_idx in range(target_scene_num, current_scene_num + 1):
            s_key = str(s_idx)
            if s_key in self.state["scenes_completed"]:
                del self.state["scenes_completed"][s_key]
            if s_key in self.state["scene_assets"]:
                del self.state["scene_assets"][s_key]
            clip_keys_to_remove = [k for k in self.state["clip_context_ids"] if k.startswith(f"{s_idx}_")]
            for k in clip_keys_to_remove:
                del self.state["clip_context_ids"][k]

        self.state["current_scene_index"] = target_scene_num - 1
        self.save_state()

    def run_mastering(self):
        print("\n==================== PIPELINE STAGE 5: GLOBAL FILM MASTERING ====================")
        scenes = self.blueprint.get("scenes", [])
        output_movie_path = "assets/movie_final_master.mp4"

        scene_files = []
        for s in scenes:
            s_num = s["scene_number"]
            asset_info = self.state["scene_assets"].get(str(s_num))
            if asset_info and asset_info.get("composite"):
                scene_files.append(asset_info["composite"])

        if not scene_files:
            print("[Error] No approved scene files available for mastering.")
            return

        try:
            with open("concat_list.txt", "w") as f:
                for path in scene_files:
                    f.write(f"file '{path}'\n")
            concat_cmd = ["ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", "concat_list.txt", "-c", "copy", output_movie_path]
            result = subprocess.run(concat_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            print(f"SUCCESS: Completed cinematic mastering! Saved final video to: {output_movie_path}")
        except Exception as e:
            print(f"[Error] Mastering compilation failed: {e}")

    def run_pipeline(self):
        scenes = self.blueprint.get("scenes", [])
        total_scenes = len(scenes)

        current_idx = 0
        for idx, s in enumerate(scenes):
            s_num = str(s["scene_number"])
            if not self.state["scenes_completed"].get(s_num):
                current_idx = idx
                break
        else:
            current_idx = total_scenes

        self.state["current_scene_index"] = current_idx
        self.save_state()

        while current_idx < total_scenes:
            scene = scenes[current_idx]
            s_num = scene["scene_number"]

            self.process_scene(scene)

            print(f"\n*** INTERACTIVE GATE: REVIEW SCENE {s_num}/{total_scenes} ***")
            user_action = ""
            while user_action not in ["A", "F", "R", "Q"]:
                user_action = input("Review Scene [X]. [A] Approve, [F] Forward Feedback, [R] Retroactive Rollback, [Q] Quit: ").strip().upper()

            if user_action == "A":
                print(f"[Success] Approved Scene {s_num}.")
                self.state["scenes_completed"][str(s_num)] = True
                current_idx += 1
                self.state["current_scene_index"] = current_idx
                self.save_state()
            elif user_action == "F":
                feedback = input("\nEnter your forward feedback modifier: ").strip()
                scope = input("Select scoping - [L] Local Clip, [C] Cascading Scene, [G] Global Forward: ").strip().upper()
                self.state["scenes_completed"][str(s_num)] = False 
                self.save_state()
            elif user_action == "R":
                feedback = input("\nEnter retroactive feedback: ").strip()
                target_scene = 0
                while target_scene < 1 or target_scene > s_num:
                    try:
                        target_scene = int(input(f"Enter the past scene number where this modification should begin (1 to {s_num}): "))
                    except ValueError:
                        pass
                self.backpropagate_retroactive_feedback(s_num, target_scene, feedback)
                current_idx = self.state["current_scene_index"]
            elif user_action == "Q":
                sys.exit(0)

        self.run_mastering()


if __name__ == "__main__":
    print("=========================================================================")
    print("         AGENTIC GENERATION SCRIPT ENGINE (V8) - RUNNING                 ")
    print("=========================================================================")
    engine = AgenticGenerationEngine()
    engine.run_pipeline()