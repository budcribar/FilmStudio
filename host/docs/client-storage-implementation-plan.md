# Implementation Plan — Client-Side MP4 Storage as Primary Path (Reviewed & Hardened)

> **Second review pass (2026-07-26)** caught issues in the original hardening — most importantly,
> **Step 2's proposed fix is a regression**: it would silently drop every clip except the last one
> in a multi-clip scene/batch generation. Corrected approach is inline below each affected step.
> Status column tracks actual implementation as it lands.

## Background

The server is running out of disk and memory under load. **Most of the core infrastructure already exists** — the generation pipeline issues 45-minute proxy tickets (`ClientMediaUrl`) so the browser can download clips directly into user folders via the JS File System Access API. 

This document details the refined step-by-step implementation plan incorporating architectural review feedback.

---

## What Already Works (Do Not Re-Build)

| Component | File | Status |
|---|---|---|
| Clip generation hands proxy URL to browser (not server disk) | `FilmJobService.cs` L2580–2594 | ✅ Done |
| Server media proxy endpoint (CORS-safe 45-min ticket) | `Program.cs` L3863 `/api/media/proxy/{token}` | ✅ Done |
| `MediaProxyTicketStore` issues short-lived download tickets | `Program.cs` L131 | ✅ Done |
| `JobSnapshot.ClientMediaUrl` + `ClientRelativePath` populated | `FilmJobService.cs` L2588–2589 | ✅ Done |
| JS File System Access API (`showDirectoryPicker`, read/write) | `pagetomovie-media.js` | ✅ Done |
| SHA-256 computed in browser, written to server registry | `pagetomovie-media.js` `_sha256Hex` | ✅ Done |
| ffmpeg.wasm silence trim before save | `ClientMediaFolderService.cs` `SilenceTrimAsync` | ✅ Done |
| `ClientMediaFolderService` auto-saves on `JobUpdated` event | `ClientMediaFolderService.cs` L40–51 | ✅ Done |
| `ClientVideoStitchService` prefers local blob URL over server proxy | `ClientVideoStitchService.cs` L70–74 | ✅ Done |
| Clip history archived to `assets/video/history/` before overwrite | `pagetomovie-media.js` `_archiveClipHistoryAsync` | ✅ Done |
| `.client.json` marker file recognised as "clip present" | `FilmJobService.cs` `ClipPresentOnServerOrClient` | ✅ Done |
| `MediaRegistryService` stores sha256 + path in SQLite | `MediaRegistryService.cs` | ✅ Done |
| `ServerMediaPruningService` purges server media after 48h / 80% disk (sync-confirmed via `MediaRegistryService`, off by default) | `ServerMediaPruningService.cs` | ✅ Done |
| `.client.json` marker already written on verified registration | `Program.cs` ~L3990 inside `POST /api/projects/{id}/media/register` | ✅ Done — **missing from the original table**; Step 3 below was proposing to re-add this as new work |

---

## Hardened Implementation Steps

### Step 1: Stream Media Proxy Response Without Premature Disposal

**File:** `host/PageToMovie.Api/Program.cs` (`/api/media/proxy/{token}`)

**Problem (confirmed real):** `ReadAsByteArrayAsync` buffers entire MP4 files (20–100 MB) into server RAM before returning `Results.File`. Under concurrent client downloads, this causes immediate server Memory (OOM) crashes.

**Original fix had two bugs, both corrected here:**
1. If `ReadAsStreamAsync` throws (between `GetAsync` succeeding and `Results.Stream` being returned), the original draft's `catch` block returned `Results.BadRequest` without ever disposing `resp`/`http` — a real leak on exactly the error path most likely to occur under load.
2. `new HttpClient()` per request is the classic anti-pattern that causes socket exhaustion under concurrent load — the same failure mode this whole fix exists to prevent. Use `IHttpClientFactory` (already used elsewhere in this codebase, e.g. `AddHttpClient<GeminiChatClient>`), not a raw `new HttpClient()`.

**Corrected fix:** stream via `Results.Stream`, wrap the whole thing in try/catch-with-cleanup so every exit path disposes `resp`, and get the client from `IHttpClientFactory`.

