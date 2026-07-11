import base64
import json
import mimetypes
import os
import re
import signal
import sys
import time
import subprocess
import urllib.request
import urllib.error
from typing import Dict, Any, List, Optional, Tuple

# Optional Google GenAI SDK (Gemini QA / Imagen fallbacks only)
try:
    from google import genai
    from google.genai import types
except ImportError:
    genai = None
    types = None

STATE_FILE = "pipeline_state.json"
BLUEPRINT_FILE = "nickandme.json"
CONFIG_FILE = "pipeline_config.json"

XAI_API_BASE = "https://api.x.ai/v1"

DEFAULT_CONFIG = {
    "video_provider": "grok",
    "character_design_provider": "grok",
    "qa_provider": "grok",
    "image_model_name": "grok-imagine-image-quality",
    "qa_model_name": "grok-4.5",
    "model_name": "grok-imagine-video",
    "use_video_audio_for_music": True,
    "regenerate_silent_clips": True,
    "merge_scene_after_each_clip": True,
    # Last-frame continuation only for true continuous shots (not cuts / new locations)
    "smart_continuation": True,
    # Re-generate when Grok QA rejects a clip
    "qa_retry_on_fail": True,
    "qa_max_retries": 2,
    # Prefer Grok native audio. TTS is optional fallback only (often robotic).
    # Set ensure_dialogue_audio true only if native speech is missing/weak.
    "ensure_dialogue_audio": False,
    "dialogue_audio_mode": "replace",
    "dialogue_tts_volume": 1.0,
    "native_audio_mix_volume": 0.12,
    "aspect_ratio": "16:9",
    "duration_seconds": 8,
    "resolution": "720p",
    "qa_frame_count": 4,
}

# Prompt cues that mean a real cut / new setup — do NOT seed from previous last frame
_HARD_CUT_RE = re.compile(
    r"\b("
    r"CUT\s+TO|JUMP\s+CUT|SMASH\s+CUT|MATCH\s+CUT|"
    r"FLASHBACK|FLASH\s*FORWARD|"
    r"WIDE\s+SHOT|ESTABLISHING|AERIAL|DRONE\s+SHOT|"
    r"EXT\.|EXTERIOR|INT\.|INTERIOR|"
    r"MEANWHILE|LATER|ELSEWHERE|NEW\s+LOCATION"
    r")\b",
    re.IGNORECASE,
)


class GenerationFailure(Exception):
    """
    Raised whenever a paid model/API call (Suno music, Grok/Veo video, or the local
    FFmpeg mux/master step) fails or produces unusable output.
    """
    pass


class PipelineInterrupted(Exception):
    """Raised for graceful shutdown after Ctrl+C / SIGTERM (state is preserved)."""
    pass


def music_output_path(scene_number: int) -> str:
    return f"assets/music/scene_{scene_number:02d}_music.mp3"


def clip_output_path(scene_number: int, clip_number: int) -> str:
    return f"assets/video/scene_{scene_number:02d}_clip_{clip_number:02d}.mp4"


def composite_output_path(scene_number: int) -> str:
    return f"assets/scenes/scene_{scene_number:02d}_complete.mp4"


ASSET_DIRS = (
    "assets",
    "assets/characters",
    "assets/music",
    "assets/video",
    "assets/scenes",
)


def ensure_parent_dir(path: str) -> None:
    """Create the parent directory of a file path if it does not exist."""
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)


def file_is_usable(path: Optional[str], min_bytes: int = 1) -> bool:
    """True when path exists and is larger than min_bytes (not a failed empty download)."""
    try:
        return bool(path) and os.path.isfile(path) and os.path.getsize(path) >= min_bytes
    except OSError:
        return False


def _running_on_wsl() -> bool:
    """True when the Python process is inside WSL (Linux kernel Microsoft build)."""
    if os.name == "nt":
        return False
    try:
        with open("/proc/version", "r", encoding="utf-8", errors="ignore") as f:
            return "microsoft" in f.read().lower()
    except OSError:
        return False


def resolve_ffmpeg() -> str:
    """
    Return an ffmpeg executable path.

    Order:
      1. FFMPEG_PATH / FFMPEG env override
      2. PATH (works in WSL when `ffmpeg` is apt-installed)
      3. Common Linux/WSL absolute paths
      4. Common Windows install paths (native Windows only)
      5. imageio-ffmpeg bundled binary (same OS only)
    """
    import shutil
    import sys

    for env_key in ("FFMPEG_PATH", "FFMPEG"):
        env_path = (os.environ.get(env_key) or "").strip().strip('"')
        if env_path and os.path.isfile(env_path) and os.access(env_path, os.X_OK):
            return env_path
        # On Windows, X_OK is odd; also accept plain exists
        if env_path and os.path.isfile(env_path):
            return env_path

    found = shutil.which("ffmpeg")
    if found:
        return found

    is_windows = os.name == "nt"
    is_wsl = _running_on_wsl()

    linux_candidates = [
        "/usr/bin/ffmpeg",
        "/usr/local/bin/ffmpeg",
        "/bin/ffmpeg",
        os.path.expanduser("~/.local/bin/ffmpeg"),
    ]
    windows_candidates = [
        r"C:\ffmpeg\bin\ffmpeg.exe",
        r"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
        r"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
        os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WinGet\Links\ffmpeg.exe"),
        os.path.expandvars(r"%USERPROFILE%\scoop\shims\ffmpeg.exe"),
        os.path.expandvars(r"%ProgramData%\chocolatey\bin\ffmpeg.exe"),
    ]

    candidates = []
    if is_windows:
        candidates.extend(windows_candidates)
    else:
        # WSL / native Linux: never pick a Windows .exe
        candidates.extend(linux_candidates)

    for c in candidates:
        if c and os.path.isfile(c):
            return c

    # Bundled binary via optional dependency (pip install imageio-ffmpeg)
    try:
        import imageio_ffmpeg
        bundled = imageio_ffmpeg.get_ffmpeg_exe()
        if bundled and os.path.isfile(bundled):
            # Reject cross-OS binaries (e.g. .exe under WSL)
            if is_windows and not bundled.lower().endswith(".exe"):
                pass
            elif (not is_windows) and bundled.lower().endswith(".exe"):
                pass
            else:
                return bundled
    except Exception:
        pass

    return "ffmpeg"


