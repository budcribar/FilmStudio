/**
 * Client-side video stitch with ffmpeg.wasm (single-thread core — no COOP required).
 * Downloads clip/scene MP4 URLs (with short-lived media tokens) and concatenates in-browser.
 *
 * CDN assets are version-pinned with Subresource Integrity (sha384) to reduce supply-chain risk.
 */
window.PageToMovieFfmpeg = {
    _ffmpeg: null,
    _loading: null,
    _loaded: false,
    _blobUrl: null,

    // Pinned @ffmpeg/* versions + SRI (sha384). Recompute if you bump versions.
    _assets: {
        ffmpegJs: {
            url: "https://cdn.jsdelivr.net/npm/@ffmpeg/ffmpeg@0.12.10/dist/umd/ffmpeg.js",
            integrity: "sha384-HJcOheArWWImG8iIDY0pbuK4nyRXZYGkzfaCq+ghw2CcjBlDShKWGpC9sTL42Lcu",
        },
        utilJs: {
            url: "https://cdn.jsdelivr.net/npm/@ffmpeg/util@0.12.1/dist/umd/index.js",
            integrity: "sha384-77TSno5UBOIFbP0dHjJN2umKfrf22jDQ8tKw2BfJqKvoJfUsWnmtW6a5LlkDVdNu",
        },
        coreJs: {
            url: "https://cdn.jsdelivr.net/npm/@ffmpeg/core@0.12.6/dist/umd/ffmpeg-core.js",
            integrity: "sha384-c9jtXGMa7FHb4zjdEQbYHSk+IhD2qPKTKyyD05+FsJ4hTo1G67o9cgo7APw3U9Lv",
        },
        coreWasm: {
            url: "https://cdn.jsdelivr.net/npm/@ffmpeg/core@0.12.6/dist/umd/ffmpeg-core.wasm",
            integrity: "sha384-SnAthyn82idS4YdVo46XOl86g1sUylqtN6BEYmPDFqzVO3Z3O/Xj1tVlyFqgyW4K",
        },
    },

    _log: function (msg) {
        if (typeof console !== "undefined" && console.debug) {
            console.debug("[PageToMovieFfmpeg]", msg);
        }
    },

    /** Load UMD scripts once, then ffmpeg core/wasm from jsDelivr (SRI-checked). */
    ensureLoadedAsync: async function (onProgress) {
        if (this._loaded && this._ffmpeg) return { success: true };
        if (this._loading) return this._loading;

        this._loading = (async () => {
            try {
                onProgress && onProgress(0, "Loading video tools…");
                await this._ensureScript(this._assets.ffmpegJs.url, this._assets.ffmpegJs.integrity);
                await this._ensureScript(this._assets.utilJs.url, this._assets.utilJs.integrity);

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

                onProgress && onProgress(5, "Loading ffmpeg core…");
                // Fetch core/wasm ourselves with SRI, then hand blob URLs to ffmpeg.load.
                const coreBlobUrl = await this._fetchWithSriBlobUrl(
                    this._assets.coreJs.url,
                    this._assets.coreJs.integrity,
                    "text/javascript");
                const wasmBlobUrl = await this._fetchWithSriBlobUrl(
                    this._assets.coreWasm.url,
                    this._assets.coreWasm.integrity,
                    "application/wasm");

                await ffmpeg.load({
                    coreURL: coreBlobUrl,
                    wasmURL: wasmBlobUrl,
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

    _ensureScript: function (src, integrity) {
        return new Promise((resolve, reject) => {
            const key = src;
            const existing = document.querySelector('script[data-ptm-ffmpeg="' + key + '"]');
            if (existing) {
                if (existing.dataset.loaded === "1") resolve();
                else existing.addEventListener("load", () => resolve());
                existing.addEventListener("error", () => reject(new Error("Failed to load " + src)));
                return;
            }
            const s = document.createElement("script");
            s.src = src;
            s.async = true;
            s.dataset.ptmFfmpeg = key;
            if (integrity) {
                s.integrity = integrity;
                s.crossOrigin = "anonymous";
            }
            s.onload = () => { s.dataset.loaded = "1"; resolve(); };
            s.onerror = () => reject(new Error("Failed to load (or SRI failed): " + src));
            document.head.appendChild(s);
        });
    },

    /**
     * Fetch a CDN asset, verify sha384 SRI, return a blob: URL for ffmpeg.load.
     * integrity format: "sha384-<base64>"
     */
    _fetchWithSriBlobUrl: async function (url, integrity, mime) {
        const res = await fetch(url, { mode: "cors", credentials: "omit", cache: "force-cache" });
        if (!res.ok) throw new Error("Failed to fetch " + url + " (" + res.status + ")");
        const buf = await res.arrayBuffer();
        await this._assertSha384(buf, integrity);
        return URL.createObjectURL(new Blob([buf], { type: mime || "application/octet-stream" }));
    },

    _assertSha384: async function (arrayBuffer, integrity) {
        if (!integrity || !integrity.startsWith("sha384-")) {
            throw new Error("Missing sha384 integrity");
        }
        const expectedB64 = integrity.slice("sha384-".length);
        const digest = await crypto.subtle.digest("SHA-384", arrayBuffer);
        const actualB64 = this._bytesToBase64(new Uint8Array(digest));
        if (actualB64 !== expectedB64) {
            throw new Error("SRI mismatch for ffmpeg asset (refusing to load)");
        }
    },

    _bytesToBase64: function (bytes) {
        let binary = "";
        const chunk = 0x8000;
        for (let i = 0; i < bytes.length; i += chunk) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        }
        return btoa(binary);
    },

    revokePreviewUrl: function () {
        if (this._blobUrl) {
            try { URL.revokeObjectURL(this._blobUrl); } catch (_) { /* */ }
            this._blobUrl = null;
        }
    },

    /**
     * Fetch ordered video URLs and concatenate into one MP4 blob URL for &lt;video src&gt;.
     * @param {string[]} urls absolute or root-relative clip/scene URLs (may include mt media token)
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
