/** Server-row shapes (snake_case) for ptm_* tables. */

export type DbProjectStage = "source" | "screenplay" | "storyboard" | "film";
export type DbProjectStatus = "setup" | "sample" | "generating" | "ready";
export type DbWizardStep = "cast" | "voice" | "estimate" | "confirm" | "done";
export type DbSourceKind = "classic" | "custom";

export type DbLockKind =
  | "project"
  | "screenplay"
  | "cast"
  | "voice"
  | "estimate"
  | "generate"
  | "render";

/** Content freezes — once true, UI should require explicit unlock/re-open. */
export type ProjectContentLocks = {
  screenplayLocked: boolean;
  castLocked: boolean;
  voiceLocked: boolean;
  estimateLocked: boolean;
  pictureLocked: boolean;
  generationLocked: boolean;
};

export type DbProjectRow = {
  id: string;
  user_id: string;
  title: string;
  author: string;
  genre: string;
  source_kind: DbSourceKind;
  classic_id: string | null;
  source_text: string;
  screenplay: string;
  stage: DbProjectStage;
  status: DbProjectStatus;
  wizard_step: DbWizardStep;
  progress: number;
  progress_label: string;
  unlocked_shots: number;
  stars: number;
  casting_confirmed: boolean;
  screenplay_locked: boolean;
  cast_locked: boolean;
  voice_locked: boolean;
  estimate_locked: boolean;
  picture_locked: boolean;
  generation_locked: boolean;
  estimate_json: unknown;
  voice_json: unknown;
  stitched_vo_media_id: string | null;
  output_media_id: string | null;
  created_at: string;
  updated_at: string;
};

export type DbSceneRow = {
  id: string;
  project_id: string;
  scene_number: number;
  heading: string;
  visual: string;
  dialogue: string | null;
  duration_sec: number;
  palette: string | null;
  plate_media_id: string | null;
  render_media_id: string | null;
  locked: boolean;
  sort_order: number;
  created_at: string;
  updated_at: string;
};

export type DbCastRow = {
  id: string;
  project_id: string;
  role_in_story: string;
  display_name: string;
  relation: string;
  selected: boolean;
  notes: string | null;
  photo_media_id: string | null;
  sort_order: number;
  created_at: string;
  updated_at: string;
};

export type DbVoiceSampleRow = {
  id: string;
  project_id: string;
  cast_id: string;
  enabled: boolean;
  has_sample: boolean;
  consent: boolean;
  source: "mic" | "upload" | null;
  sample_label: string | null;
  capture_media_id: string | null;
  clone_output_media_id: string | null;
  line_media_id: string | null;
  model_id: string | null;
  created_at: string;
  updated_at: string;
};

export type DbProjectLockRow = {
  id: string;
  project_id: string;
  user_id: string;
  lock_kind: DbLockKind;
  holder_label: string | null;
  acquired_at: string;
  expires_at: string;
  client_token: string | null;
};