class AgenticGenerationEngine:
    def __init__(self, blueprint_path: str = BLUEPRINT_FILE, state_path: str = STATE_FILE, config_path: str = CONFIG_FILE):
        self.blueprint_path = blueprint_path
        self.state_path = state_path
        self.config_path = config_path
        
        self.blueprint: Dict[str, Any] = {}
        self.state: Dict[str, Any] = {}
        self.config: Dict[str, Any] = {}
        self.client = None
        self._shutdown_requested = False
        self._active_scene_num: Optional[int] = None
        self._active_clip_num: Optional[int] = None

        # Ensure asset tree exists before any I/O
        self.ensure_asset_directories()

        # Load configurations first
        self.load_config()

        # Log runtime tooling so WSL vs Windows path issues are obvious
        ffmpeg_path = resolve_ffmpeg()
        env_label = "WSL" if _running_on_wsl() else ("Windows" if os.name == "nt" else "Linux")
        print(f"[Runtime] host={env_label}, ffmpeg={ffmpeg_path}")
        
        # Optional Gemini client (QA/Imagen fallbacks when qa_provider/character_design_provider is gemini)
        if genai and os.environ.get("GEMINI_API_KEY"):
            try:
                self.client = genai.Client()
            except Exception as e:
                print(f"[Warning] Failed to initialize GenAI Client: {e}")

        self.load_blueprint()
        self.load_state()
        self._install_signal_handlers()

    def _install_signal_handlers(self) -> None:
        """Ctrl+C / SIGTERM request a graceful stop (second signal forces exit)."""
        def _handler(signum, frame):
            sig_name = signal.Signals(signum).name if hasattr(signal, "Signals") else str(signum)
            if self._shutdown_requested:
                print(f"\n[Shutdown] Second {sig_name} — forcing exit now.")
                try:
                    self.save_state()
                except Exception:
                    pass
                os._exit(130)
            self._shutdown_requested = True
            print(
                f"\n[Shutdown] {sig_name} received — stopping after the current wait "
                f"(state will be saved; re-run to resume). Press Ctrl+C again to force quit."
            )

        for sig in (signal.SIGINT, signal.SIGTERM):
            try:
                signal.signal(sig, _handler)
            except (ValueError, OSError):
                # Signals can be restricted in some embedded hosts
                pass

    def _check_shutdown(self, where: str = "") -> None:
        """Raise PipelineInterrupted if the user asked to stop."""
        if self._shutdown_requested:
            loc = f" during {where}" if where else ""
            raise PipelineInterrupted(f"Interrupted by user{loc}")

    def _interruptible_sleep(self, seconds: float, where: str = "sleep") -> None:
        """Sleep in short slices so Ctrl+C is handled promptly."""
        end = time.time() + max(0.0, float(seconds))
        while time.time() < end:
            self._check_shutdown(where)
            time.sleep(min(0.5, end - time.time()))

    def graceful_stop(self, reason: str = "Interrupted by user") -> None:
        """Save state and exit cleanly (no traceback)."""
        print("\n" + "=" * 75)
        print(f"[Shutdown] {reason}")
        if self._active_scene_num is not None:
            print(
                f"[Shutdown] Active work: Scene {self._active_scene_num}"
                + (f" Clip {self._active_clip_num}" if self._active_clip_num is not None else "")
            )
        try:
            # Mark current scene incomplete so resume continues here
            if self._active_scene_num is not None:
                self.state.setdefault("scenes_completed", {})[str(self._active_scene_num)] = False
                assets = self.state.setdefault("scene_assets", {}).setdefault(str(self._active_scene_num), {})
                assets["partial"] = True
                assets["last_error"] = reason
            self.save_state()
            print(f"[Shutdown] Progress saved to '{self.state_path}'.")
        except Exception as e:
            print(f"[Shutdown Warning] Could not save state: {e}")
        print("[Shutdown] Re-run:  python3 generation_script.py")
        print("           Existing clips will be reused; incomplete jobs will resume.")
        print("=" * 75)
        raise SystemExit(130)

    def ensure_asset_directories(self) -> None:
        """Create all standard asset directories if missing."""
        for d in ASSET_DIRS:
            os.makedirs(d, exist_ok=True)

    def load_config(self):
        """Loads execution engine options from a distinct external configuration JSON."""
        if not os.path.exists(self.config_path):
            print(f"[Info] Config file not found. Generating default '{self.config_path}'...")
            self.config = dict(DEFAULT_CONFIG)
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
                self.config = dict(DEFAULT_CONFIG)

        # Fill any missing keys from defaults so older config files still work
        for key, value in DEFAULT_CONFIG.items():
            self.config.setdefault(key, value)

        video_provider = str(self.config.get("video_provider", "grok")).lower()
        char_provider = str(self.config.get("character_design_provider", "grok")).lower()
        qa_provider = str(self.config.get("qa_provider", "grok")).lower()
        print(
            f"[Config] video_provider={video_provider}, "
            f"character_design_provider={char_provider}, "
            f"qa_provider={qa_provider}, "
            f"model_name={self.config.get('model_name')}, "
            f"image_model_name={self.config.get('image_model_name')}, "
            f"qa_model_name={self.config.get('qa_model_name')}"
        )

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

    def initialize_fresh_state(self):
        """Initializes empty/default pipeline state metadata."""
        self.state = {
            "characters_designed": False,
            "current_scene_index": 0,
            "scenes_completed": {},
            "scene_assets": {},
            "clip_context_ids": {},
            # Per-clip job metadata for resume after download/API failures
            "clip_jobs": {},
            "music_jobs": {},
        }
        self.save_state()
        print("[Info] Initialized clean pipeline state cache.")

    def load_state(self):
        """Loads progress state or initializes a new pipeline state cache."""
        if os.path.exists(self.state_path):
            try:
                with open(self.state_path, 'r') as f:
                    self.state = json.load(f)
                # Backfill keys added for resume support
                self.state.setdefault("characters_designed", False)
                self.state.setdefault("current_scene_index", 0)
                self.state.setdefault("scenes_completed", {})
                self.state.setdefault("scene_assets", {})
                self.state.setdefault("clip_context_ids", {})
                self.state.setdefault("clip_jobs", {})
                self.state.setdefault("music_jobs", {})
                print(f"[Success] Resumed execution state from '{self.state_path}'")
            except Exception as e:
                print(f"[Warning] Failed to parse state file, starting fresh: {e}")
                self.initialize_fresh_state()
        else:
            self.initialize_fresh_state()

    def save_state(self):
        """Saves current state cache to disk with atomic safety."""
        ensure_parent_dir(self.state_path)
        temp_file = f"{self.state_path}.tmp"
        try:
            with open(temp_file, 'w') as f:
                json.dump(self.state, f, indent=2)
            os.replace(temp_file, self.state_path)
        except Exception as e:
            print(f"[Error] Failed to write state cache: {e}")

    def save_blueprint_to_disk(self):
        """Saves modifications made to the live blueprint in memory back to disk."""
        ensure_parent_dir(self.blueprint_path)
        temp_file = f"{self.blueprint_path}.tmp"
        try:
            with open(temp_file, 'w') as f:
                json.dump(self.blueprint, f, indent=2)
            os.replace(temp_file, self.blueprint_path)
            print(f"[Success] Persisted blueprint updates to '{self.blueprint_path}'")
        except Exception as e:
            print(f"[Error] Failed to write blueprint to disk: {e}")

    def _clip_job_key(self, scene_num: int, clip_num: int) -> str:
        return f"{scene_num}_{clip_num}"

    def _update_clip_job(self, scene_num: int, clip_num: int, **fields: Any) -> None:
        key = self._clip_job_key(scene_num, clip_num)
        job = self.state.setdefault("clip_jobs", {}).setdefault(key, {})
        job.update(fields)
        job["scene_number"] = scene_num
        job["clip_number"] = clip_num
        job["updated_at"] = time.strftime("%Y-%m-%dT%H:%M:%S")
        self.save_state()

    def _download_to_path(self, url: str, output_path: str, retries: int = 5,
                          timeout: int = 300, label: str = "asset") -> None:
        """
        Download a remote URL to disk with retries, parent-dir creation, and atomic rename.
        Partial/failed files are written to output_path.tmp then promoted on success.
        """
        if not url:
            raise GenerationFailure(f"Cannot download {label}: empty URL.")

        ensure_parent_dir(output_path)
        tmp_path = f"{output_path}.download.tmp"
        last_error: Optional[Exception] = None

        for attempt in range(1, retries + 1):
            try:
                if os.path.exists(tmp_path):
                    try:
                        os.remove(tmp_path)
                    except OSError:
                        pass

                print(f"  [Download] {label}: attempt {attempt}/{retries} -> {output_path}")
                req = urllib.request.Request(url, method="GET", headers={
                    "User-Agent": "NickAndMe-GenerationScript/8.9",
                })
                with urllib.request.urlopen(req, timeout=timeout) as response:
                    # Stream to disk so large videos do not require full memory buffer
                    with open(tmp_path, "wb") as out_f:
                        while True:
                            chunk = response.read(1024 * 1024)
                            if not chunk:
                                break
                            out_f.write(chunk)

                if not os.path.exists(tmp_path) or os.path.getsize(tmp_path) == 0:
                    raise GenerationFailure(f"Downloaded {label} is empty: {tmp_path}")

                os.replace(tmp_path, output_path)
                size_mb = os.path.getsize(output_path) / (1024 * 1024)
                print(f"  [Download] {label}: saved ({size_mb:.2f} MB)")
                return
            except GenerationFailure as e:
                last_error = e
            except Exception as e:
                last_error = e

            # Clean partial temp on failure
            try:
                if os.path.exists(tmp_path):
                    os.remove(tmp_path)
            except OSError:
                pass

            if attempt < retries:
                backoff = min(30, 2 ** attempt)
                print(f"  [Download] {label}: failed ({last_error}). Retrying in {backoff}s...")
                time.sleep(backoff)

        raise GenerationFailure(
            f"Failed to download {label} to '{output_path}' after {retries} attempts: {last_error}"
        )

    def _grok_generate_image_variants(self, prompt: str, n: int = 3, aspect_ratio: str = "1:1") -> List[bytes]:
        """Generate n portrait variants via Grok Imagine image API; returns raw image bytes list."""
        model_name = self.config.get("image_model_name", "grok-imagine-image-quality")
        payload = {
            "model": model_name,
            "prompt": prompt,
            "n": n,
            "aspect_ratio": aspect_ratio,
            "response_format": "b64_json",
        }
        result = self._grok_request("POST", f"{XAI_API_BASE}/images/generations", payload, timeout=180)
        data = result.get("data")
        if not isinstance(data, list) or not data:
            # Some responses return a top-level url list; try URL download path
            raise GenerationFailure(f"Grok image API returned no image data: {result}")

        images: List[bytes] = []
        for item in data:
            if not isinstance(item, dict):
                continue
            if item.get("b64_json"):
                images.append(base64.b64decode(item["b64_json"]))
            elif item.get("url"):
                # Temporary URL fallback when base64 is unavailable
                tmp_path = f"assets/characters/_grok_tmp_{len(images)}.img"
                self._download_to_path(item["url"], tmp_path)
                with open(tmp_path, "rb") as f:
                    images.append(f.read())
                try:
                    os.remove(tmp_path)
                except OSError:
                    pass
        if len(images) < n:
            raise GenerationFailure(
                f"Grok image API returned {len(images)}/{n} usable images: {result}"
            )
        return images[:n]

    def _imagen_generate_image_variants(self, prompt: str, n: int = 3) -> List[bytes]:
        """Optional Imagen fallback for character portraits (requires Gemini client)."""
        if not self.client or types is None:
            raise GenerationFailure("Imagen character design requires Gemini GenAI client (GEMINI_API_KEY).")

        images: List[bytes] = []
        for _ in range(n):
            result = self.client.models.generate_images(
                model="imagen-3.0-generate-002",
                prompt=prompt,
                config=types.GenerateImagesConfig(
                    numberOfImages=1,
                    outputMimeType="image/png",
                    aspectRatio="1:1",
                ),
            )
            for generated_img in result.generated_images:
                images.append(generated_img.image.image_bytes)
        if len(images) < n:
            raise GenerationFailure(f"Imagen returned {len(images)}/{n} character variants.")
        return images[:n]

    def pre_production_character_design(self):
        """STAGE 0: Interactively generate 3 portrait options per character (Grok Imagine by default)."""
        print("\n==================== STAGE 0: PRE-PRODUCTION CHARACTER DESIGN GATE ====================")
        self.ensure_asset_directories()
        os.makedirs("assets/characters", exist_ok=True)

        char_seeds = self.blueprint.get("global_production_variables", {}).get("character_seed_tokens", {})
        if not char_seeds:
            print("[Info] No upfront character seed tokens found in blueprint. Skipping Phase 0.")
            return

        char_provider = str(self.config.get("character_design_provider", "grok")).lower().strip()
        if char_provider in ("grok", "xai", "imagine"):
            if not os.environ.get("XAI_API_KEY"):
                print("[Warning] XAI_API_KEY not set. Bypassing automatic character generation.")
                return
            design_label = "Grok Imagine"
        elif char_provider in ("imagen", "gemini", "google"):
            if not self.client:
                print("[Warning] Gemini/Imagen client not configured. Bypassing automatic character generation.")
                return
            design_label = "Imagen 3"
        else:
            print(f"[Warning] Unknown character_design_provider '{char_provider}'. Bypassing Stage 0.")
            return

        for char_key, seed_info in char_seeds.items():
            local_image_name = seed_info.get("reference_image_placeholder", f"{char_key.lower()}_ref.png")
            final_path = f"assets/characters/{local_image_name}"

            # Check if this character has already been locked in from a previous execution
            if os.path.exists(final_path):
                print(f"[Anchor Locked] Character reference asset already locked at: {final_path}")
                continue

            satisfied = False
            while not satisfied:
                print(f"\n[Designing] Launching {design_label} variants for {char_key}...")
                description = seed_info.get("description", "")

                # Formulate structural design template prompt matching film parameters
                treatment = self.blueprint.get("global_production_variables", {}).get(
                    "directorial_treatment", "cinematic lighting"
                )
                design_prompt = (
                    f"A detailed portrait model-sheet photograph of {char_key}: {description}. "
                    f"Character centered in frame, look straight at camera, neutral expression, {treatment}. "
                    f"High texture realism, isolated plain dark concrete studio background."
                )

                option_paths: List[str] = []
                try:
                    if char_provider in ("grok", "xai", "imagine"):
                        print("  Generating 3 option variants in one Grok batch...")
                        image_blobs = self._grok_generate_image_variants(design_prompt, n=3, aspect_ratio="1:1")
                    else:
                        print("  Generating 3 option variants via Imagen...")
                        image_blobs = self._imagen_generate_image_variants(design_prompt, n=3)

                    for idx, blob in enumerate(image_blobs, start=1):
                        opt_path = f"assets/characters/{char_key.lower()}_variant_0{idx}.png"
                        ensure_parent_dir(opt_path)
                        with open(opt_path, "wb") as f:
                            f.write(blob)
                        option_paths.append(opt_path)
                        print(f"  Saved Option variant {idx}/3 -> {opt_path}")
                except Exception as e:
                    print(f"  [Error] Variant generation failed: {e}")
                    for p in option_paths:
                        if os.path.exists(p):
                            os.remove(p)
                    print("[Error] Retrying entire batch...")
                    continue

                if len(option_paths) < 3:
                    print("[Error] Failed to generate all 3 variants. Retrying entire batch...")
                    for p in option_paths:
                        if os.path.exists(p):
                            os.remove(p)
                    continue

                print(f"\n*** INTERACTIVE SELECTION FOR {char_key} ***")
                print(f"Option 1 saved to: {option_paths[0]}")
                print(f"Option 2 saved to: {option_paths[1]}")
                print(f"Option 3 saved to: {option_paths[2]}")

                user_choice = ""
                while user_choice not in ["1", "2", "3", "R"]:
                    user_choice = input(
                        "Please inspect the character images. "
                        "Select [1], [2], [3] to Lock character look, or [R] to Regenerate fresh options: "
                    ).strip().upper()

                if user_choice in ["1", "2", "3"]:
                    selected_index = int(user_choice) - 1
                    chosen_variant_path = option_paths[selected_index]

                    # Promote chosen temporary variant image file to the master locked anchor file path
                    os.replace(chosen_variant_path, final_path)
                    print(f"[Success] Locked {char_key} design choice! Saved to master reference slot: {final_path}")

                    # Clean up other trailing variants to keep directories clean
                    for p in option_paths:
                        if os.path.exists(p):
                            os.remove(p)
                    satisfied = True
                elif user_choice == "R":
                    print(f"Flushing variant cache and rerolling seed space layout for {char_key}...")
                    for p in option_paths:
                        if os.path.exists(p):
                            os.remove(p)

        self.state["characters_designed"] = True
        self.save_state()

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
            self._check_shutdown(f"Suno poll Scene {s_num}")
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

            self._interruptible_sleep(poll_interval, f"Suno poll Scene {s_num}")

        raise GenerationFailure(f"Scene {s_num} music: suno-api job timed out.")

    def generate_suno_music(self, scene: Dict[str, Any]) -> str:
        s_num = scene["scene_number"]
        music_bed = scene.get("music_bed", {})
        brief = self._build_suno_brief(music_bed)
        output_music_path = music_output_path(s_num)
        ensure_parent_dir(output_music_path)
        os.makedirs("assets/music", exist_ok=True)

        # Resume: keep existing music if already on disk
        if file_is_usable(output_music_path, min_bytes=1024):
            print(f"  [Suno] Reusing existing music bed: {output_music_path}")
            return output_music_path

        music_job = self.state.setdefault("music_jobs", {}).get(str(s_num), {})
        base_url = os.environ.get("SUNO_API_URL", "http://localhost:3000").rstrip("/")
        poll_interval = int(os.environ.get("SUNO_POLL_INTERVAL_SECONDS", "5"))
        timeout_seconds = int(os.environ.get("SUNO_TIMEOUT_SECONDS", "300"))
        title = (scene.get("scene_filename") or f"scene_{s_num:02d}")[:80]

        # Resume pending download if we already have a URL from a prior run
        pending_url = music_job.get("audio_url")
        if pending_url:
            print(f"  [Suno] Resuming pending music download for Scene {s_num}...")
            try:
                self._download_to_path(
                    pending_url, output_music_path,
                    label=f"Scene {s_num} music",
                )
                self.state["music_jobs"][str(s_num)] = {
                    "status": "complete",
                    "path": output_music_path,
                }
                self.save_state()
                return output_music_path
            except GenerationFailure as e:
                print(f"  [Suno] Pending download failed ({e}); submitting a fresh job...")

        print(f"  [Suno] Submitting music generation for Scene {s_num}...")

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
        self.state.setdefault("music_jobs", {})[str(s_num)] = {
            "status": "submitted",
            "clip_ids": clip_ids,
        }
        self.save_state()

        audio_url = self._suno_poll_for_audio(base_url, clip_ids, s_num, poll_interval, timeout_seconds)
        self.state["music_jobs"][str(s_num)] = {
            "status": "pending_download",
            "clip_ids": clip_ids,
            "audio_url": audio_url,
        }
        self.save_state()

        self._download_to_path(audio_url, output_music_path, label=f"Scene {s_num} music")
        self.state["music_jobs"][str(s_num)] = {
            "status": "complete",
            "path": output_music_path,
        }
        self.save_state()
        return output_music_path

    def _file_to_data_uri(self, path: str) -> str:
        """Encode a local media file as a base64 data URI for the xAI API."""
        mime, _ = mimetypes.guess_type(path)
        if not mime:
            lower = path.lower()
            if lower.endswith(".png"):
                mime = "image/png"
            elif lower.endswith((".jpg", ".jpeg")):
                mime = "image/jpeg"
            elif lower.endswith(".webp"):
                mime = "image/webp"
            elif lower.endswith(".mp4"):
                mime = "video/mp4"
            else:
                mime = "application/octet-stream"
        with open(path, "rb") as f:
            encoded = base64.b64encode(f.read()).decode("ascii")
        return f"data:{mime};base64,{encoded}"

    def _find_character_anchor_path(self, prompt: str) -> Optional[str]:
        """Return the first locked character reference image mentioned in the prompt."""
        char_seeds = self.blueprint.get("global_production_variables", {}).get("character_seed_tokens", {})
        for char_key, seed_info in char_seeds.items():
            if char_key in prompt:
                local_image_name = seed_info.get("reference_image_placeholder", f"{char_key.lower()}_ref.png")
                local_image_path = f"assets/characters/{local_image_name}"
                if os.path.exists(local_image_path):
                    return local_image_path
        return None

    def _simplify_visual_for_single_clip(self, visual: str) -> str:
        """
        Rewrite multi-beat 'SHOT A. CUT to SHOT B' prompts into one continuous short-clip sequence.
        Short models handle one evolving shot better than hard multi-setup edits.
        """
        visual = (visual or "").strip()
        if not visual:
            return visual

        # Strip trailing technical suffix so we can reattach cleanly
        suffix = ""
        m = re.search(r"\s*/\s*720p.*$", visual, re.IGNORECASE)
        if m:
            suffix = visual[m.start():]
            visual = visual[:m.start()].strip()

        if re.search(r"\bCUT\s+TO\b", visual, re.IGNORECASE):
            parts = re.split(r"\bCUT\s+TO\b", visual, maxsplit=1, flags=re.IGNORECASE)
            if len(parts) == 2:
                first = parts[0].strip(" .;,")
                second = parts[1].strip(" .;,")
                visual = (
                    "Single continuous cinematic sequence for one short clip "
                    "(smooth transition, not a jarring random cut): "
                    f"Begin with {first}. Then the camera / scene transitions into {second}."
                )

        # Flashbacks should be clearly labeled as a new visual world
        if re.search(r"\bFLASHBACK\b", visual, re.IGNORECASE):
            if "new scene" not in visual.lower():
                visual = (
                    "Distinct FLASHBACK sequence in a clearly different time/place "
                    f"(do not continue the previous present-day framing): {visual}"
                )

        return f"{visual}{suffix}".strip()

    def _should_use_last_frame_continuation(
        self,
        clip: Dict[str, Any],
        continuation_source: str,
        prev_path: Optional[str],
    ) -> bool:
        """
        Only continue from the previous last frame for true same-setup extensions.
        Hard cuts, exteriors, flashbacks, and multi-shot 'CUT TO' prompts must start fresh
        (optionally with character reference images) so Grok is not stuck on the prior close-up.
        """
        if not self.config.get("smart_continuation", True):
            # Legacy behavior: any non-none continuation_source uses last frame
            return (
                continuation_source not in (None, "", "none")
                and bool(prev_path)
                and file_is_usable(prev_path, min_bytes=1024)
                and not str(prev_path).startswith("ctx_mock_")
            )

        if continuation_source in (None, "", "none"):
            return False
        if not prev_path or not file_is_usable(prev_path, min_bytes=1024):
            return False
        if str(prev_path).startswith("ctx_mock_"):
            return False

        visual = clip.get("visual_prompt") or ""
        if _HARD_CUT_RE.search(visual):
            return False
        # Explicit multi-setup language
        if re.search(r"\bCUT\s+TO\b", visual, re.IGNORECASE):
            return False

        return continuation_source.lower() in {
            "extend_previous",
            "extend",
            "continue",
            "previous",
            "continuous",
            "continuation",
        }

    def _build_video_generation_prompt(self, clip: Dict[str, Any], mode: str = "fresh") -> str:
        """
        Merge visual_prompt + audio_payload so Grok generates native audio
        (dialogue / narration / ambient / Foley) with the picture.

        AUDIO is placed first — Grok responds better when speech is explicit and early.
        """
        visual = self._simplify_visual_for_single_clip((clip.get("visual_prompt") or "").strip())
        audio = clip.get("audio_payload") or {}
        speaker = (audio.get("speaker") or "").strip()
        dialogue = (audio.get("dialogue") or "").strip()
        sfx = (audio.get("sfx") or audio.get("sound_effects") or "").strip()
        ambient = (audio.get("ambient") or audio.get("atmosphere") or "").strip()

        framing_bits: List[str] = []
        if mode == "continue":
            framing_bits.append(
                "Continue seamlessly from the provided starting frame with the same character identity, "
                "wardrobe, and location. Natural camera motion only — do not invent a new establishing shot."
            )
        else:
            framing_bits.append(
                "Follow the camera framing and location in this prompt exactly. "
                "If a wide/exterior/establishing shot is specified, show that environment clearly "
                "(do not stay locked on an unrelated close-up face)."
            )

        speaker_ok = bool(speaker) and speaker.lower() not in ("none", "n/a", "null", "-")

        # Leading AUDIO block (format used successfully with Grok Imagine)
        if dialogue and speaker_ok:
            audio_block = (
                f'AUDIO: Required audible stereo soundtrack. '
                f'Clear male/female conversational voiceover for {speaker} at normal listening volume, '
                f'not whispered, not muted, fully intelligible English: "{dialogue}". '
                f'Also include matching ambient room tone and environmental Foley under the voice.'
            )
        elif dialogue:
            audio_block = (
                f'AUDIO: Required audible stereo soundtrack. Clear voiceover at normal volume: "{dialogue}". '
                f'Include ambient atmosphere under the voice.'
            )
        else:
            audio_block = (
                "AUDIO: Required audible stereo soundtrack with realistic ambient environmental sound "
                "and Foley matching the action. No spoken dialogue. Do not output a silent clip."
            )

        if sfx:
            audio_block += f" Sound effects: {sfx}."
        if ambient:
            audio_block += f" Ambient bed: {ambient}."
        if self.config.get("use_video_audio_for_music", True) and dialogue:
            audio_block += " Keep speech dominant over any background music or wind."

        return (
            f"{audio_block} "
            f"VISUAL: {visual} {' '.join(framing_bits)} "
            f"Must include synchronized native audio track with the video."
        ).strip()

    def _mp4_audio_stats(self, video_path: str) -> Dict[str, Any]:
        """
        Pure-Python MP4 probe: detect audio track and rough payload size via stsz samples.
        Works even when ffmpeg is not on PATH.
        """
        import struct

        stats: Dict[str, Any] = {
            "has_audio_track": False,
            "audio_bytes": 0,
            "audio_samples": 0,
            "video_bytes": 0,
        }
        if not file_is_usable(video_path, min_bytes=8):
            return stats

        try:
            with open(video_path, "rb") as f:
                data = f.read()
        except OSError:
            return stats

        def iter_boxes(buf: bytes, start: int = 0, end: Optional[int] = None):
            if end is None:
                end = len(buf)
            i = start
            while i + 8 <= end:
                size = struct.unpack(">I", buf[i : i + 4])[0]
                typ = buf[i + 4 : i + 8]
                if size == 1:
                    if i + 16 > end:
                        break
                    size = struct.unpack(">Q", buf[i + 8 : i + 16])[0]
                    hdr = 16
                elif size == 0:
                    size = end - i
                    hdr = 8
                else:
                    hdr = 8
                if size < hdr:
                    break
                yield i, size, typ, i + hdr, i + size
                next_i = i + size
                if next_i <= i:
                    break
                i = next_i

        def find_boxes(buf: bytes, target: bytes, start: int = 0, end: Optional[int] = None):
            for i, size, typ, cs, ce in iter_boxes(buf, start, end):
                if typ == target:
                    yield i, size, typ, cs, ce
                if typ in (b"moov", b"trak", b"mdia", b"minf", b"stbl", b"udta", b"edts"):
                    yield from find_boxes(buf, target, cs, ce)

        try:
            for _, _, _, tcs, tce in find_boxes(data, b"trak"):
                hdlrs = list(find_boxes(data, b"hdlr", tcs, tce))
                if not hdlrs:
                    continue
                _, _, _, hs, _ = hdlrs[0]
                handler = data[hs + 8 : hs + 12]
                stszs = list(find_boxes(data, b"stsz", tcs, tce))
                if not stszs:
                    continue
                _, _, _, ss, se = stszs[0]
                if ss + 12 > len(data):
                    continue
                sample_size = struct.unpack(">I", data[ss + 4 : ss + 8])[0]
                sample_count = struct.unpack(">I", data[ss + 8 : ss + 12])[0]
                if sample_size == 0:
                    need = ss + 12 + 4 * sample_count
                    if need > len(data) or sample_count < 0 or sample_count > 2_000_000:
                        total = 0
                    else:
                        sizes = struct.unpack(f">{sample_count}I", data[ss + 12 : need])
                        total = int(sum(sizes))
                else:
                    total = int(sample_size) * int(sample_count)

                if handler == b"soun":
                    stats["has_audio_track"] = True
                    stats["audio_bytes"] = total
                    stats["audio_samples"] = sample_count
                elif handler == b"vide":
                    stats["video_bytes"] = total
        except Exception as e:
            print(f"  [Audio Probe Warning] MP4 parse failed for '{video_path}': {e}")

        return stats

    def _video_has_audio(self, video_path: str) -> bool:
        """Return True if the file has an audio track (ffmpeg or pure-Python MP4 parse)."""
        if not file_is_usable(video_path, min_bytes=1):
            return False

        # Fast pure-Python path (reliable without ffmpeg on PATH)
        mp4 = self._mp4_audio_stats(video_path)
        if mp4.get("has_audio_track"):
            return True

        ffmpeg = resolve_ffmpeg()
        try:
            result = subprocess.run(
                [ffmpeg, "-hide_banner", "-i", video_path],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            probe = (result.stderr or b"").decode(errors="ignore")
            for line in probe.splitlines():
                if "Audio:" in line or ": Audio:" in line:
                    return True
            return False
        except Exception:
            return bool(mp4.get("has_audio_track"))

    def _audio_is_weak(self, video_path: str, duration_hint: float = 8.0) -> bool:
        """
        Heuristic: missing track, or very small audio payload for the duration
        (often near-silent ambient with no usable speech).
        """
        stats = self._mp4_audio_stats(video_path)
        if not stats.get("has_audio_track"):
            return True
        # ~4 KB/s is extremely sparse for audible dialogue AAC; normal speech is much higher
        min_bytes = max(12_000, int(duration_hint * 4_000))
        return int(stats.get("audio_bytes") or 0) < min_bytes

    def _synthesize_dialogue_wav(self, text: str, wav_path: str) -> str:
        """
        Create a WAV of the dialogue.

        Backends (first success wins):
          1. espeak-ng / espeak  (typical on WSL/Linux)
          2. Windows SAPI via powershell  (native Windows, or powershell.exe from WSL)
          3. edge-tts CLI if installed
        """
        import shutil

        ensure_parent_dir(wav_path)
        raw = (text or "").replace("\r", " ").replace("\n", " ").strip()
        if not raw:
            raise GenerationFailure("Cannot synthesize empty dialogue.")

        abs_wav = os.path.abspath(wav_path)
        errors: List[str] = []

        # --- 1) edge-tts (most natural; optional: pip install edge-tts) ---
        edge = shutil.which("edge-tts")
        if edge:
            mp3_path = abs_wav + ".mp3"
            cmd = [
                edge,
                "--voice", os.environ.get("EDGE_TTS_VOICE", "en-US-GuyNeural"),
                "--text", raw,
                "--write-media", mp3_path,
            ]
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            if result.returncode == 0 and file_is_usable(mp3_path, min_bytes=100):
                ffmpeg = resolve_ffmpeg()
                conv = subprocess.run(
                    [ffmpeg, "-y", "-i", mp3_path, abs_wav],
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                )
                try:
                    os.remove(mp3_path)
                except OSError:
                    pass
                if conv.returncode == 0 and file_is_usable(wav_path, min_bytes=100):
                    print("  [Audio] TTS backend: edge-tts")
                    return wav_path
            errors.append(f"edge-tts: {(result.stderr or b'').decode(errors='ignore')[-120:]}")

        # --- 2) Windows SAPI (native Windows, or powershell.exe from WSL) ---
        ps_safe = raw.replace("'", "''")
        ps_candidates = []
        if os.name == "nt":
            ps_candidates = ["powershell"]
        else:
            for name in ("powershell.exe", "pwsh.exe"):
                if shutil.which(name):
                    ps_candidates.append(name)
            for p in (
                "/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe",
                "/mnt/c/Program Files/PowerShell/7/pwsh.exe",
            ):
                if os.path.isfile(p):
                    ps_candidates.append(p)

        win_wav = abs_wav
        if _running_on_wsl() and abs_wav.startswith("/mnt/"):
            parts = abs_wav.split("/")
            if len(parts) > 3 and parts[1] == "mnt" and len(parts[2]) == 1:
                win_wav = parts[2].upper() + ":\\" + "\\".join(parts[3:])

        for ps in ps_candidates:
            ps_script = (
                "Add-Type -AssemblyName System.Speech; "
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
                f"$s.SetOutputToWaveFile('{win_wav.replace(chr(39), chr(39)+chr(39))}'); "
                f"$s.Speak('{ps_safe}'); "
                "$s.Dispose();"
            )
            cmd = [ps, "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps_script]
            try:
                result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120)
            except (FileNotFoundError, subprocess.TimeoutExpired) as e:
                errors.append(f"{ps}: {e}")
                continue
            if result.returncode == 0 and file_is_usable(wav_path, min_bytes=100):
                print(f"  [Audio] TTS backend: Windows SAPI via {os.path.basename(ps)}")
                return wav_path
            errors.append(
                f"{ps}: {(result.stderr or b'').decode(errors='ignore')[-120:]}"
            )

        # --- 3) espeak / espeak-ng (WSL / Linux fallback) ---
        for espeak in ("espeak-ng", "espeak"):
            exe = shutil.which(espeak)
            if not exe:
                continue
            cmd = [exe, "-w", abs_wav, "-s", "140", "-a", "150", raw]
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            if result.returncode == 0 and file_is_usable(wav_path, min_bytes=100):
                print(f"  [Audio] TTS backend: {espeak}")
                return wav_path
            errors.append(f"{espeak}: {(result.stderr or b'').decode(errors='ignore')[-120:]}")

        raise GenerationFailure(
            "TTS dialogue synthesis failed. Prefer: pip install edge-tts. "
            "Or on WSL: sudo apt install espeak-ng. On Windows: PowerShell SAPI. "
            f"Details: {' | '.join(errors)[:400]}"
        )

    def _ensure_dialogue_audio(self, video_path: str, clip: Dict[str, Any]) -> str:
        """
        Guarantee audible spoken dialogue on the clip.

        Modes (pipeline_config.dialogue_audio_mode):
          - "replace" (default): video picture + TTS only — no double voice
          - "mix": TTS over quiet native bed (can double if Grok also spoke)
        """
        if not self.config.get("ensure_dialogue_audio", True):
            return video_path

        audio = clip.get("audio_payload") or {}
        dialogue = str(audio.get("dialogue") or "").strip()
        speaker = str(audio.get("speaker") or "").strip()
        if not dialogue or speaker.lower() in ("none", "n/a", ""):
            return video_path

        ffmpeg = resolve_ffmpeg()
        tts_wav = f"{video_path}.dialogue.wav"
        tmp_out = f"{video_path}.voiced.tmp.mp4"
        native_backup = f"{video_path}.native.mp4"
        mode = str(self.config.get("dialogue_audio_mode", "replace")).lower().strip()
        tts_vol = float(self.config.get("dialogue_tts_volume", 1.0))
        bed_vol = float(self.config.get("native_audio_mix_volume", 0.12))

        try:
            import shutil as _shutil

            # Prefer original Grok picture as source if we saved a native backup earlier
            source_video = native_backup if file_is_usable(native_backup, min_bytes=1000) else video_path
            if source_video == video_path and not file_is_usable(native_backup, min_bytes=1000):
                # First time: keep a native backup before we replace audio
                try:
                    _shutil.copy2(video_path, native_backup)
                    source_video = native_backup
                except OSError:
                    pass

            print(
                f"  [Audio] Applying TTS dialogue ({mode}) for speaker={speaker!r}..."
            )
            self._synthesize_dialogue_wav(dialogue, tts_wav)

            has_native = self._video_has_audio(source_video)
            if mode == "mix" and has_native:
                # Quiet ambient under TTS — can still double if native has speech
                filter_complex = (
                    f"[0:a]volume={bed_vol},highpass=f=120,"
                    f"aformat=sample_rates=48000:channel_layouts=stereo[bed];"
                    f"[1:a]volume={tts_vol},aformat=sample_rates=48000:channel_layouts=stereo[voice];"
                    f"[bed][voice]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[aout]"
                )
                cmd = [
                    ffmpeg, "-y",
                    "-i", source_video,
                    "-i", tts_wav,
                    "-filter_complex", filter_complex,
                    "-map", "0:v:0",
                    "-map", "[aout]",
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                    "-c:a", "aac", "-b:a", "192k",
                    "-shortest",
                    "-movflags", "+faststart",
                    tmp_out,
                ]
            else:
                # REPLACE (default): picture from Grok + TTS only — one clear voice, no delay double
                if mode != "replace":
                    print(f"  [Audio] Mode '{mode}' unavailable without native audio; using replace.")
                cmd = [
                    ffmpeg, "-y",
                    "-i", source_video,
                    "-i", tts_wav,
                    "-map", "0:v:0",
                    "-map", "1:a:0",
                    "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                    "-c:a", "aac", "-b:a", "192k",
                    "-shortest",
                    "-movflags", "+faststart",
                    tmp_out,
                ]

            try:
                result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            except FileNotFoundError as e:
                raise GenerationFailure(f"ffmpeg executable not runnable ({ffmpeg}): {e}")

            if result.returncode != 0 or not file_is_usable(tmp_out, min_bytes=50_000):
                err = (result.stderr or b"").decode(errors="ignore")[-500:]
                raise GenerationFailure(f"Failed to mux dialogue TTS onto clip: {err}")

            stats = self._mp4_audio_stats(tmp_out)
            if not stats.get("has_audio_track") and not self._video_has_audio(tmp_out):
                raise GenerationFailure("Dialogue mux produced a file with no audio track.")
            if os.path.getsize(tmp_out) < 100_000:
                raise GenerationFailure(
                    f"Dialogue mux output suspiciously small ({os.path.getsize(tmp_out)} bytes)."
                )

            os.replace(tmp_out, video_path)
            print(f"  [Audio] Dialogue applied ({mode}) -> {video_path}")
            return video_path
        except GenerationFailure as e:
            print(f"  [Audio Warning] {e}. Leaving original clip audio as-is.")
            return video_path
        finally:
            for p in (tts_wav, tmp_out):
                try:
                    if os.path.exists(p):
                        os.remove(p)
                except OSError:
                    pass

    def _extract_last_frame(self, video_path: str, frame_path: str) -> str:
        """Extract the last frame of a clip for Grok image-to-video continuity."""
        ensure_parent_dir(frame_path)
        ffmpeg = resolve_ffmpeg()
        cmd = [
            ffmpeg, "-y",
            "-sseof", "-0.05",
            "-i", video_path,
            "-frames:v", "1",
            "-update", "1",
            frame_path,
        ]
        try:
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        except Exception as e:
            raise GenerationFailure(f"Failed to extract last frame from '{video_path}': {e}")

        if result.returncode != 0 or not os.path.exists(frame_path) or os.path.getsize(frame_path) == 0:
            stderr_tail = result.stderr.decode(errors="ignore")[-400:]
            raise GenerationFailure(
                f"Failed to extract last frame from '{video_path}'. FFmpeg: {stderr_tail}"
            )
        return frame_path

    def _grok_request(self, method: str, url: str, payload: Optional[Dict[str, Any]] = None,
                      timeout: int = 60) -> Dict[str, Any]:
        api_key = os.environ.get("XAI_API_KEY")
        if not api_key:
            raise GenerationFailure("XAI_API_KEY is not set. Required for Grok video generation.")

        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        }
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                raw = response.read().decode()
        except urllib.error.HTTPError as e:
            body = e.read().decode(errors="ignore") if hasattr(e, "read") else ""
            raise GenerationFailure(f"xAI API returned HTTP {e.code} from {url}: {body[:400]}")
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            raise GenerationFailure(f"Could not reach xAI API at '{url}': {e}")

        if not raw:
            return {}
        try:
            return json.loads(raw)
        except json.JSONDecodeError as e:
            raise GenerationFailure(f"xAI API returned non-JSON from {url}: {e}")

    def _grok_submit_generation(self, payload: Dict[str, Any]) -> str:
        result = self._grok_request("POST", f"{XAI_API_BASE}/videos/generations", payload, timeout=120)
        request_id = result.get("request_id")
        if not request_id:
            raise GenerationFailure(f"xAI video generation response missing request_id: {result}")
        return request_id

    def _grok_poll_for_video_url(self, request_id: str, scene_num: int, clip_num: int) -> str:
        poll_interval = int(os.environ.get("GROK_POLL_INTERVAL_SECONDS", "5"))
        timeout_seconds = int(os.environ.get("GROK_TIMEOUT_SECONDS", "900"))
        deadline = time.time() + timeout_seconds

        while time.time() < deadline:
            self._check_shutdown(f"Grok poll Scene {scene_num} Clip {clip_num}")
            data = self._grok_request("GET", f"{XAI_API_BASE}/videos/{request_id}", timeout=60)
            status = data.get("status")

            if status == "done":
                video = data.get("video") or {}
                if not video.get("respect_moderation", True):
                    raise GenerationFailure(
                        f"Scene {scene_num} Clip {clip_num}: Grok video blocked by moderation."
                    )
                url = video.get("url")
                if not url:
                    raise GenerationFailure(
                        f"Scene {scene_num} Clip {clip_num}: Grok returned done status with no video URL."
                    )
                return url

            if status in ("failed", "expired"):
                err = data.get("error") or {}
                detail = err.get("message") or data
                raise GenerationFailure(
                    f"Scene {scene_num} Clip {clip_num}: Grok video job {status}: {detail}"
                )

            progress = data.get("progress")
            progress_note = f" ({progress}%)" if progress is not None else ""
            print(f"  [Grok] Still generating clip {clip_num}{progress_note}...")
            self._interruptible_sleep(poll_interval, f"Grok poll Scene {scene_num} Clip {clip_num}")

        raise GenerationFailure(
            f"Scene {scene_num} Clip {clip_num}: Grok video job timed out after {timeout_seconds}s."
        )

    def _try_resume_grok_download(self, scene_num: int, clip_num: int, output_clip_path: str) -> bool:
        """
        Attempt to finish a previously submitted Grok job without re-billing generation.
        Returns True if the local clip file is ready.
        """
        if file_is_usable(output_clip_path, min_bytes=1024):
            return True

        job = self.state.get("clip_jobs", {}).get(self._clip_job_key(scene_num, clip_num), {})
        request_id = job.get("request_id")
        video_url = job.get("video_url")

        # Prefer re-polling request_id (URL may have expired)
        if request_id:
            print(f"  [Grok] Resuming job request_id={request_id} for Clip {clip_num}...")
            try:
                video_url = self._grok_poll_for_video_url(request_id, scene_num, clip_num)
                self._update_clip_job(
                    scene_num, clip_num,
                    status="pending_download",
                    request_id=request_id,
                    video_url=video_url,
                    path=output_clip_path,
                )
            except GenerationFailure as e:
                print(f"  [Grok] Could not resume request_id={request_id}: {e}")
                # Fall through to stored URL, if any
                if not video_url:
                    return False

        if video_url:
            try:
                self._download_to_path(
                    video_url, output_clip_path,
                    label=f"Scene {scene_num} Clip {clip_num}",
                )
                self._update_clip_job(
                    scene_num, clip_num,
                    status="complete",
                    path=output_clip_path,
                    video_url=video_url,
                    request_id=request_id,
                )
                return True
            except GenerationFailure as e:
                print(f"  [Grok] Resume download failed: {e}")
                self._update_clip_job(
                    scene_num, clip_num,
                    status="download_failed",
                    last_error=str(e),
                    path=output_clip_path,
                )
                return False

        return False

    def _invalidate_clip_file(self, scene_num: int, clip_num: int, output_clip_path: str, reason: str) -> None:
        """Delete a bad local clip and clear job download pointers so it can be regenerated."""
        print(f"  [Grok] Invalidating Clip {clip_num}: {reason}")
        try:
            if os.path.exists(output_clip_path):
                os.remove(output_clip_path)
        except OSError as e:
            print(f"  [Grok Warning] Could not remove '{output_clip_path}': {e}")
        self._update_clip_job(
            scene_num, clip_num,
            status="invalidated",
            path=output_clip_path,
            request_id=None,
            video_url=None,
            last_error=reason,
            qa_approved=False,
        )

    def generate_grok_clip(
        self,
        scene_num: int,
        clip: Dict[str, Any],
        previous_context_id: Any = None,
        force_regenerate: bool = False,
    ) -> tuple:
        """Generate a video clip via xAI Grok Imagine video API (text / image / reference modes)."""
        clip_num = clip["clip_number"]
        continuation_source = clip.get("veo_continuation_source", "none")
        output_clip_path = clip_output_path(scene_num, clip_num)
        ensure_parent_dir(output_clip_path)
        os.makedirs("assets/video", exist_ok=True)

        job = self.state.get("clip_jobs", {}).get(self._clip_job_key(scene_num, clip_num), {})
        prior_qa_failed = job.get("qa_approved") is False

        # Forced regen (QA retry) or previous QA failure on disk
        if force_regenerate or prior_qa_failed:
            if file_is_usable(output_clip_path, min_bytes=1):
                self._invalidate_clip_file(
                    scene_num, clip_num, output_clip_path,
                    reason="force_regenerate" if force_regenerate else "prior_qa_failed",
                )

        # Resume: skip generation when the clip is already on disk WITH audio and prior QA ok
        if file_is_usable(output_clip_path, min_bytes=1024) and not force_regenerate:
            has_audio = self._video_has_audio(output_clip_path)
            qa_ok = job.get("qa_approved") is not False
            dialogue_ready = job.get("dialogue_audio_ensured") is True
            dialogue_text = str(
                ((clip.get("audio_payload") or {}).get("dialogue") or "")
            ).strip()
            needs_dialogue = bool(dialogue_text)
            if (has_audio or not self.config.get("regenerate_silent_clips", True)) and qa_ok:
                # Still ensure TTS dialogue overlay if missing from older renders
                if needs_dialogue and self.config.get("ensure_dialogue_audio", True) and not dialogue_ready:
                    print(f"  [Grok] Existing Clip {clip_num} lacks ensured dialogue audio — mixing TTS voiceover...")
                    self._ensure_dialogue_audio(output_clip_path, clip)
                    self._update_clip_job(
                        scene_num, clip_num,
                        status="complete",
                        path=output_clip_path,
                        has_audio=True,
                        dialogue_audio_ensured=True,
                    )
                    return output_clip_path, output_clip_path
                print(f"  [Grok] Reusing existing Clip {clip_num}: {output_clip_path} (audio={has_audio})")
                self._update_clip_job(
                    scene_num, clip_num,
                    status="complete",
                    path=output_clip_path,
                    has_audio=has_audio,
                )
                return output_clip_path, output_clip_path
            if not has_audio and self.config.get("regenerate_silent_clips", True):
                self._invalidate_clip_file(
                    scene_num, clip_num, output_clip_path,
                    reason="no_audio_track",
                )
            elif not qa_ok:
                self._invalidate_clip_file(
                    scene_num, clip_num, output_clip_path,
                    reason="qa_not_approved",
                )

        # Resume: finish a prior generate+download that failed mid-way (skip if force regen)
        if not force_regenerate and self._try_resume_grok_download(scene_num, clip_num, output_clip_path):
            if self._video_has_audio(output_clip_path) or not self.config.get("regenerate_silent_clips", True):
                print(f"  [Grok] Resumed Clip {clip_num} successfully: {output_clip_path}")
                return output_clip_path, output_clip_path
            print(f"  [Grok] Resumed file for Clip {clip_num} is silent — submitting a new audio-aware job...")
            self._invalidate_clip_file(scene_num, clip_num, output_clip_path, reason="resumed_silent")

        # Decide continuation vs fresh shot BEFORE building the prompt
        prev_path = previous_context_id if isinstance(previous_context_id, str) else None
        use_continuation = self._should_use_last_frame_continuation(clip, continuation_source, prev_path)
        mode = "continue" if use_continuation else "fresh"
        prompt = self._build_video_generation_prompt(clip, mode=mode)

        print(
            f"  [Grok] Generating Clip {clip_num} "
            f"(blueprint_source={continuation_source}, mode={mode})..."
        )
        if not use_continuation and continuation_source not in (None, "", "none"):
            print(
                "  [Grok] Smart continuation: blueprint says extend, but prompt is a cut/new setup — "
                "using fresh generation with character refs instead of last-frame lock."
            )

        audio_payload = clip.get("audio_payload") or {}
        if (audio_payload.get("dialogue") or "").strip():
            print(f"  [Grok] Audio: speaker={audio_payload.get('speaker')!r}, dialogue included in prompt")
        else:
            print("  [Grok] Audio: ambient/Foley only (no dialogue on this clip)")

        throttle_delay = int(os.environ.get("GROK_THROTTLE_DELAY", os.environ.get("VEO_THROTTLE_DELAY", "10")))
        if throttle_delay > 0:
            print(f"  [Grok] Throttling: Cooling down for {throttle_delay}s...")
            self._interruptible_sleep(throttle_delay, f"Grok throttle Scene {scene_num} Clip {clip_num}")

        model_name = self.config.get("model_name", "grok-imagine-video")
        duration = int(self.config.get("duration_seconds", 8))
        # Grok text/image generation accepts 1–15s
        duration = max(1, min(15, duration))

        payload: Dict[str, Any] = {
            "model": model_name,
            "prompt": prompt,
            "duration": duration,
            "aspect_ratio": self.config.get("aspect_ratio", "16:9"),
            "resolution": self.config.get("resolution", "720p"),
        }

        self._check_shutdown(f"Grok generate Scene {scene_num} Clip {clip_num}")
        if use_continuation:
            frame_path = f"assets/video/scene_{scene_num:02d}_clip_{clip_num:02d}_seed_frame.png"
            ensure_parent_dir(frame_path)
            print(f"  [Grok] True continuation: image-to-video from last frame of {prev_path}...")
            self._extract_last_frame(prev_path, frame_path)
            payload["image"] = {"url": self._file_to_data_uri(frame_path)}
        else:
            # Fresh shot: character reference images for look consistency (does not lock first frame)
            # Use original visual + dialogue text for character name matching
            anchor_probe = f"{clip.get('visual_prompt', '')} {(audio_payload.get('speaker') or '')}"
            anchor_path = self._find_character_anchor_path(anchor_probe)
            if not anchor_path:
                # Fall back: any locked character still helps identity
                char_seeds = self.blueprint.get("global_production_variables", {}).get("character_seed_tokens", {})
                for char_key, seed_info in char_seeds.items():
                    local_image_name = seed_info.get("reference_image_placeholder", f"{char_key.lower()}_ref.png")
                    candidate = f"assets/characters/{local_image_name}"
                    if os.path.exists(candidate) and char_key in anchor_probe:
                        anchor_path = candidate
                        break
            if anchor_path:
                print(f"  [Character Anchor] Injecting Grok reference image: {anchor_path}")
                payload["reference_images"] = [{"url": self._file_to_data_uri(anchor_path)}]

        try:
            request_id = self._grok_submit_generation(payload)
            print(f"  [Grok] Submitted job request_id={request_id}")
            # Persist immediately so a crash during poll can still resume
            self._update_clip_job(
                scene_num, clip_num,
                status="submitted",
                request_id=request_id,
                path=output_clip_path,
            )

            video_url = self._grok_poll_for_video_url(request_id, scene_num, clip_num)
            self._update_clip_job(
                scene_num, clip_num,
                status="pending_download",
                request_id=request_id,
                video_url=video_url,
                path=output_clip_path,
            )

            self._download_to_path(
                video_url, output_clip_path,
                label=f"Scene {scene_num} Clip {clip_num}",
            )
            audio_stats = self._mp4_audio_stats(output_clip_path)
            has_audio = bool(audio_stats.get("has_audio_track")) or self._video_has_audio(output_clip_path)
            print(
                f"  [Grok] Clip {clip_num} audio probe: track={has_audio}, "
                f"payload_bytes={audio_stats.get('audio_bytes', 0)}"
            )
            if not has_audio:
                print(f"  [Grok Warning] Clip {clip_num} has NO audio track after download.")
            elif self._audio_is_weak(output_clip_path, duration_hint=float(duration)):
                print(f"  [Grok Warning] Clip {clip_num} audio payload looks weak/near-silent.")

            # Guarantee audible spoken lines (Grok often returns ambient-only beds)
            self._ensure_dialogue_audio(output_clip_path, clip)
            has_audio = self._video_has_audio(output_clip_path)
            self._update_clip_job(
                scene_num, clip_num,
                status="complete",
                request_id=request_id,
                video_url=video_url,
                path=output_clip_path,
                has_audio=has_audio,
                dialogue_audio_ensured=True,
                audio_bytes=audio_stats.get("audio_bytes"),
            )
        except (PipelineInterrupted, KeyboardInterrupt):
            # Keep submitted request_id in clip_jobs so resume can re-poll/download
            self._update_clip_job(
                scene_num, clip_num,
                status="interrupted",
                path=output_clip_path,
                last_error="interrupted_by_user",
            )
            raise
        except GenerationFailure as e:
            self._update_clip_job(
                scene_num, clip_num,
                status="failed",
                last_error=str(e),
                path=output_clip_path,
            )
            raise
        except Exception as e:
            err = f"Scene {scene_num} Clip {clip_num}: Grok generation failed: {e}"
            self._update_clip_job(
                scene_num, clip_num,
                status="failed",
                last_error=err,
                path=output_clip_path,
            )
            raise GenerationFailure(err)

        if not file_is_usable(output_clip_path, min_bytes=1024):
            raise GenerationFailure(
                f"Scene {scene_num} Clip {clip_num}: Grok output file is missing or zero-length."
            )

        # Context for the next clip is the local path (used for last-frame continuity)
        return output_clip_path, output_clip_path

    def generate_veo_clip(self, scene_num: int, clip: Dict[str, Any], previous_context_id: Any = None) -> tuple:
        """Executes Google GenAI Veo 3.1 SDK requests using model paths tracking."""
        clip_num = clip["clip_number"]
        prompt = clip["visual_prompt"]
        neg_prompt = clip["negative_prompt"]
        continuation_source = clip.get("veo_continuation_source", "none")

        output_clip_path = clip_output_path(scene_num, clip_num)
        ensure_parent_dir(output_clip_path)
        os.makedirs("assets/video", exist_ok=True)

        if file_is_usable(output_clip_path, min_bytes=1024):
            print(f"  [Veo 3.1] Reusing existing Clip {clip_num}: {output_clip_path}")
            ctx = self.state.get("clip_context_ids", {}).get(
                self._clip_job_key(scene_num, clip_num), output_clip_path
            )
            return output_clip_path, ctx

        print(f"  [Veo 3.1] Generating Clip {clip_num} (Source: {continuation_source})...")

        if not self.client:
            raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: Google GenAI client is not initialized.")

        if types is None:
            raise GenerationFailure(f"Scene {scene_num} Clip {clip_num}: google.genai.types is unavailable.")

        throttle_delay = int(os.environ.get("VEO_THROTTLE_DELAY", "30"))
        if throttle_delay > 0:
            print(f"  [Veo 3.1] Throttling: Cooling down for {throttle_delay}s...")
            time.sleep(throttle_delay)

        model_name = self.config.get("model_name", "veo-3.1-fast-generate-preview")

        generation_config = types.GenerateVideosConfig(
            aspect_ratio=self.config.get("aspect_ratio", "16:9"),
            duration_seconds=self.config.get("duration_seconds", 8)
        )

        if self.config.get("use_video_audio_for_music", False):
            if "with audio" not in prompt.lower():
                prompt = f"{prompt}, high quality atmospheric stereo background cinematic sound and matching environment audio"

        try:
            # 🎬 IF A CAMERA JUMP CUT HAPPENS, ATTACH STAGE 0 PORTRAIT AS BASELINE LOCK
            if continuation_source == "none" or not previous_context_id or str(previous_context_id).startswith("ctx_mock_"):
                image_reference = None
                
                # Scan prompt context to verify which primary character token is under focus
                char_seeds = self.blueprint.get("global_production_variables", {}).get("character_seed_tokens", {})
                for char_key, seed_info in char_seeds.items():
                    if char_key in prompt:
                        local_image_name = seed_info.get("reference_image_placeholder", f"{char_key.lower()}_ref.png")
                        local_image_path = f"assets/characters/{local_image_name}"
                        
                        if os.path.exists(local_image_path):
                            print(f"  [Character Anchor] Found upfront character map image for {char_key}. Injecting into canvas input layer...")
                            image_reference = self.client.files.upload(file=local_image_path)
                            break

                if image_reference:
                    operation = self.client.models.generate_videos(
                        model=model_name,
                        prompt=prompt,
                        video=image_reference, # Forces face/outfit alignment consistency acrosscuts
                        config=generation_config
                    )
                else:
                    operation = self.client.models.generate_videos(
                        model=model_name,
                        prompt=prompt,
                        config=generation_config
                    )
            
            # 🔄 CONTINUOUS EXTENSION MODE
            else:
                operation = self.client.models.generate_videos(
                    model=model_name,
                    prompt=prompt,
                    video=previous_context_id,
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

    def generate_video_clip(
        self,
        scene_num: int,
        clip: Dict[str, Any],
        previous_context_id: Any = None,
        force_regenerate: bool = False,
    ) -> tuple:
        """Dispatch video generation to Grok (default) or Veo based on pipeline_config.video_provider."""
        provider = str(self.config.get("video_provider", "grok")).lower().strip()
        if provider in ("grok", "xai", "grok-imagine", "imagine"):
            return self.generate_grok_clip(
                scene_num, clip, previous_context_id, force_regenerate=force_regenerate
            )
        if provider in ("veo", "google", "gemini"):
            return self.generate_veo_clip(scene_num, clip, previous_context_id)
        raise GenerationFailure(
            f"Unknown video_provider '{provider}'. Use 'grok' (default) or 'veo'."
        )

    def _qa_evaluation_prompt(self, visual_prompt: str) -> str:
        return (
            f"You are a film continuity QA reviewer. These still frames were sampled in order "
            f"from a generated video clip. Verify the clip matches this visual description:\n"
            f"'{visual_prompt}'\n\n"
            f"Judge lighting, camera motion, character/actor consistency, wardrobe, and whether "
            f"the sequence matches the intended action.\n\n"
            f"Respond with ONLY a single JSON object (no markdown fences):\n"
            f"{{\n"
            f"  \"approved\": true or false,\n"
            f"  \"critique\": \"detailed observations about lighting, camera drift, and actor consistency\"\n"
            f"}}"
        )

    def _probe_video_duration_seconds(self, video_path: str) -> float:
        cmd = [
            "ffprobe", "-v", "error",
            "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1",
            video_path,
        ]
        try:
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            if result.returncode == 0 and result.stdout.strip():
                return max(0.1, float(result.stdout.strip()))
        except Exception:
            pass
        # Fallback when ffprobe is unavailable
        return float(self.config.get("duration_seconds", 8))

    def _extract_qa_frames(self, video_path: str, frame_count: int = 4) -> List[str]:
        """Sample evenly spaced JPEG frames from a clip for vision QA."""
        os.makedirs("assets/video", exist_ok=True)
        base = os.path.splitext(os.path.basename(video_path))[0]
        duration = self._probe_video_duration_seconds(video_path)
        frame_count = max(1, min(int(frame_count), 8))

        # Avoid the absolute end frame which can be black/corrupt on some encodes
        usable = max(0.05, duration - 0.05)
        if frame_count == 1:
            timestamps = [usable * 0.5]
        else:
            timestamps = [usable * (i / (frame_count - 1)) for i in range(frame_count)]

        frame_paths: List[str] = []
        for idx, ts in enumerate(timestamps, start=1):
            out_path = f"assets/video/{base}_qa_frame_{idx:02d}.jpg"
            ensure_parent_dir(out_path)
            ffmpeg = resolve_ffmpeg()
            cmd = [
                ffmpeg, "-y",
                "-ss", f"{ts:.3f}",
                "-i", video_path,
                "-frames:v", "1",
                "-q:v", "2",
                out_path,
            ]
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            if result.returncode == 0 and os.path.exists(out_path) and os.path.getsize(out_path) > 0:
                frame_paths.append(out_path)
            else:
                print(f"  [QA Warning] Failed to extract frame {idx} at t={ts:.2f}s")

        if not frame_paths:
            raise GenerationFailure(f"Could not extract any QA frames from '{video_path}'")
        return frame_paths

    def _parse_qa_json(self, text: str) -> Dict[str, Any]:
        """Parse model QA JSON, tolerating optional markdown fences."""
        cleaned = (text or "").strip()
        if cleaned.startswith("```"):
            lines = cleaned.splitlines()
            # Drop opening fence and optional trailing fence
            if lines and lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip().startswith("```"):
                lines = lines[:-1]
            cleaned = "\n".join(lines).strip()

        try:
            return json.loads(cleaned)
        except json.JSONDecodeError:
            # Best-effort extract of first {...} block
            start = cleaned.find("{")
            end = cleaned.rfind("}")
            if start >= 0 and end > start:
                return json.loads(cleaned[start:end + 1])
            raise

    def _extract_response_text(self, result: Dict[str, Any]) -> str:
        """Pull assistant text out of xAI /v1/responses or chat.completions payloads."""
        if not isinstance(result, dict):
            return str(result)

        # chat.completions shape
        choices = result.get("choices")
        if isinstance(choices, list) and choices:
            message = choices[0].get("message") or {}
            content = message.get("content")
            if isinstance(content, str):
                return content
            if isinstance(content, list):
                parts = []
                for part in content:
                    if isinstance(part, dict) and part.get("type") in ("text", "output_text"):
                        parts.append(part.get("text") or part.get("output_text") or "")
                if parts:
                    return "\n".join(parts)

        # responses API shape: output[].content[].text
        if isinstance(result.get("output_text"), str) and result["output_text"].strip():
            return result["output_text"]

        output = result.get("output")
        if isinstance(output, list):
            texts: List[str] = []
            for item in output:
                if not isinstance(item, dict):
                    continue
                content = item.get("content")
                if isinstance(content, list):
                    for part in content:
                        if not isinstance(part, dict):
                            continue
                        if part.get("type") in ("output_text", "text") and part.get("text"):
                            texts.append(part["text"])
                elif isinstance(content, str):
                    texts.append(content)
            if texts:
                return "\n".join(texts)

        return json.dumps(result)

    def run_grok_qa(self, video_path: str, visual_prompt: str) -> bool:
        """Critique a generated clip with Grok vision using sampled frames."""
        if not os.environ.get("XAI_API_KEY"):
            print("  [Grok QA Warning] XAI_API_KEY not set. Bypassing QA safely.")
            return True

        print(f"  [Grok QA] Critiquing generated clip: {video_path}...")
        frame_paths: List[str] = []
        try:
            frame_count = int(self.config.get("qa_frame_count", 4))
            frame_paths = self._extract_qa_frames(video_path, frame_count=frame_count)
            print(f"  [Grok QA] Sampled {len(frame_paths)} frame(s) for vision review.")

            content: List[Dict[str, Any]] = []
            for path in frame_paths:
                content.append({
                    "type": "input_image",
                    "image_url": self._file_to_data_uri(path),
                    "detail": "high",
                })
            content.append({
                "type": "input_text",
                "text": self._qa_evaluation_prompt(visual_prompt),
            })

            model_name = self.config.get("qa_model_name", "grok-4.5")
            payload = {
                "model": model_name,
                "input": [
                    {
                        "role": "user",
                        "content": content,
                    }
                ],
            }
            result = self._grok_request("POST", f"{XAI_API_BASE}/responses", payload, timeout=180)
            text = self._extract_response_text(result)
            parsed = self._parse_qa_json(text)
            approved = bool(parsed.get("approved", True))
            print(f"  [Grok QA] Result: Approved={approved}, Critique={parsed.get('critique')}")
            return approved
        except Exception as e:
            print(f"  [Grok QA Warning] Evaluation failed: {e}. Bypassing safely.")
            return True
        finally:
            for p in frame_paths:
                try:
                    if os.path.exists(p):
                        os.remove(p)
                except OSError:
                    pass

    def run_gemini_qa(self, video_path: str, visual_prompt: str) -> bool:
        """Critique a generated clip with Gemini multimodal video understanding."""
        if not self.client:
            print("  [Gemini QA Warning] Gemini client not configured. Bypassing QA safely.")
            return True

        print(f"  [Gemini QA] Critiquing generated clip: {video_path}...")
        try:
            video_file = self.client.files.upload(file=video_path)

            while video_file.state.name == "PROCESSING":
                time.sleep(2)
                video_file = self.client.files.get(name=video_file.name)

            if video_file.state.name != "ACTIVE":
                raise ValueError(f"File upload entered state: {video_file.state.name}")

            gemini_model = "gemini-2.5-flash"
            configured_qa_model = str(self.config.get("qa_model_name", ""))
            if "gemini" in configured_qa_model.lower():
                gemini_model = configured_qa_model

            response = self.client.models.generate_content(
                model=gemini_model,
                contents=[video_file, self._qa_evaluation_prompt(visual_prompt)],
                config=types.GenerateContentConfig(
                    response_mime_type="application/json"
                ) if types else None,
            )

            result = json.loads(response.text)
            print(f"  [Gemini QA] Result: Approved={result.get('approved')}, Critique={result.get('critique')}")
            return result.get("approved", True)
        except Exception as e:
            print(f"  [Gemini QA Warning] Evaluation failed: {e}. Bypassing safely.")
            return True

    def run_clip_qa(self, video_path: str, visual_prompt: str) -> bool:
        """Dispatch clip QA to Grok (default) or Gemini based on pipeline_config.qa_provider."""
        provider = str(self.config.get("qa_provider", "grok")).lower().strip()
        if provider in ("grok", "xai"):
            return self.run_grok_qa(video_path, visual_prompt)
        if provider in ("gemini", "google"):
            return self.run_gemini_qa(video_path, visual_prompt)
        if provider in ("none", "off", "skip"):
            print("  [QA] Skipped by config (qa_provider=none).")
            return True
        print(f"  [QA Warning] Unknown qa_provider '{provider}'. Bypassing safely.")
        return True

    def _normalize_clip_for_concat(self, clip_path: str, normalized_path: str) -> str:
        """
        Re-encode a clip to H.264 + AAC so concat is reliable and silent clips get a silent track.
        Always boosts audio so soft Grok beds are still audible in the composite.
        """
        ensure_parent_dir(normalized_path)
        ffmpeg = resolve_ffmpeg()
        has_audio = self._video_has_audio(clip_path)
        # Loudness boost for soft ambient / quiet native beds
        gain_db = float(self.config.get("composite_audio_gain_db", 9.0))

        if has_audio:
            cmd = [
                ffmpeg, "-y",
                "-i", clip_path,
                "-vf", "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2,fps=24,format=yuv420p",
                "-af", f"aresample=48000,aformat=channel_layouts=stereo,volume={gain_db}dB",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                "-c:a", "aac", "-b:a", "192k",
                "-ar", "48000", "-ac", "2",
                "-movflags", "+faststart",
                normalized_path,
            ]
        else:
            # Attach silent stereo audio so progressive concat never drops the track
            print(f"  [FFmpeg] Clip has no audio — adding silent track for mux: {clip_path}")
            cmd = [
                ffmpeg, "-y",
                "-i", clip_path,
                "-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
                "-vf", "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2,fps=24,format=yuv420p",
                "-map", "0:v:0",
                "-map", "1:a:0",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                "-c:a", "aac", "-b:a", "192k",
                "-shortest",
                "-ar", "48000", "-ac", "2",
                "-movflags", "+faststart",
                normalized_path,
            ]

        result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if result.returncode != 0 or not file_is_usable(normalized_path, min_bytes=1024):
            stderr_tail = result.stderr.decode(errors="ignore")[-500:]
            raise GenerationFailure(
                f"Failed to normalize clip '{clip_path}' for scene concat. FFmpeg: {stderr_tail}"
            )
        if not self._video_has_audio(normalized_path):
            raise GenerationFailure(
                f"Normalized clip lost audio track: '{normalized_path}' from '{clip_path}'"
            )
        return normalized_path

    def mix_scene_assets(self, scene_num: int, clip_paths: List[str], music_path: Optional[str],
                         force: bool = False) -> str:
        """Stitches clips (and optional music bed) into the scene composite using FFmpeg."""
        output_scene_path = composite_output_path(scene_num)
        ensure_parent_dir(output_scene_path)
        os.makedirs("assets/scenes", exist_ok=True)

        # Only reuse a finished composite when not force-rebuilding (progressive updates use force=True)
        if (
            not force
            and file_is_usable(output_scene_path, min_bytes=1024)
            and self._video_has_audio(output_scene_path)
        ):
            print(f"  [FFmpeg] Reusing existing Scene {scene_num} composite: {output_scene_path}")
            return output_scene_path

        for p in clip_paths:
            if not file_is_usable(p, min_bytes=1024):
                raise GenerationFailure(
                    f"Scene {scene_num}: cannot mux — missing or empty clip '{p}'. "
                    f"Re-run the script to resume generation."
                )

        # Ensure dialogue TTS is on source clips before stitching (Grok audio is often ambient-only)
        scene_obj = None
        for s in self.blueprint.get("scenes", []):
            if s.get("scene_number") == scene_num:
                scene_obj = s
                break
        if scene_obj and self.config.get("ensure_dialogue_audio", True):
            clips_by_num = {
                c.get("clip_number"): c for c in (scene_obj.get("veo_clips") or [])
            }
            for p in clip_paths:
                # path like assets/video/scene_01_clip_03.mp4
                m = re.search(r"clip_(\d+)\.mp4$", p.replace("\\", "/"))
                if not m:
                    continue
                c_num = int(m.group(1))
                clip_meta = clips_by_num.get(c_num)
                if not clip_meta:
                    continue
                dlg = str(((clip_meta.get("audio_payload") or {}).get("dialogue") or "")).strip()
                if dlg:
                    job = self.state.get("clip_jobs", {}).get(self._clip_job_key(scene_num, c_num), {})
                    if not job.get("dialogue_audio_ensured"):
                        print(f"  [Audio] Ensuring dialogue on source clip before composite: {p}")
                        self._ensure_dialogue_audio(p, clip_meta)
                        self._update_clip_job(
                            scene_num, c_num,
                            path=p,
                            dialogue_audio_ensured=True,
                            has_audio=True,
                        )

        ffmpeg = resolve_ffmpeg()
        work_dir = f"assets/scenes/_work_scene_{scene_num:02d}"
        os.makedirs(work_dir, exist_ok=True)

        # Normalize each clip so concat keeps a continuous, loud enough audio track
        normalized_paths: List[str] = []
        for idx, p in enumerate(clip_paths, start=1):
            norm = os.path.join(work_dir, f"norm_clip_{idx:02d}.mp4")
            # Always rebuild norms on force so audio fixes apply
            if force and os.path.exists(norm):
                try:
                    os.remove(norm)
                except OSError:
                    pass
            self._normalize_clip_for_concat(p, norm)
            normalized_paths.append(norm)

        concat_list = f"assets/scenes/concat_list_scene_{scene_num}.txt"
        ensure_parent_dir(concat_list)
        with open(concat_list, "w", encoding="utf-8") as f:
            for p in normalized_paths:
                # Paths relative to concat list location (assets/scenes/)
                rel = os.path.relpath(p, start="assets/scenes").replace("\\", "/")
                f.write(f"file '{rel}'\n")

        tmp_out = f"{output_scene_path}.tmp.mp4"
        # Full re-encode (not stream-copy) so audio is never dropped by player/demux quirks
        if music_path and file_is_usable(music_path, min_bytes=1):
            ffmpeg_cmd = [
                ffmpeg, "-y",
                "-f", "concat", "-safe", "0", "-i", concat_list,
                "-i", music_path,
                "-filter_complex",
                "[0:a]aformat=sample_rates=48000:channel_layouts=stereo[va];"
                "[1:a]aformat=sample_rates=48000:channel_layouts=stereo,volume=0.25[m];"
                "[va][m]amix=inputs=2:duration=first:dropout_transition=2:normalize=0,"
                "loudnorm=I=-14:TP=-1.5:LRA=11[aout]",
                "-map", "0:v:0",
                "-map", "[aout]",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                "-c:a", "aac", "-b:a", "192k",
                "-ar", "48000", "-ac", "2",
                "-shortest",
                "-movflags", "+faststart",
                tmp_out,
            ]
        else:
            ffmpeg_cmd = [
                ffmpeg, "-y",
                "-f", "concat", "-safe", "0", "-i", concat_list,
                "-map", "0:v:0",
                "-map", "0:a:0?",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
                "-c:a", "aac", "-b:a", "192k",
                "-af", "loudnorm=I=-14:TP=-1.5:LRA=11",
                "-ar", "48000", "-ac", "2",
                "-movflags", "+faststart",
                tmp_out,
            ]

        print(
            f"  [FFmpeg] Muxing Scene {scene_num} composite "
            f"({len(clip_paths)} clip(s)){' [force rebuild]' if force else ''}..."
        )
        try:
            result = subprocess.run(ffmpeg_cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        except Exception as e:
            raise GenerationFailure(f"Scene {scene_num}: local FFmpeg execution error while muxing: {e}")

        if result.returncode != 0 or not file_is_usable(tmp_out, min_bytes=1024):
            stderr_tail = result.stderr.decode(errors="ignore")[-500:]
            raise GenerationFailure(f"Scene {scene_num}: FFmpeg muxing failed. Last FFmpeg output: {stderr_tail}")

        if not self._video_has_audio(tmp_out):
            # Fail loudly — silent composites are almost always a mux bug
            try:
                os.remove(tmp_out)
            except OSError:
                pass
            raise GenerationFailure(
                f"Scene {scene_num}: composite was written without an audio track. "
                f"Check source clips have audio and ffmpeg is working."
            )

        os.replace(tmp_out, output_scene_path)
        has_audio = self._video_has_audio(output_scene_path)
        audio_stats = self._mp4_audio_stats(output_scene_path)
        print(
            f"  [FFmpeg] Scene composite ready: {output_scene_path} "
            f"(clips={len(clip_paths)}, audio={has_audio}, "
            f"audio_bytes={audio_stats.get('audio_bytes', 0)})"
        )
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

    def _handle_generation_failure(self, scene_num: int, generated_files: List[str],
                                   error: GenerationFailure, clip_paths: Optional[List[str]] = None,
                                   music_path: Optional[str] = None):
        print("\n" + "=" * 75)
        print(f"[FATAL] Generation failed for Scene {scene_num}. Halting pipeline.")
        print(f"[FATAL] Reason: {error}")
        print("[RESUME] Progress was saved. Re-run the script to continue from this point.")
        print("         Existing usable clips will be skipped; pending downloads will be retried.")
        print("=" * 75)

        removed = self._remove_zero_length_files(generated_files)
        if removed:
            print(f"[Cleanup] Removed {len(removed)} zero-length file(s).")

        s_key = str(scene_num)
        self.state["scenes_completed"][s_key] = False
        # Keep partial scene_assets so resume knows what was finished
        partial_clips = [p for p in (clip_paths or []) if file_is_usable(p, min_bytes=1024)]
        self.state["scene_assets"][s_key] = {
            "video_clips": partial_clips,
            "music_bed": music_path if file_is_usable(music_path, min_bytes=1) else None,
            "composite": None,
            "partial": True,
            "last_error": str(error),
        }
        self.save_state()
        sys.exit(1)

    def process_scene(self, scene: Dict[str, Any]) -> bool:
        s_num = scene["scene_number"]
        print(f"\n==================== PROCESSING SCENE {s_num} ====================")
        self.ensure_asset_directories()
        self._active_scene_num = s_num
        self._active_clip_num = None
        self._check_shutdown(f"start Scene {s_num}")

        generated_files: List[str] = []
        clip_paths: List[str] = []
        music_path: Optional[str] = None
        composite_scene_path: Optional[str] = None

        try:
            if not self.config.get("use_video_audio_for_music", False):
                generated_files.append(music_output_path(s_num))
                music_path = self.generate_suno_music(scene)
            else:
                print(f"  [Pipeline Config] Bypassing Suno API Generation. Utilizing native video generator background music.")

            previous_context_id = None
            max_qa_retries = int(self.config.get("qa_max_retries", 2))
            qa_retry_on_fail = bool(self.config.get("qa_retry_on_fail", True))

            for clip in scene.get("veo_clips", []):
                self._check_shutdown(f"Scene {s_num} before next clip")
                c_num = clip["clip_number"]
                self._active_clip_num = c_num
                clip_state_key = f"{s_num}_{c_num}"
                out_path = clip_output_path(s_num, c_num)
                generated_files.append(out_path)

                # Prefer previous clip's local file for continuity when resuming mid-scene
                seed_ctx = previous_context_id
                if not seed_ctx and c_num > 1:
                    prev_path = clip_output_path(s_num, c_num - 1)
                    if file_is_usable(prev_path, min_bytes=1024):
                        seed_ctx = prev_path
                # Do not pass stale Veo operation IDs as Grok seeds
                if seed_ctx and isinstance(seed_ctx, str) and (
                    seed_ctx.startswith("models/") or seed_ctx.startswith("ctx_mock_")
                ):
                    seed_ctx = None

                attempts = max_qa_retries + 1 if qa_retry_on_fail else 1
                clip_path = None
                context_id = None
                qa_passed = False

                for attempt in range(1, attempts + 1):
                    force = attempt > 1
                    if force:
                        print(
                            f"  [QA Retry] Clip {c_num}: attempt {attempt}/{attempts} "
                            f"(regenerating after QA rejection)..."
                        )
                    clip_path, context_id = self.generate_video_clip(
                        s_num, clip, seed_ctx, force_regenerate=force
                    )

                    qa_passed = self.run_clip_qa(clip_path, clip["visual_prompt"])
                    self._update_clip_job(
                        s_num, c_num,
                        path=clip_path,
                        qa_approved=bool(qa_passed),
                        qa_attempt=attempt,
                        status="complete" if qa_passed else "qa_rejected",
                    )
                    if qa_passed:
                        break
                    if attempt < attempts:
                        self._invalidate_clip_file(
                            s_num, c_num, clip_path,
                            reason=f"qa_rejected_attempt_{attempt}",
                        )
                    else:
                        print(
                            f"  [QA Warning] Clip {c_num} still rejected after {attempts} attempt(s); "
                            f"keeping last render and continuing."
                        )

                clip_paths.append(clip_path)
                self.state["clip_context_ids"][clip_state_key] = context_id

                # Progressive scene merge after each accepted (or final) clip
                if self.config.get("merge_scene_after_each_clip", True):
                    try:
                        composite_scene_path = self.mix_scene_assets(
                            s_num, list(clip_paths), music_path, force=True
                        )
                        print(
                            f"  [Scene] Progressive merge after clip {c_num}: "
                            f"{composite_scene_path} ({len(clip_paths)} clip(s))"
                        )
                    except GenerationFailure as mix_err:
                        # Don't lose a paid clip if only mux failed — save and re-raise
                        print(f"  [Scene] Progressive merge failed: {mix_err}")
                        raise

                self.state["scene_assets"][str(s_num)] = {
                    "video_clips": list(clip_paths),
                    "music_bed": music_path,
                    "composite": composite_scene_path,
                    "partial": True,
                    "clips_merged": len(clip_paths),
                }
                self.save_state()
                previous_context_id = context_id

            # Final mux
            generated_files.append(composite_output_path(s_num))
            composite_scene_path = self.mix_scene_assets(
                s_num, clip_paths, music_path, force=True
            )

        except PipelineInterrupted:
            # Preserve partial scene progress, then re-raise for outer graceful_stop
            self.state.setdefault("scenes_completed", {})[str(s_num)] = False
            self.state.setdefault("scene_assets", {})[str(s_num)] = {
                "video_clips": [p for p in clip_paths if file_is_usable(p, min_bytes=1024)],
                "music_bed": music_path if file_is_usable(music_path, min_bytes=1) else None,
                "composite": composite_scene_path if file_is_usable(composite_scene_path, min_bytes=1024) else None,
                "partial": True,
                "clips_merged": len(clip_paths),
                "last_error": "interrupted_by_user",
            }
            self.save_state()
            raise
        except GenerationFailure as e:
            self._handle_generation_failure(
                s_num, generated_files, e,
                clip_paths=clip_paths, music_path=music_path,
            )

        self.state["scene_assets"][str(s_num)] = {
            "video_clips": clip_paths,
            "music_bed": music_path,
            "composite": composite_scene_path,
            "partial": False,
            "clips_merged": len(clip_paths),
        }
        self.save_state()
        self._active_clip_num = None
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
        ensure_parent_dir(output_movie_path)
        self.ensure_asset_directories()

        scene_files = []
        for s in scenes:
            s_num = s["scene_number"]
            asset_info = self.state["scene_assets"].get(str(s_num))
            composite = None
            if asset_info and asset_info.get("composite"):
                composite = asset_info["composite"]
            else:
                # Fall back to on-disk composite if state is incomplete
                candidate = composite_output_path(s_num)
                if file_is_usable(candidate, min_bytes=1024):
                    composite = candidate
            if composite and file_is_usable(composite, min_bytes=1024):
                scene_files.append(composite)

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
        self.ensure_asset_directories()

        try:
            # Fire Stage 0 Design Sequence prior to running film operational scene timelines
            if not self.state.get("characters_designed", False):
                self.pre_production_character_design()

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
                self._check_shutdown("pipeline scene loop")
                scene = scenes[current_idx]
                s_num = scene["scene_number"]

                self.process_scene(scene)

                print(f"\n*** INTERACTIVE GATE: REVIEW SCENE {s_num}/{total_scenes} ***")
                user_action = ""
                while user_action not in ["A", "F", "R", "Q"]:
                    self._check_shutdown("interactive review gate")
                    try:
                        user_action = input(
                            "Review Scene [X]. [A] Approve, [F] Forward Feedback, "
                            "[R] Retroactive Rollback, [Q] Quit: "
                        ).strip().upper()
                    except EOFError as e:
                        raise PipelineInterrupted("EOF on interactive input") from e

                if user_action == "A":
                    print(f"[Success] Approved Scene {s_num}.")
                    self.state["scenes_completed"][str(s_num)] = True
                    current_idx += 1
                    self.state["current_scene_index"] = current_idx
                    self.save_state()
                elif user_action == "F":
                    feedback = input("\nEnter your forward feedback modifier: ").strip()
                    scope = input(
                        "Select scoping - [L] Local Clip, [C] Cascading Scene, [G] Global Forward: "
                    ).strip().upper()
                    self.state["scenes_completed"][str(s_num)] = False
                    self.save_state()
                elif user_action == "R":
                    feedback = input("\nEnter retroactive feedback: ").strip()
                    target_scene = 0
                    while target_scene < 1 or target_scene > s_num:
                        try:
                            target_scene = int(
                                input(
                                    f"Enter the past scene number where this modification "
                                    f"should begin (1 to {s_num}): "
                                )
                            )
                        except ValueError:
                            pass
                    self.backpropagate_retroactive_feedback(s_num, target_scene, feedback)
                    current_idx = self.state["current_scene_index"]
                elif user_action == "Q":
                    self.graceful_stop("Quit requested from interactive gate")

            self.run_mastering()
        except PipelineInterrupted as e:
            self.graceful_stop(str(e) or "Interrupted by user")
        except KeyboardInterrupt:
            self.graceful_stop("Interrupted by user (KeyboardInterrupt)")


if __name__ == "__main__":
    print("=========================================================================")
    print("         AGENTIC GENERATION SCRIPT ENGINE (V9.2) - RUNNING               ")
    print("         Smart cuts + QA retry  |  Ctrl+C saves state & resumes          ")
    print("=========================================================================")
    engine = None
    try:
        engine = AgenticGenerationEngine()
        engine.run_pipeline()
    except SystemExit:
        raise
    except KeyboardInterrupt:
        if engine is not None:
            engine.graceful_stop("Interrupted by user (KeyboardInterrupt)")
        print("\n[Shutdown] Interrupted before engine init. Nothing to save.")
        raise SystemExit(130)
    except PipelineInterrupted as e:
        if engine is not None:
            engine.graceful_stop(str(e))
        raise SystemExit(130)