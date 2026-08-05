# UITestingBranch — remaining readiness tests

- [x] **1. Finish/seed stage2 under fakes → Film + Generate**
  - Root cause: empty project config (`model_name` / `planning_model_name` missing) → Stage2 failed with “no model selected”.
  - Fix for tests: `PUT /api/projects/{id}/config` with catalog models, then `POST /api/jobs/stage2`.
  - Verified: `stage2_ready=true`, **5 scenes · 12 clips**, blueprint at `blueprint.clips.grok.json`.
  - Note: Strip **Film** uses `CanScenes` (= stage2 ready + clips &gt; 0). **Generate** still needs cast voice + locked image (item 2).
- [ ] **2. Fake cast voice/image locks → CanScenes / Generate**
- [ ] **3. S5 double-submit when Generate enables**
- [ ] **4. Longer wait for S3 strip on Home**
- [ ] **5. Cost Agree + length after sign-off with UI activate**

## Stage2 seed recipe (fakes)

```bash
# After create project + import-fountain + screenplay/sign-off:
curl -X PUT "$API/api/projects/{id}/config" -H "Content-Type: application/json" \
  -d '{
    "model_name": "grok-imagine-video",
    "planning_model_name": "grok-4.5",
    "chat_model_name": "grok-4.5",
    "image_model_name": "grok-imagine-image",
    "vision_model_name": "grok-4.5"
  }'
curl -X POST "$API/api/jobs/stage2" -d '{"projectId":"{id}"}'
# Poll until GET /api/stage2-status → stage2_ready true
```