```csharp
app.MapGet("/api/media/proxy/{token}", async (
    string token,
    MediaProxyTicketStore tickets,
    IHttpClientFactory httpFactory,
    CancellationToken ct) =>
{
    var url = tickets.TryTakeUrl(token);
    if (string.IsNullOrWhiteSpace(url))
        return Results.NotFound(new { ok = false, error = "Media ticket expired or invalid" });

    var http = httpFactory.CreateClient("media-proxy");
    HttpResponseMessage? resp = null;
    try
    {
        resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var code = (int)resp.StatusCode;
            resp.Dispose();
            return Results.Json(new { ok = false, error = $"Upstream HTTP {code}" }, statusCode: code);
        }

        var stream = await resp.Content.ReadAsStreamAsync(ct);
        var ctype = resp.Content.Headers.ContentType?.ToString() ?? "video/mp4";
        var toDispose = resp; // Results.Stream's onCompleted always fires — this is the only disposal path from here on.
        return Results.Stream(
            stream,
            contentType: ctype,
            fileDownloadName: "clip.mp4",
            onCompleted: () => { toDispose.Dispose(); return Task.CompletedTask; });
    }
    catch (Exception ex)
    {
        resp?.Dispose(); // covers the ReadAsStreamAsync-throws case the original draft missed
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});
```

`IHttpClientFactory` client registration (near the other `AddHttpClient<T>` calls): `builder.Services.AddHttpClient("media-proxy", c => c.Timeout = TimeSpan.FromMinutes(10));`

---

### Step 2: Idempotent Early Hub Hooking (Global + Page Level) & Double-Fire Prevention

**Files:** `host/PageToMovie.Web/Components/Layout/MainLayout.razor`, `host/PageToMovie.Web/Services/ClientMediaFolderService.cs`

**1. Early Registration (Lifetime Safety) — unchanged, this part is correct:**
Instead of hooking `EnsureHubHookAsync()` only inside `Scenes.razor` (which disposes if the user navigates away mid-generation), invoke `EnsureHubHookAsync()` idempotently inside `MainLayout.razor` on session start:
```csharp
// In MainLayout.razor OnAfterRenderAsync:
if (firstRender)
{
    await MediaFolder.EnsureHubHookAsync();
}
```
`EnsureHubHookAsync()` is idempotent (`if (_hubHooked) return;`).

**2. Double-Fire Prevention — original approach was a regression, corrected here.**

The original draft proposed restricting `OnJobUpdated` to save only when `snap.Status == "done"`. **This breaks multi-clip generation.** Traced precisely: `ClientMediaUrl`/`ClientRelativePath` are set inside `FilmJobService.GenerateOneClipAsync`, which `RunBatchGenAsync` calls in a loop for every clip in a scene — the job's overall `Status` stays `"running"` for the entire loop and only flips to `"done"` once, after all clips finish. Since `ClientMediaUrl` gets overwritten on each clip, a `Status=="done"`-only guard would mean only the *last* clip of a multi-clip scene ever gets saved to the client's folder — every earlier clip's "running" tick (the only time its URL is live) would be silently ignored.

The double-fetch this step is actually trying to prevent is real, but for a different reason: the existing `_savingKeys` lock only blocks *concurrent* duplicate saves for the same path — it doesn't stop a second, *sequential* notification for a path that already finished saving (e.g. a single-clip job's "running" tick saves the clip, then its "done" tick — carrying the same URL — triggers a second, wasted download+hash+write). The fix is to remember which paths have *already completed*, not to gate on job status:

