/**
 * Client-side video stitch with ffmpeg.wasm (single-thread core — no COOP required).
 * Downloads clip/scene MP4 URLs (with auth query tokens) and concatenates in-browser.
 */
window.PageToMovieFfmpeg = {
    _ffmpeg: null,
    _loading: null,
    _loaded: false,
    _blobUrl: null,

    _log: function (msg) {
        if (typeof console !== "undefined" && console.debug) {
            console.debug("[PageToMovieFfmpeg]", msg);
        }
    },

    /** Load UMD scripts once, then ffmpeg core/wasm from jsDelivr. */
    ensureLoadedAsync: async function (onProgress) {
        if (this._loaded && this._ffmpeg) return { success: true };
        if (this._loading) return this._loading;

        this._loading = (async () => {
            try {
                onProgress && onProgress(0, "Loading video tools…");
                await this._ensureScript(
                    "https://cdn.jsdelivr.net/npm/@ffmpeg/ffmpeg@0.12.10/dist/umd/ffmpeg.js");
                await this._ensureScript(
                    "https://cdn.jsdelivr.net/npm/@ffmpeg/util@0.12.1/dist/umd/util.js");

                const FFmpegClass = (window.FFmpegWASM && window.FFmpegWASM.FFmpeg)
                    || (window.FFmpeg && window.FFmpeg.FFmpeg)
                    || window.FFmpeg;
                if (!FFmpegClass) {
                    throw new Error("ffmpeg.wasm UMD not available");
                }

                const ffmpeg = new FFmpegClass();
                ffmpeg.on("log", ({ message }) => this._log(message));
                if (typeof onProgress === "function") {
                    ffmpeg.on("progress", ({ progress }) => {
                        const pct = Math.max(0, Math.min(99, Math.round((progress || 0) * 100)));
                        onProgress(pct, "Combining…");
                    });
                }

                const coreBase = "https://cdn.jsdelivr.net/npm/@ffmpeg/core@0.12.6/dist/umd";
                onProgress && onProgress(5, "Loading ffmpeg core…");
                await ffmpeg.load({
                    coreURL: coreBase + "/ffmpeg-core.js",
                    wasmURL: coreBase + "/ffmpeg-core.wasm",
                });

                this._ffmpeg = ffmpeg;
                this._loaded = true;
                onProgress && onProgress(10, "Ready");
                return { success: true };
            } catch (err) {
                this._loading = null;
                console.error("ffmpeg.wasm load failed:", err);
                return { success: false, error: err.message || String(err) };
            } finally {
                this._loading = null;
            }
        })();

        return this._loading;
    },

    _ensureScript: function (src) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector('script[data-ptm-ffmpeg="' + src + '"]');
            if (existing) {
                if (existing.dataset.loaded === "1") resolve();
                else existing.addEventListener("load", () => resolve());
                existing.addEventListener("error", () => reject(new Error("Failed to load " + src)));
                return;
            }
            const s = document.createElement("script");
            s.src = src;
            s.async = true;
            s.dataset.ptmFfmpeg = src;
            s.onload = () => { s.dataset.loaded = "1"; resolve(); };
            s.onerror = () => reject(new Error("Failed to load " + src));
            document.head.appendChild(s);
        });
    },

    revokePreviewUrl: function () {
        if (this._blobUrl) {
            try { URL.revokeObjectURL(this._blobUrl); } catch (_) { /* */ }
            this._blobUrl = null;
        }
    },

    /**
     * Fetch ordered video URLs and concatenate into one MP4 blob URL for &lt;video src&gt;.
     * @param {string[]} urls absolute or root-relative clip/scene URLs (may include access_token)
     * @param {(pct:number,msg:string)=>void} [onProgress]
     * @returns {{ success:boolean, url?:string, error?:string, count?:number }}
     */
    concatVideosAsync: async function (urls, onProgress) {
        if (!urls || urls.length === 0) {
            return { success: false, error: "No video URLs to combine" };
        }

        // Single file — no stitch needed
        if (urls.length === 1) {
            onProgress && onProgress(100, "Ready");
            return { success: true, url: urls[0], count: 1, single: true };
        }

        const load = await this.ensureLoadedAsync(onProgress);
        if (!load.success) return load;

        const ffmpeg = this._ffmpeg;
        const util = window.FFmpegUtil || {};
        const fetchFile = util.fetchFile;
        if (typeof fetchFile !== "function") {
            return { success: false, error: "ffmpeg util fetchFile missing" };
        }

        const written = [];
        try {
            onProgress && onProgress(12, "Downloading clips…");
            for (let i = 0; i < urls.length; i++) {
                const name = "in" + String(i).padStart(3, "0") + ".mp4";
                onProgress && onProgress(
                    12 + Math.round((i / urls.length) * 40),
                    "Downloading " + (i + 1) + "/" + urls.length + "…");
                const data = await fetchFile(urls[i]);
                await ffmpeg.writeFile(name, data);
                written.push(name);
            }

            // concat demuxer list
            const listBody = written.map(n => "file '" + n + "'").join("\n");
            await ffmpeg.writeFile("list.txt", listBody);

            onProgress && onProgress(55, "Stitching…");
            // Prefer stream copy (fast). Fallback to re-encode if copy fails.
            let ok = false;
            try {
                await ffmpeg.exec([
                    "-f", "concat", "-safe", "0", "-i", "list.txt",
                    "-c", "copy",
                    "-movflags", "+faststart",
                    "out.mp4",
                ]);
                ok = true;
            } catch (copyErr) {
                this._log("copy concat failed, re-encoding: " + (copyErr && copyErr.message));
                try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }
                await ffmpeg.exec([
                    "-f", "concat", "-safe", "0", "-i", "list.txt",
                    "-c:v", "libx264", "-preset", "ultrafast", "-crf", "28",
                    "-c:a", "aac", "-b:a", "128k",
                    "-movflags", "+faststart",
                    "out.mp4",
                ]);
                ok = true;
            }

            if (!ok) return { success: false, error: "Stitch failed" };

            onProgress && onProgress(92, "Preparing player…");
            const out = await ffmpeg.readFile("out.mp4");
            const blob = new Blob([out.buffer], { type: "video/mp4" });
            this.revokePreviewUrl();
            this._blobUrl = URL.createObjectURL(blob);

            // Cleanup MEMFS
            for (const n of written) {
                try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
            }
            try { await ffmpeg.deleteFile("list.txt"); } catch (_) { /* */ }
            try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }

            onProgress && onProgress(100, "Ready");
            return { success: true, url: this._blobUrl, count: urls.length };
        } catch (err) {
            console.error("concatVideosAsync failed:", err);
            return { success: false, error: err.message || String(err) };
        }
    },
};
