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
        return this._root ? this._root.name : null;
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
            return { success: true, folderName: this._root.name };
        } catch (err) {
            return { success: false, error: err.message || "Folder selection cancelled" };
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
     * Download from same-origin proxy (or any URL) and write under media folder.
     * Returns sha256 hex + size.
     */
    saveFromUrlAsync: async function (url, relativePath, onProgress) {
        if (!this._root) {
            const c = await this.connectFolderAsync();
            if (!c.success) return c;
        }
        try {
            onProgress && onProgress(5, "Downloading clip…");
            const res = await fetch(url, { credentials: "same-origin" });
            if (!res.ok) return { success: false, error: "Download failed HTTP " + res.status };
            const buf = await res.arrayBuffer();
            onProgress && onProgress(60, "Hashing…");
            const sha = await this._sha256Hex(buf);
            onProgress && onProgress(80, "Writing folder…");
            const { dir, fileName } = await this._ensurePathAsync(relativePath);
            const fh = await dir.getFileHandle(fileName, { create: true });
            const w = await fh.createWritable();
            await w.write(buf);
            await w.close();
            onProgress && onProgress(100, "Saved");
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
            const fh = await dir.getFileHandle(fileName, { create: false });
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