```csharp
// New field alongside _savingKeys:
private readonly HashSet<string> _savedKeys = new(StringComparer.OrdinalIgnoreCase);

private void OnJobUpdated(JobSnapshot snap)
{
    if (snap is null) return;
    if (!string.Equals(snap.Status, "done", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(snap.Status, "running", StringComparison.OrdinalIgnoreCase))
        return;
    if (string.IsNullOrWhiteSpace(snap.ClientMediaUrl) ||
        string.IsNullOrWhiteSpace(snap.ClientRelativePath) ||
        string.IsNullOrWhiteSpace(snap.ProjectId))
        return;

    var key = $"{snap.ProjectId}|{snap.ClientRelativePath}";
    lock (_savingKeys)
    {
        if (_savedKeys.Contains(key)) return; // already completed — the later "done" tick for this same path is a no-op
    }
    _ = SaveJobMediaAsync(snap);
}
```
`SaveJobMediaAsync`'s success path adds `key` to `_savedKeys` (alongside the existing `_savingKeys.Remove(key)` in its `finally`). This fixes the single-clip double-fire (second notification for an already-saved path is skipped) without dropping any clip in a multi-clip batch (each clip has its own distinct path, saved exactly once on its own first sighting).

---

### Step 3: `.client.json` Marker — Already Done, No New Code Needed

