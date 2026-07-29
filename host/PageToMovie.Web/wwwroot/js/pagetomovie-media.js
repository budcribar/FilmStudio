/**
 * Client media folder: store gen MP4s on the user's disk (File System Access API).
 * Server keeps hashes + metadata only.
 */
window.PageToMovieMedia = {
    _root: null, // FileSystemDirectoryHandle
    _projectId: null,
    _blobUrls: {},

    supportsDirectoryPicker: function () {
        return typeof window.showDirectoryPicker === "function";
    },

    hasFolder: function () {
        return !!this._root;
    },

    folderName: function () {
        return this.getFullPath() || (this._root ? this._root.name : null);
    },

    getFullPath: function () {
        return localStorage.getItem("ptm-media-fullpath") || null;
    },

    setFullPath: function (path) {
        if (path) {
            localStorage.setItem("ptm-media-fullpath", path);
        } else {
            localStorage.removeItem("ptm-media-fullpath");
        }
    },

    /**
     * Prompt once for a media root folder (session). Prefer same folder as export.
     */
    connectFolderAsync: async function () {
        if (!this.supportsDirectoryPicker()) {
            return { success: false, error: "This browser does not support folder access (use Chrome/Edge)." };
        }
        try {
            // Prefer reusing export folder if already connected
            if (window.PageToMovieExport && window.PageToMovieExport._directoryHandle) {
                this._root = window.PageToMovieExport._directoryHandle;
            } else {
                this._root = await window.showDirectoryPicker({ mode: "readwrite" });
                if (window.PageToMovieExport)
                    window.PageToMovieExport._directoryHandle = this._root;
            }
            await this._saveHandleToDbAsync(this._root);
            return { success: true, folderName: this._root.name };
        } catch (err) {
            return { success: false, error: err.message || "Folder selection cancelled" };
        }
    },

    /**
     * IndexedDB-backed persistence of the actual FileSystemDirectoryHandle (structured-cloneable,
     * unlike localStorage which can only hold the folder's name string). This is what makes a real
     * 1-click reconnect possible: showDirectoryPicker() always needs a fresh user gesture and has no
     * way to pre-select a remembered folder, but re-requesting permission on an already-held handle
     * does not re-open the OS folder-browser dialog.
     */
    _dbPromise: null,
    _openDbAsync: function () {
        if (this._dbPromise) return this._dbPromise;
        this._dbPromise = new Promise((resolve, reject) => {
            const req = indexedDB.open("ptm-media", 1);
            req.onupgradeneeded = () => { req.result.createObjectStore("handles"); };
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
        return this._dbPromise;
    },
    _saveHandleToDbAsync: async function (handle) {
        try {
            const db = await this._openDbAsync();
            await new Promise((resolve, reject) => {
                const tx = db.transaction("handles", "readwrite");
                tx.objectStore("handles").put(handle, "root");
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        } catch (err) {
            console.warn("Could not persist media folder handle for reconnect:", err);
        }
    },
    _loadHandleFromDbAsync: async function () {
        try {
            const db = await this._openDbAsync();
            return await new Promise((resolve, reject) => {
                const tx = db.transaction("handles", "readonly");
                const req = tx.objectStore("handles").get("root");
                req.onsuccess = () => resolve(req.result || null);
                req.onerror = () => reject(req.error);
            });
        } catch (_) {
            return null;
        }
    },

    /**
     * Silent reconnect attempt on page load: no dialog, no user gesture required. Succeeds only if
     * a handle was previously persisted AND the browser still grants readwrite permission on it
     * without asking (permission grants often do not survive a full page reload, in which case this
     * returns reason:"prompt" so the caller can offer a 1-click "Reconnect" button that calls
     * reconnectAsync() from a real click handler).
     */
    tryReconnectAsync: async function () {
        if (this._root) return { success: true, folderName: this._root.name, silent: true };
        const handle = await this._loadHandleFromDbAsync();
        if (!handle) return { success: false, reason: "none" };
        try {
            const perm = await handle.queryPermission({ mode: "readwrite" });
            if (perm === "granted") {
                this._root = handle;
                if (window.PageToMovieExport) window.PageToMovieExport._directoryHandle = handle;
                return { success: true, folderName: handle.name, silent: true };
            }
            return { success: false, reason: perm === "denied" ? "denied" : "prompt", folderName: handle.name };
        } catch (err) {
            return { success: false, reason: "error", error: err.message || String(err) };
        }
    },

    /**
     * Re-grant permission on the previously-chosen folder from a real user gesture (button click).
     * No folder-browser dialog — just a permission re-grant on the same handle.
     */
    reconnectAsync: async function () {
        const handle = await this._loadHandleFromDbAsync();
        if (!handle) return { success: false, error: "No remembered folder to reconnect to" };
        try {
            const perm = await handle.requestPermission({ mode: "readwrite" });
            if (perm !== "granted")
                return { success: false, error: "Permission was not granted" };
            this._root = handle;
            if (window.PageToMovieExport) window.PageToMovieExport._directoryHandle = handle;
            return { success: true, folderName: handle.name };
        } catch (err) {
            return { success: false, error: err.message || "Reconnect failed" };
        }
    },

    /**
     * Ensure project subfolder: {root}/{projectId}/assets/video/...
     */
    _ensurePathAsync: async function (relativePath) {
        if (!this._root) throw new Error("Media folder not connected");
        const parts = relativePath.replace(/\\/g, "/").split("/").filter(Boolean);
        let dir = this._root;
        for (let i = 0; i < parts.length - 1; i++) {
            dir = await dir.getDirectoryHandle(parts[i], { create: true });
        }
        const fileName = parts[parts.length - 1];
        return { dir, fileName };
    },

    /**
     * If relativePath is a clip video (assets/video/scene_SS_clip_CC.mp4) and a file already
     * exists there, move it to assets/video/history/scene_SS_clip_CC_{timestamp}.mp4 before it
     * gets overwritten, so ClipPromptCompareViewer has a previous version to compare against.
     * Best-effort: any failure here must never block the actual save.
     */
    _archiveClipHistoryAsync: async function (relativePath) {
        try {
            const m = /^assets\/video\/(scene_\d+_clip_\d+)\.mp4$/i.exec(relativePath.replace(/\\/g, "/"));
            if (!m) return;
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            let existing;
            try { existing = await dir.getFileHandle(fileName, { create: false }); }
            catch (_) { return; } // nothing to archive yet
            const file = await existing.getFile();
            if (!file || file.size < 1024) return;

            const historyPath = `assets/video/history/${m[1]}_${Date.now()}.mp4`;
            const { dir: histDir, fileName: histName } = await this._ensurePathAsync(historyPath);
            const buf = await file.arrayBuffer();
            const wh = await histDir.getFileHandle(histName, { create: true });
            const w = await wh.createWritable();
            await w.write(buf);
            await w.close();
        } catch (err) {
            console.warn("clip history archive skipped:", err);
        }
    },

    /**
     * Download from same-origin proxy (or any URL, incl. blob:) and write under media folder.
     * Pure I/O — callers that want silence-trim run PageToMovieFfmpeg.analyzeSilenceAsync /
     * encodeSliceAsync themselves first and pass the resulting blob: URL in as `url`.
     * Returns sha256 hex + size.
     * @param {string} url
     * @param {string} relativePath
     * @param {(p:number,msg:string)=>void} [onProgress]
     */
    saveFromUrlAsync: async function (url, relativePath, onProgress) {
        if (!this._root) {
            const c = await this.connectFolderAsync();
            if (!c.success) return c;
        }
        try {
            await this._archiveClipHistoryAsync(relativePath);
            const report = (p, m) => typeof onProgress === "function" && onProgress(p, m);
            report(5, "Downloading clip…");
            const res = await fetch(url, { credentials: "same-origin" });
            if (!res.ok) return { success: false, error: "Download failed HTTP " + res.status };
            const buf = await res.arrayBuffer();

            report(60, "Hashing…");
            const sha = await this._sha256Hex(buf);
            report(85, "Writing folder…");
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            const fh = await dir.getFileHandle(fileName, { create: true });
            const w = await fh.createWritable();
            await w.write(buf);
            await w.close();
            // Invalidate cached blob URL for this path
            const key = relativePath.replace(/\\/g, "/");
            if (this._blobUrls[key]) {
                try { URL.revokeObjectURL(this._blobUrls[key]); } catch (_) { /* */ }
                delete this._blobUrls[key];
            }
            report(100, "Saved");
            return {
                success: true,
                sha256: sha,
                sizeBytes: buf.byteLength,
                relativePath: relativePath.replace(/\\/g, "/"),
                folderName: this._root.name,
            };
        } catch (err) {
            console.error("saveFromUrlAsync", err);
            return { success: false, error: err.message || String(err) };
        }
    },

    /** Revoke an arbitrary blob: URL (e.g. one handed back by PageToMovieFfmpeg.encodeSliceAsync). */
    revokeUrl: function (url) {
        try { URL.revokeObjectURL(url); } catch (_) { /* */ }
    },

    /**
     * Read a project-relative file as a blob: URL for &lt;video&gt; / stitch.
     */
    getBlobUrlAsync: async function (relativePath) {
        if (!this._root) return { success: false, error: "Media folder not connected" };
        try {
            const key = relativePath.replace(/\\/g, "/");
            if (this._blobUrls[key]) {
                return { success: true, url: this._blobUrls[key] };
            }
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            let fh;
            try {
                fh = await dir.getFileHandle(fileName, { create: false });
            } catch (_) {
                // Fallback: search for take pattern scene_XX_clip_YY*.mp4 in local media folder
                const m = fileName.match(/^(scene_\d+_clip_\d+)/i);
                if (m) {
                    const prefix = m[1].toLowerCase();
                    let bestFh = null;
                    let bestMtime = 0;
                    for await (const entry of dir.values()) {
                        const nameLower = entry.name.toLowerCase();
                        if (entry.kind === "file" && nameLower.startsWith(prefix) && nameLower.endsWith(".mp4")) {
                            try {
                                const f = await entry.getFile();
                                if (f && f.size >= 1024 && f.lastModified > bestMtime) {
                                    bestMtime = f.lastModified;
                                    bestFh = entry;
                                }
                            } catch (_) {}
                        }
                    }
                    if (bestFh) fh = bestFh;
                }
            }
            if (!fh) return { success: false, error: "Not found in media folder" };
            const file = await fh.getFile();
            if (!file || file.size < 1024)
                return { success: false, error: "File missing or empty" };
            const url = URL.createObjectURL(file);
            this._blobUrls[key] = url;
            return { success: true, url: url, sizeBytes: file.size };
        } catch (err) {
            return { success: false, error: err.message || "Not found in media folder" };
        }
    },

    /**
     * Read a project-relative file (e.g. assets/video/scene_01_clip_01.mp4) as byte array.
     */
    getBytesAsync: async function (relativePath) {
        if (!this._root) return { success: false, error: "Media folder not connected" };
        try {
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            let fh;
            try {
                fh = await dir.getFileHandle(fileName, { create: false });
            } catch (_) {
                const m = fileName.match(/^(scene_\d+_clip_\d+)/i);
                if (m) {
                    const prefix = m[1].toLowerCase();
                    let bestFh = null;
                    let bestMtime = 0;
                    for await (const entry of dir.values()) {
                        const nameLower = entry.name.toLowerCase();
                        if (entry.kind === "file" && nameLower.startsWith(prefix) && nameLower.endsWith(".mp4")) {
                            try {
                                const f = await entry.getFile();
                                if (f && f.size >= 1024 && f.lastModified > bestMtime) {
                                    bestMtime = f.lastModified;
                                    bestFh = entry;
                                }
                            } catch (_) {}
                        }
                    }
                    if (bestFh) fh = bestFh;
                }
            }
            if (!fh) return { success: false, error: "Not found in media folder" };
            const file = await fh.getFile();
            if (!file || file.size < 1024)
                return { success: false, error: "File missing or empty" };
            const buf = await file.arrayBuffer();
            return { success: true, bytes: new Uint8Array(buf), sizeBytes: file.size };
        } catch (err) {
            return { success: false, error: err.message || "Not found in media folder" };
        }
    },

    /**
     * List archived previous versions of one clip (newest first), written by
     * _archiveClipHistoryAsync. Each entry's relativePath can be passed to getBlobUrlAsync.
     * @returns {{ success:boolean, entries?: { relativePath:string, timestampMs:number }[], error?:string }}
     */
    listClipHistoryAsync: async function (scene, clip) {
        if (!this._root) return { success: false, error: "Media folder not connected" };
        try {
            const prefix = `scene_${String(scene).padStart(2, "0")}_clip_${String(clip).padStart(2, "0")}_`;
            let histDir;
            try { histDir = await this._root.getDirectoryHandle("assets", { create: false }); }
            catch (_) { return { success: true, entries: [] }; }
            try { histDir = await histDir.getDirectoryHandle("video", { create: false }); }
            catch (_) { return { success: true, entries: [] }; }
            try { histDir = await histDir.getDirectoryHandle("history", { create: false }); }
            catch (_) { return { success: true, entries: [] }; }

            const entries = [];
            for await (const [name, handle] of histDir.entries()) {
                if (handle.kind !== "file" || !name.startsWith(prefix) || !name.endsWith(".mp4")) continue;
                const ts = parseInt(name.slice(prefix.length, -4), 10);
                if (!Number.isFinite(ts)) continue;
                entries.push({ relativePath: `assets/video/history/${name}`, timestampMs: ts });
            }
            entries.sort((a, b) => b.timestampMs - a.timestampMs);
            return { success: true, entries };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    revokeBlobUrls: function () {
        for (const k of Object.keys(this._blobUrls)) {
            try { URL.revokeObjectURL(this._blobUrls[k]); } catch (_) { /* */ }
        }
        this._blobUrls = {};
    },

    /**
     * Hash a blob: URL (stitched export) for registry + demo.
     */
    hashBlobUrlAsync: async function (blobUrl) {
        try {
            const res = await fetch(blobUrl);
            const buf = await res.arrayBuffer();
            const sha = await this._sha256Hex(buf);
            return { success: true, sha256: sha, sizeBytes: buf.byteLength };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    saveBlobUrlAsync: async function (blobUrl, relativePath) {
        if (!this._root) {
            const c = await this.connectFolderAsync();
            if (!c.success) return c;
        }
        try {
            const res = await fetch(blobUrl);
            const buf = await res.arrayBuffer();
            const sha = await this._sha256Hex(buf);
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            const fh = await dir.getFileHandle(fileName, { create: true });
            const w = await fh.createWritable();
            await w.write(buf);
            await w.close();
            return {
                success: true,
                sha256: sha,
                sizeBytes: buf.byteLength,
                relativePath: relativePath.replace(/\\/g, "/"),
            };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    _sha256Hex: async function (arrayBuffer) {
        const digest = await crypto.subtle.digest("SHA-256", arrayBuffer);
        const bytes = new Uint8Array(digest);
        return Array.from(bytes).map(b => b.toString(16).padStart(2, "0")).join("");
    },
};
