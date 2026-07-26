# Implementation Plan — Client-Side MP4 Storage as Primary Path

## Background

The server is running out of disk. The good news: **most of the infrastructure already exists**. The generation pipeline already does the right thing — it hands a proxy ticket to the browser instead of writing to server disk. The gap is that the browser-side save is never triggered automatically because the UI never calls `MediaFolder.EnsureHubHookAsync()` on load.

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
| `Scenes.razor` injects `ClientMediaFolderService` | `Scenes.razor` L8 | ✅ Done |
| Clip history archived to `assets/video/history/` before overwrite | `pagetomovie-media.js` `_archiveClipHistoryAsync` | ✅ Done |
| `.client.json` marker file recognised as "clip present" | `FilmJobService.cs` `ClipPresentOnServerOrClient` | ✅ Done |
| `MediaRegistryService` stores sha256 + path in SQLite | `MediaRegistryService.cs` | ✅ Done |
| `ServerMediaPruningService` purges server media after 48h / 80% disk | `ServerMediaPruningService.cs` | ✅ Done |

---

## The Core Problem

`ClientMediaFolderService.EnsureHubHookAsync()` subscribes to `JobUpdated` events and auto-saves clips. But in `Scenes.razor` it is **never called**. The Hub is started (`Hub.EnsureStartedAsync()`) but `MediaFolder.EnsureHubHookAsync()` is not invoked. So when a clip finishes generating and the `JobUpdated` event fires with `ClientMediaUrl`, nobody is listening → clip is never downloaded to the client folder.

Additionally there is no persistent folder handle across page navigations — the browser forgets the selected folder when the user leaves `Scenes.razor`.

---

## What Needs to Change

### 1. Wire `EnsureHubHookAsync` in `Scenes.razor`

**File:** `host/PageToMovie.Web/Components/Pages/Scenes.razor`

In the existing `EnsureHubAsync()` method (or `OnAfterRenderAsync`), call `MediaFolder.EnsureHubHookAsync()` immediately after `Hub.EnsureStartedAsync()`. This is a one-liner since the service already does everything correctly once hooked.

```csharp
// In EnsureHubAsync():
await Hub.EnsureStartedAsync();
await MediaFolder.EnsureHubHookAsync();  // ← ADD THIS
```

**Effect:** From this point forward, every completed clip job automatically triggers `SaveJobMediaAsync`, which:
1. Prompts the user once for a folder (if not yet connected)
2. Downloads via `/api/media/proxy/{ticket}`
3. Runs ffmpeg.wasm silence trim
4. Writes to `{folder}/assets/video/scene_SS_clip_CC.mp4`
5. Registers SHA-256 with the server

---

### 2. Add a Persistent "Connect Media Folder" Banner in `Scenes.razor`

**File:** `host/PageToMovie.Web/Components/Pages/Scenes.razor`

Users need to connect their folder *before* generating, so the auto-save prompt doesn't interrupt mid-generation. Add a visible banner at the top of the Scenes page:

```html
@if (!MediaFolder.IsConnected)
{
    <div class="alert alert-warning d-flex align-items-center gap-3 mb-3">
        <span>📁 <strong>Connect a local media folder</strong> to save generated clips to your computer (keeps Railway disk free).</span>
        <button class="btn btn-sm btn-warning" @onclick="ConnectMediaFolderAsync">Connect Folder</button>
    </div>
}
else
{
    <div class="alert alert-success d-flex align-items-center gap-2 mb-3 py-2">
        <span>✅ Media folder: <strong>@MediaFolder.FolderName</strong> — clips save automatically.</span>
    </div>
}
```

```csharp
private async Task ConnectMediaFolderAsync()
{
    await MediaFolder.ConnectFolderAsync();
    StateHasChanged();
}
```

---

### 3. Persist Folder Handle Across Navigation (localStorage + Re-prompt)

**Problem:** The File System Access API handle is in-memory only. Navigating away from `Scenes.razor` and back means the user must pick the folder again.

**Plan A (Simple — Ship First):** Store only the folder *name* in `localStorage`. On next load, show a "Reconnect `{folderName}`?" banner with one-click re-prompt. The browser re-opens the picker but Chrome/Edge remember the last-used folder so it's 1 click.

**Plan B (Chrome Origin Private File System — Future):** Use OPFS (`navigator.storage.getDirectory()`) which requires no user prompt and survives navigation. More complex, implement after Plan A is stable.

**Changes for Plan A:**
- In `pagetomovie-media.js`: after `connectFolderAsync` succeeds, write `localStorage.setItem('ptm_media_folder', this._root.name)`.
- In `Scenes.razor` `OnAfterRenderAsync`: read `localStorage` via JS interop, if a name is found and folder is not connected, show the "Reconnect?" banner.

---

### 4. Write `.client.json` Marker After Successful Client Save

**File:** `host/PageToMovie.Engine/FilmJobService.cs` (server) and/or `host/PageToMovie.Web/Services/ClientMediaFolderService.cs` (client)

When the client successfully saves a clip and calls `POST /api/projects/{id}/media/register`, the server should write a `.client.json` marker file alongside the non-existent `.mp4` so the `ClipPresentOnServerOrClient` check (`FilmJobService.cs` L3375) returns true, and the Scenes page shows the clip as "on disk".