**This already exists** — `Program.cs` (`POST /api/projects/{id}/media/register`) writes it today:
```csharp
var marker = full + ".client.json";
await File.WriteAllTextAsync(marker, System.Text.Json.JsonSerializer.Serialize(new
{
    storage = "client",
    sha256 = dto.Sha256,
    sizeBytes = dto.SizeBytes,
    registeredAt = dto.CreatedAt,
    userId = user.UserId,
}) + "\n", ct);
```
— guarded by `Directory.CreateDirectory(Path.GetDirectoryName(full)!);` right before it, so it never silently skips writing the marker for a brand-new scene folder (the original draft's proposed snippet used `if (Directory.Exists(dir))` instead of creating it, which would have been a real regression for exactly that case — several places, `CreditsGeneratorService` and `ReviewIndexService` among them, treat a missing marker as "clip not present").

**Action:** none. Do not add a second copy of this logic to `MediaRegistryService` — it would either duplicate the write or, if it replaced the existing one, reintroduce the directory-creation gap above. If a later refactor wants this centralized in the service instead of the endpoint, that's a plain code-move, not new functionality.

---

### Step 4: Add User-Facing "Connect Folder" Banner in `Scenes.razor`

**Files:** `host/PageToMovie.Web/Components/Pages/Scenes.razor`, `NavMenu.razor`

**Unified State & User-Centric Language:**
Reuse `ClientMediaFolderService.IsConnected` and `FolderName` (shared with `NavMenu`). Avoid technical jargon like "Railway" or "server disk".

```html
@if (!MediaFolder.IsConnected)
{
    <div class="alert alert-warning d-flex align-items-center justify-content-between py-2 mb-3">
        <span>📁 <strong>Connect a folder</strong> so clips save on this computer.</span>
        <button class="btn btn-sm btn-warning ms-3 text-nowrap" @onclick="ConnectMediaFolderAsync">Connect Folder</button>
    </div>
}
else
{
    <div class="alert alert-success d-flex align-items-center justify-content-between py-2 mb-3">
        <span>✅ Saving clips to: <strong>@MediaFolder.FolderName</strong></span>
    </div>
}
```

---

### Step 5: Aggressive Server MP4 Prune Pass When `.client.json` Marker Exists

**File:** `host/PageToMovie.Engine/ServerMediaPruningService.cs`

Add a pre-pass in `PerformPruning`: if a `.client.json` marker file exists alongside a server `.mp4`, delete the server `.mp4` immediately. The marker proves the client successfully saved the file locally.

```csharp
// Pass 0: Delete server MP4s where .client.json marker is present
foreach (var jsonMarker in Directory.GetFiles(rootPath, "*.client.json", SearchOption.AllDirectories))
{
    var mp4Path = jsonMarker.Substring(0, jsonMarker.Length - ".client.json".Length);
    if (File.Exists(mp4Path))
    {
        try
        {
            File.Delete(mp4Path);
            deletedCount++;
            _logger?.LogInformation("Pruned redundant server MP4: {Path} (client marker confirmed)", mp4Path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed deleting redundant server MP4 {Path}", mp4Path);
        }
    }
}
```

---

### Step 6: Folder Persistence (localStorage + Quick Re-connect)

**Files:** `host/PageToMovie.Web/wwwroot/js/pagetomovie-media.js`, `ClientMediaFolderService.cs`

- On successful `connectFolderAsync`, save folder name: `localStorage.setItem('ptm_media_folder', this._root.name)`.
- If user reloads or navigates back, read stored folder name. If folder is disconnected, show a 1-click **"Reconnect {folderName}"** button. Chrome/Edge remember the directory selection, making reconnection seamless.

---

### Step 7: `ClientStorageMode` Server Direct-Proxy Flag

**File:** `host/PageToMovie.Engine/FilmJobService.cs`

**Gating Rule:** **DO NOT** flip `ClientStorageMode=true` on Railway until Steps 1–5 are proven in production.

When `ClientStorageMode` is enabled via environment variable (`PageToMovie__ClientStorageMode=true`):
- Grok/Veo/Gemini clip generators skip writing raw `.mp4` bytes to server disk.
- Always issue a proxy ticket (`ClientMediaUrl`) for client-side download.
- Fall back to 48-hour `ServerMediaPruningService` for non-connected clients.

---

## Handling Edge Cases & Platform Limitations

1. **Ticket Expiration on Delayed Connect:**
   If a user connects their folder >45 minutes after generation completes, the proxy ticket will return HTTP 404/401. `ClientMediaFolderService` will detect 401/404 and fall back to fetching the standard scene clip URL `/api/projects/{id}/scenes/{s}/clips/{c}/video`.
   *(Follow-up: add `POST /api/media/proxy/refresh` if ticket timeouts are frequent).*

2. **Safari & Mobile iOS (No File System Access API):**
   Safari does not support `window.showDirectoryPicker`. For Safari/iOS users:
   - UI displays: *"Folder save requires Chrome or Edge."*
   - Generation falls back seamlessly to 48-hour server pruning + direct streaming.

3. **Clip finished, no media folder (feature 8 — shipped 2026-07-26, `6769a93`):**
   When a job reaches **`done`** with `ClientMediaUrl` + `ClientRelativePath` and the folder is not connected:
   - Service attempts `ConnectFolderAsync` once (picker).
   - If the user cancels or the browser is unsupported → sets `LocalSaveWarning` (outcome-only copy; Chrome/Edge wording when the API is missing).
   - **Scenes** shows a dismissible warning with **Connect folder**.
   - Warning clears on successful connect or Dismiss.
   - Hub subscription is early/idempotent (`MainLayout` + Scenes) so this still works if the user is not on Scenes mid-gen.
   - Auto-save **ignores `running`** and only acts on **`done`**.

---

## Approved Ship Sequence

| Step | Item | Status |
|------|------|--------|
| **1** | Stream Proxy (`Program.cs`) | 🔲 Open |
| **2** | Early hub hook + save only on `done` | ✅ Partial / done (`MainLayout`, Scenes, `ClientMediaFolderService` — `6769a93`) |
| **3** | Write `.client.json` on verified register | 🔲 Open |
| **4** | Proactive “Connect folder” banner (pre-gen) | 🔲 Open (post-gen warning is feature 8, already shipped) |
| **5** | Prune server MP4 when client marker exists | 🔲 Open |
| **6** | Folder name persistence (localStorage) | 🔲 Open |
| **7** | `ClientStorageMode` skip server write | 🔲 Open — only after 1–5 proven in production |
| **8** | Fallback UI when folder not connected | ✅ Done (`6769a93`) |

1. **Step 1: Stream Proxy** (`Program.cs`) — pure server fix, zero UX risk, eliminates OOM.
2. **Step 2: Early Idempotent Hub Hook & Status Guard** (`MainLayout.razor` & `ClientMediaFolderService.cs`) — **shipped**.
3. **Step 3: Write `.client.json` Marker on Verified Register** (`MediaRegistryService.cs`).
4. **Step 4: Connect Folder Banner** (`Scenes.razor`) — pre-gen; distinct from feature-8 post-gen warning.
5. **Step 5: Prune Redundant Server MP4s** (`ServerMediaPruningService.cs`).
6. **Step 6: Folder Persistence** (`pagetomovie-media.js`).
7. **Step 7: `ClientStorageMode` Flag** (`FilmJobService.cs`) — only after 1–5 are verified in production.
8. **Feature 8: No-folder fallback warning** — **shipped** (`6769a93`).
