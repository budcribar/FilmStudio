/**
 * PageToMovie Client-Side Video Export & File System Access API Helper
 * Enables zero-server-overhead direct streaming of rendered MP4 movies
 * straight to the user's local hard drive.
 */

window.PageToMovieExport = {
    _directoryHandle: null,

    /**
     * Download a binary stream from Blazor (DotNetStreamReference) as a file.
     * Used for admin full-project zip export.
     */
    downloadStreamAsync: async function (fileName, contentStreamReference) {
        try {
            const arrayBuffer = await contentStreamReference.arrayBuffer();
            const blob = new Blob([arrayBuffer], { type: "application/zip" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = fileName || "PageToMovie_project.zip";
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            return { success: true };
        } catch (err) {
            console.error("downloadStreamAsync failed:", err);
            return { success: false, error: err.message || String(err) };
        }
    },

    /**
     * Checks if modern File System Access API is supported by the user's browser.
     */
    supportsFileSystemAccess: function () {
        return 'showSaveFilePicker' in window || 'showDirectoryPicker' in window;
    },

    /**
     * Prompts user to select an output folder ONCE per session.
     * All subsequent clip/movie renders in the session save directly into this folder without prompting.
     */
    selectExportDirectoryAsync: async function () {
        if (!('showDirectoryPicker' in window)) {
            return { success: false, error: 'Directory Picker API not supported on this browser.' };
        }
        try {
            this._directoryHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
            return {
                success: true,
                folderName: this._directoryHandle.name,
                message: `Export folder '${this._directoryHandle.name}' connected for this session.`
            };
        } catch (err) {
            console.warn('Directory selection cancelled or failed:', err);
            return { success: false, error: err.message || 'Folder selection cancelled.' };
        }
    },

    /**
     * Returns true if a local directory handle has been authorized by the user for this session.
     */
    hasDirectoryHandle: function () {
        return this._directoryHandle !== null;
    },

    /**
     * Saves raw Uint8Array / base64 data directly into the authorized session folder without prompts.
     * If no folder is selected yet, prompts once via file save picker or folder picker.
     */
    saveMovieToDiskAsync: async function (suggestedFilename, base64Data, mimeType) {
        try {
            const raw = window.atob(base64Data);
            const rawLength = raw.length;
            const uInt8Array = new Uint8Array(rawLength);
            for (let i = 0; i < rawLength; ++i) {
                uInt8Array[i] = raw.charCodeAt(i);
            }
            const blob = new Blob([uInt8Array], { type: mimeType || 'video/mp4' });

            // 1. Direct write into authorized session folder (zero prompts)
            if (this._directoryHandle) {
                const fileHandle = await this._directoryHandle.getFileHandle(suggestedFilename || 'PageToMovie_WIP.mp4', { create: true });
                const writable = await fileHandle.createWritable();
                await writable.write(blob);
                await writable.close();
                return { success: true, folderName: this._directoryHandle.name, message: `Saved directly into '${this._directoryHandle.name}/${suggestedFilename}'.` };
            }

            // 2. Single-file save picker (prompts once)
            if ('showSaveFilePicker' in window) {
                const options = {
                    suggestedName: suggestedFilename || 'PageToMovie_WIP.mp4',
                    types: [{
                        description: 'MP4 Video File',
                        accept: { 'video/mp4': ['.mp4'] }
                    }]
                };

                const handle = await window.showSaveFilePicker(options);
                const writable = await handle.createWritable();
                await writable.write(blob);
                await writable.close();
                return { success: true, message: 'Movie saved directly to disk.' };
            } else {
                // 3. Fallback browser download prompt
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = suggestedFilename || 'PageToMovie_WIP.mp4';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
                return { success: true, message: 'Movie downloaded via browser fallback.' };
            }
        } catch (err) {
            console.error('File System Access API export error:', err);
            return { success: false, error: err.message || 'Export cancelled or failed.' };
        }
    },

    /**
     * Client-Side WASM FFmpeg video clip concatenator helper stub.
     * Uses browser Blob URLs to merge scene clips in browser memory without server CPU usage.
     */
    concatenateClipsInBrowserAsync: async function (clipUrls, outputFilename) {
        try {
            console.log('Concatenating clips in browser WASM context:', clipUrls);
            const blobs = await Promise.all(clipUrls.map(url => fetch(url).then(r => r.blob())));
            const mergedBlob = new Blob(blobs, { type: 'video/mp4' });

            if (this._directoryHandle) {
                const fileHandle = await this._directoryHandle.getFileHandle(outputFilename || 'PageToMovie_FullMovie.mp4', { create: true });
                const writable = await fileHandle.createWritable();
                await writable.write(mergedBlob);
                await writable.close();
                return { success: true, folderName: this._directoryHandle.name };
            }

            const url = URL.createObjectURL(mergedBlob);
            const a = document.createElement('a');
            a.href = url;
            a.download = outputFilename || 'PageToMovie_FullMovie.mp4';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            return { success: true, count: clipUrls.length };
        } catch (err) {
            console.error('Browser WASM concatenation error:', err);
            return { success: false, error: err.message };
        }
    },

    /**
     * Copy text to the system clipboard (share links, etc.).
     */
    copyTextAsync: async function (text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text || "");
                return { success: true };
            }
            // Fallback for older browsers / non-secure contexts
            const ta = document.createElement("textarea");
            ta.value = text || "";
            ta.setAttribute("readonly", "");
            ta.style.position = "fixed";
            ta.style.left = "-9999px";
            document.body.appendChild(ta);
            ta.select();
            const ok = document.execCommand("copy");
            document.body.removeChild(ta);
            return ok ? { success: true } : { success: false, error: "Copy command failed" };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    /**
     * Upload a browser media URL (blob: or /api/… with access_token) to POST /api/demos as multipart.
     * @param {string} mediaUrl blob or same-origin media URL
     * @param {string} uploadUrl absolute or root-relative POST target (e.g. /api/demos)
     * @param {string|null} accessToken JWT for Authorization header
     * @param {{ title?: string, description?: string, projectId?: string, fileName?: string, acceptedGuidelines?: boolean }} meta
     */
    uploadDemoMovieAsync: async function (mediaUrl, uploadUrl, accessToken, meta) {
        try {
            if (!mediaUrl) return { success: false, error: "No media URL" };
            meta = meta || {};
            const res = await fetch(mediaUrl);
            if (!res.ok) {
                return { success: false, error: "Could not read video (" + res.status + ")" };
            }
            const blob = await res.blob();
            if (!blob || blob.size < 1024) {
                return { success: false, error: "Video is empty or too small" };
            }
            const form = new FormData();
            form.append("file", blob, meta.fileName || "movie.mp4");
            if (meta.title) form.append("title", meta.title);
            if (meta.description) form.append("description", meta.description);
            if (meta.projectId) form.append("projectId", meta.projectId);
            form.append("acceptedGuidelines", meta.acceptedGuidelines === false ? "false" : "true");
            form.append("madeForKids", meta.madeForKids === true ? "true" : "false");
            form.append("isAiSynthetic", meta.isAiSynthetic === false ? "false" : "true");
            if (meta.privacyStatus) form.append("privacyStatus", meta.privacyStatus);
            if (meta.tags) form.append("tags", meta.tags);
            // Default true: re-publish updates existing public demo (YouTube V2 replace)
            form.append("replaceExisting", meta.replaceExisting === false ? "false" : "true");

            const headers = {};
            if (accessToken) headers["Authorization"] = "Bearer " + accessToken;

            const up = await fetch(uploadUrl, {
                method: "POST",
                headers: headers,
                body: form,
                credentials: "same-origin",
            });
            const text = await up.text();
            let json = null;
            try { json = text ? JSON.parse(text) : null; } catch (_) { /* */ }
            if (!up.ok) {
                const err = (json && (json.error || json.message)) || text || ("HTTP " + up.status);
                return { success: false, error: String(err) };
            }
            return {
                success: true,
                demo: json && json.demo ? json.demo : json,
                pendingReview: !!(json && json.pendingReview),
                replacedExisting: !!(json && json.replacedExisting),
                message: json && json.message ? json.message : null,
            };
        } catch (err) {
            console.error("uploadDemoMovieAsync failed:", err);
            return { success: false, error: err.message || String(err) };
        }
    }
};