```csharp
// In MediaRegistryService.RegisterAsync, if kind == "clip":
var markerPath = Path.Combine(projectDir, relativePath + ".client.json");
await File.WriteAllTextAsync(markerPath, JsonSerializer.Serialize(new {
    sha256, sizeBytes, registeredAt = DateTimeOffset.UtcNow
}));
```

**Currently missing:** Without this marker, the UI shows clips as "not on disk" even after the client has saved them, causing broken video playback and incorrect status indicators.

---

### 5. Ensure Media Proxy Streams Instead of Buffering

**File:** `host/PageToMovie.Api/Program.cs` L3879

The current proxy endpoint reads all bytes into memory (`ReadAsByteArrayAsync`) before sending — this is a RAM spike for every clip download (clips are ~20–100MB). Change to streaming:

```csharp
// Replace:
var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
return Results.File(bytes, ctype, fileDownloadName: "clip.mp4");

// With:
var stream = await resp.Content.ReadAsStreamAsync(ct);
return Results.Stream(stream, ctype, fileDownloadName: "clip.mp4");
```

**Effect:** Eliminates the server RAM spike during client downloads. This alone may resolve the OOM issue for in-progress downloads.

---

### 6. Aggressive Pruning of Server-Side Clips After Client Registration

**File:** `host/PageToMovie.Engine/ServerMediaPruningService.cs`

Add a targeted pruning pass: when a `.client.json` marker exists alongside an `.mp4`, delete the `.mp4` immediately (the client has it; the server copy is redundant). This runs inside `PerformPruning` as a pass *before* the age-based sweep.

```csharp
// New pass 0: delete server MP4s where .client.json exists (client owns the file)
foreach (var jsonMarker in Directory.GetFiles(rootPath, "*.client.json", SearchOption.AllDirectories))
{
    var mp4Path = jsonMarker.Replace(".client.json", "");
    if (File.Exists(mp4Path))
    {
        try { File.Delete(mp4Path); deletedCount++; } catch { }
    }
}
```

---

### 7. Prevent Server from Writing MP4 at All for New Grok Clips

**File:** `host/PageToMovie.Engine/FilmJobService.cs`

Currently `GenerateSceneClipsAsync` (around L2030) still has a fallback write path that stores clips server-side for non-Grok providers (Gemini/Veo). Add an environment-controlled option `ClientStorageMode=true` that skips the server write for all providers and always issues a proxy ticket. When this is enabled:

- Never write to `assets/video/` on server
- Always set `ClientMediaUrl` + `ClientRelativePath` on the snapshot
- Let `ServerMediaPruningService` handle anything that slips through

This is the opt-in flag for full client-storage mode and can be set in Railway environment variables.

---

### 8. Fallback: Clips Without a Media Folder Connected

There will be cases where a user generates without connecting a folder (mobile browser, unsupported browser, etc.). The plan:

- **Server still must not OOM**: The proxy endpoint (fix #5) already streams instead of buffering.
- **48h prune** (already implemented): clips are pruned automatically.
- **UI warning**: When `ClientMediaUrl` is set in the job result but `MediaFolder.IsConnected == false`, show a one-time toast: *"Your clip was generated but couldn't be saved locally — connect a media folder to keep it permanently."*

---

## Implementation Order (Ship Sequence)

These are ordered smallest-risk-first so each can be deployed independently:

| Step | Change | Files | Risk | Impact |
|---|---|---|---|---|
| **1** | Proxy streams instead of buffers | `Program.cs` | Very Low | Eliminates RAM spike on download |
| **2** | Wire `EnsureHubHookAsync` in Scenes | `Scenes.razor` | Low | Auto-save kicks in immediately |
| **3** | Write `.client.json` marker on registration | `MediaRegistryService.cs` | Low | UI shows correct clip status |
| **4** | Add "Connect Folder" banner in Scenes | `Scenes.razor` | Low | Users know to connect before gen |
| **5** | Prune server MP4s when `.client.json` exists | `ServerMediaPruningService.cs` | Low | Immediate disk recovery |
| **6** | Persist folder name in localStorage | `pagetomovie-media.js`, `Scenes.razor` | Medium | Survives page navigation |
| **7** | `ClientStorageMode` flag skips server write | `FilmJobService.cs` | Medium | Full prevention vs. cleanup |

---

## What This Does NOT Require

- No new NuGet packages
- No schema migrations
- No new API endpoints
- No changes to the generation pipeline logic
- No changes to the Grok/Gemini/Veo clients

The entire change is: **connect the existing wiring, fix the proxy to stream, write the marker file.**

---

## Open Questions

> [!IMPORTANT]
> **Q1: Do we enable `ClientStorageMode` globally on Railway immediately, or only after the banner + auto-save flow is tested?**
> Recommend: ship steps 1–5 first, verify clip saves work end-to-end, then flip the env flag.

> [!IMPORTANT]
> **Q2: What happens to existing projects with MP4s already on Railway disk?**
> The 48h prune will clean them naturally. Or we can run a one-shot manual prune now to reclaim disk immediately.

> [!WARNING]
> **Q3: Safari does not support `showDirectoryPicker`.** Users on Safari/iOS cannot use the client folder feature. They will see a "use Chrome or Edge" message. Is that acceptable for now?
