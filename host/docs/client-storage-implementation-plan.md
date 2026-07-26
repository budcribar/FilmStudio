# Implementation Plan — Client-Side MP4 Storage as Primary Path (Reviewed & Hardened)

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
| `ServerMediaPruningService` purges server media after 48h / 80% disk | `ServerMediaPruningService.cs` | ✅ Done |

---

## Hardened Implementation Steps

### Step 1: Stream Media Proxy Response Without Premature Disposal

**File:** `host/PageToMovie.Api/Program.cs` L3863–3887

**Problem:** `ReadAsByteArrayAsync` buffers entire MP4 files (20–100 MB) into server RAM before returning `Results.File`. Under concurrent client downloads, this causes immediate server Memory (OOM) crashes.

**Fix:** Use `Results.Stream` while keeping the upstream `HttpResponseMessage` alive for the duration of the stream.

```csharp
app.MapGet("/api/media/proxy/{token}", async (
    string token,
    MediaProxyTicketStore tickets,
    CancellationToken ct) =>
{
    var url = tickets.TryTakeUrl(token);
    if (string.IsNullOrWhiteSpace(url))
        return Results.NotFound(new { ok = false, error = "Media ticket expired or invalid" });

    try
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            resp.Dispose();
            http.Dispose();
            return Results.Json(new { ok = false, error = $"Upstream HTTP {(int)resp.StatusCode}" },
                statusCode: (int)resp.StatusCode);
        }

        var stream = await resp.Content.ReadAsStreamAsync(ct);
        var ctype = resp.Content.Headers.ContentType?.ToString() ?? "video/mp4";

        // Results.Stream disposes stream, resp, and http on completion
        return Results.Stream(
            stream,
            contentType: ctype,
            fileDownloadName: "clip.mp4",
            onCompleted: async () =>
            {
                resp.Dispose();
                http.Dispose();
                await Task.CompletedTask;
            });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});
```

---

### Step 2: Idempotent Early Hub Hooking (Global + Page Level) & Double-Fire Prevention

**Files:** `host/PageToMovie.Web/Components/Layout/MainLayout.razor`, `host/PageToMovie.Web/Services/ClientMediaFolderService.cs`

**1. Early Registration (Lifetime Safety):**
Instead of hooking `EnsureHubHookAsync()` only inside `Scenes.razor` (which disposes if the user navigates away mid-generation), invoke `EnsureHubHookAsync()` idempotently inside `MainLayout.razor` on session start:
```csharp
// In MainLayout.razor OnAfterRenderAsync:
if (firstRender)
{
    await MediaFolder.EnsureHubHookAsync();
}
```
`EnsureHubHookAsync()` is idempotent (`if (_hubHooked) return;`).

**2. Double-Fire Prevention (Status Guard):**
In `ClientMediaFolderService.cs`, restrict `OnJobUpdated` to save ONLY when `snap.Status == "done"`. Ignore `"running"` events even if `ClientMediaUrl` is present:
```csharp
private void OnJobUpdated(JobSnapshot snap)
{
    if (snap is null) return;
    // Guard: Only save on "done" — ignore "running" to prevent duplicate fetches
    if (!string.Equals(snap.Status, "done", StringComparison.OrdinalIgnoreCase))
        return;

    if (string.IsNullOrWhiteSpace(snap.ClientMediaUrl) ||
        string.IsNullOrWhiteSpace(snap.ClientRelativePath) ||
        string.IsNullOrWhiteSpace(snap.ProjectId))
        return;

    _ = SaveJobMediaAsync(snap);
}
```

---

### Step 3: Write Exact `.client.json` Marker on Verified Client Registration

**Files:** `host/PageToMovie.Engine/MediaRegistryService.cs`, `host/PageToMovie.Web/Services/ClientMediaFolderService.cs`

**Exact Path Shape:**
`{WorkspaceRoot}/projects/{projectId}/assets/video/scene_{SS:D2}_clip_{CC:D2}.mp4.client.json`

**Safety Guarantee:**
The `.client.json` marker file is written ONLY AFTER the browser successfully completes:
1. `fetch(url)` from media proxy
2. `_sha256Hex` calculation
3. Disk write to local user folder
4. `POST /api/projects/{id}/media/register` with matching SHA-256 and byte length

When `MediaRegistryService.RegisterAsync` processes a clip registration:
```csharp
if (string.Equals(kind, "clip", StringComparison.OrdinalIgnoreCase))
{
    var projectDir = Path.Combine(_workspaceRoot, "projects", projectId);
    var markerPath = Path.Combine(projectDir, relativePath + ".client.json");
    var dir = Path.GetDirectoryName(markerPath);
    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
    {
        var json = JsonSerializer.Serialize(new
        {
            sha256 = sha256Norm,
            sizeBytes,
            registeredAt = DateTimeOffset.UtcNow
        });
        await File.WriteAllTextAsync(markerPath, json, ct).ConfigureAwait(false);
    }
}
```

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

---

## Approved Ship Sequence

1. **Step 1: Stream Proxy** (`Program.cs`) — pure server fix, zero UX risk, eliminates OOM.
2. **Step 2: Early Idempotent Hub Hook & Status Guard** (`MainLayout.razor` & `ClientMediaFolderService.cs`).
3. **Step 3: Write `.client.json` Marker on Verified Register** (`MediaRegistryService.cs`).
4. **Step 4: Connect Folder Banner** (`Scenes.razor`).
5. **Step 5: Prune Redundant Server MP4s** (`ServerMediaPruningService.cs`).
6. **Step 6: Folder Persistence** (`pagetomovie-media.js`).
7. **Step 7: `ClientStorageMode` Flag** (`FilmJobService.cs`) — only after 1–5 are verified in production.
