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
    /** Serializes MEMFS ops — single-thread core cannot run concurrent exec. */
    _opQueue: Promise.resolve(),

    /**
     * Run fn with exclusive access to the ffmpeg instance.
     * @template T
     * @param {() => Promise<T>} fn
     * @returns {Promise<T>}
     */
    _runExclusiveAsync: function (fn) {
        const run = this._opQueue.then(fn, fn);
        // Keep queue alive even if fn rejects
        this._opQueue = run.then(function () { }, function () { });
        return run;
    },

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

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;

            const ffmpeg = self._ffmpeg;
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
                    self._log("copy concat failed, re-encoding: " + (copyErr && copyErr.message));
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
                self.revokePreviewUrl();
                self._blobUrl = URL.createObjectURL(blob);

                // Cleanup MEMFS
                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                try { await ffmpeg.deleteFile("list.txt"); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }

                onProgress && onProgress(100, "Ready");
                return { success: true, url: self._blobUrl, count: urls.length };
            } catch (err) {
                console.error("concatVideosAsync failed:", err);
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    /**
     * Probe duration (seconds) of a video URL via ffmpeg.wasm -i (parses Duration: line).
     * @returns {{ success:boolean, seconds?:number, error?:string }}
     */
    probeDurationAsync: async function (url) {
        if (!url) return { success: false, error: "No URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            return self._probeDurationUnlockedAsync(url);
        });
    },

    /** Must hold exclusive lock (or be sole user of MEMFS). */
    _probeDurationUnlockedAsync: async function (url) {
        const load = await this.ensureLoadedAsync();
        if (!load.success) return load;
        const ffmpeg = this._ffmpeg;
        const fetchFile = (window.FFmpegUtil || {}).fetchFile;
        if (typeof fetchFile !== "function")
            return { success: false, error: "ffmpeg util fetchFile missing" };

        let logs = "";
        const onLog = ({ message }) => { logs += message + "\n"; };
        try {
            ffmpeg.on("log", onLog);
            const data = await fetchFile(url);
            await ffmpeg.writeFile("probe.mp4", data);
            try {
                await ffmpeg.exec(["-hide_banner", "-i", "probe.mp4"]);
            } catch (_) {
                // -i with no output exits non-zero; Duration is still in logs
            }
            const m = /Duration:\s*(\d{1,2}):(\d{2}):(\d{2}(?:\.\d+)?)/i.exec(logs);
            try { await ffmpeg.deleteFile("probe.mp4"); } catch (_) { /* */ }
            if (!m) return { success: false, error: "Duration not found" };
            const sec = (+m[1]) * 3600 + (+m[2]) * 60 + parseFloat(m[3]);
            return { success: true, seconds: sec };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        } finally {
            try { ffmpeg.off("log", onLog); } catch (_) { /* */ }
        }
    },

    // ── Silence trim primitives ────────────────────────────────────────────
    // Decision logic (where to cut) lives once, in C# (ClipSilenceTrimmer),
    // not duplicated here. These functions only do MEMFS/ffmpeg I/O:
    // analyzeSilenceAsync() hands the raw duration + silencedetect log to the
    // caller (ClientVideoStitchService), which runs ClipSilenceTrimmer.ComputeCutPoint /
    // ComputeLeadInPoint and then calls encodeSliceAsync() or discardSessionAsync().
    _silenceSessions: {},
    _silenceSessionSeq: 0,

    /**
     * silencedetect against a file already in MEMFS (caller holds lock).
     * @param {string} memfsName e.g. "sil_in.mp4"
     */
    _silenceDetectMemfsAsync: async function (memfsName, noiseDb, minSilenceSec) {
        noiseDb = noiseDb != null ? noiseDb : -35;
        minSilenceSec = minSilenceSec != null ? minSilenceSec : 0.25;
        const ffmpeg = this._ffmpeg;
        let logs = "";
        const onLog = ({ message }) => { logs += message + "\n"; };
        try {
            ffmpeg.on("log", onLog);
            try {
                await ffmpeg.exec([
                    "-hide_banner", "-nostats",
                    "-i", memfsName,
                    "-af", "silencedetect=noise=" + noiseDb + "dB:d=" + minSilenceSec,
                    "-f", "null", "-",
                ]);
            } catch (_) {
                // null muxer often exits non-zero; silence_* still in logs
            }
            return { success: true, log: logs };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        } finally {
            try { ffmpeg.off("log", onLog); } catch (_) { /* */ }
        }
    },

    /**
     * Probe Duration from a MEMFS file (caller holds lock).
     */
    _probeDurationMemfsAsync: async function (memfsName) {
        const ffmpeg = this._ffmpeg;
        let logs = "";
        const onLog = ({ message }) => { logs += message + "\n"; };
        try {
            ffmpeg.on("log", onLog);
            try {
                await ffmpeg.exec(["-hide_banner", "-i", memfsName]);
            } catch (_) { /* Duration still in logs */ }
            const m = /Duration:\s*(\d{1,2}):(\d{2}):(\d{2}(?:\.\d+)?)/i.exec(logs);
            if (!m) return { success: false, error: "Duration not found" };
            const sec = (+m[1]) * 3600 + (+m[2]) * 60 + parseFloat(m[3]);
            return { success: true, seconds: sec };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        } finally {
            try { ffmpeg.off("log", onLog); } catch (_) { /* */ }
        }
    },

    /**
     * Download + probe + silencedetect a clip; keep it resident in MEMFS under a
     * fresh token so a caller can decide (in C#, via ClipSilenceTrimmer) whether/where
     * to cut, then call encodeSliceAsync(token, ...) or discardSessionAsync(token).
     * Never throws — a failure just yields token:null so the caller treats it as
     * "nothing to trim" the same way a real no-silence-found result would.
     * @returns {{ success:boolean, token:string|null, totalSec:number, log:string, error?:string }}
     */
    analyzeSilenceAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, token: null, totalSec: 0, log: "", error: "No URL" };

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) {
                return {
                    success: true, token: null, totalSec: 0, log: "",
                    error: "skip: ffmpeg load failed — " + (load.error || ""),
                };
            }

            const ffmpeg = self._ffmpeg;
            const fetchFile = (window.FFmpegUtil || {}).fetchFile;
            if (typeof fetchFile !== "function") {
                return { success: true, token: null, totalSec: 0, log: "", error: "skip: ffmpeg util missing" };
            }

            const token = "sil" + (++self._silenceSessionSeq);
            const inName = token + "_in.mp4";
            try {
                onProgress && onProgress(8, "Loading clip…");
                const data = await fetchFile(url);
                await ffmpeg.writeFile(inName, data);

                onProgress && onProgress(18, "Probing duration…");
                const probe = await self._probeDurationMemfsAsync(inName);
                if (!probe.success || !(probe.seconds > 1.5)) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true, token: null, totalSec: probe.seconds || 0, log: "",
                        error: "skip: duration unknown or too short",
                    };
                }

                onProgress && onProgress(30, "Detecting silence…");
                const det = await self._silenceDetectMemfsAsync(inName, opts.noiseDb, opts.minSilenceSec);
                if (!det.success) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true, token: null, totalSec: probe.seconds, log: "",
                        error: "skip: silence detect failed — " + (det.error || ""),
                    };
                }

                self._silenceSessions[token] = inName;
                return { success: true, token: token, totalSec: probe.seconds, log: det.log };
            } catch (err) {
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                return { success: true, token: null, totalSec: 0, log: "", error: "skip: " + (err.message || String(err)) };
            }
        });
    },

    /**
     * Re-encode [startSec, startSec+durationSec) from a session opened by
     * analyzeSilenceAsync, and clean up its MEMFS file either way.
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    encodeSliceAsync: async function (token, startSec, durationSec, onProgress) {
        const self = this;
        return this._runExclusiveAsync(async function () {
            const inName = self._silenceSessions[token];
            if (!inName) return { success: false, error: "Unknown or expired silence-trim session" };
            delete self._silenceSessions[token];

            const ffmpeg = self._ffmpeg;
            const outName = token + "_out.mp4";
            try {
                onProgress && onProgress(55, "Re-encoding trimmed clip…");
                const args = ["-hide_banner", "-y"];
                if (startSec > 0.001) args.push("-ss", String(startSec));
                args.push("-i", inName);
                args.push("-t", String(Math.max(0.5, durationSec)));
                args.push(
                    "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                    "-c:a", "aac", "-b:a", "128k",
                    "-movflags", "+faststart",
                    outName);
                await ffmpeg.exec(args);

                onProgress && onProgress(90, "Preparing…");
                const out = await ffmpeg.readFile(outName);
                const blob = new Blob([out.buffer], { type: "video/mp4" });
                // Dedicated blob URL for the caller — do not share stitch preview slot
                // (caller should revoke after use).
                const outUrl = URL.createObjectURL(blob);
                onProgress && onProgress(100, "Silence trim done");
                return { success: true, url: outUrl };
            } catch (err) {
                return { success: false, error: err.message || String(err) };
            } finally {
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }
            }
        });
    },

    /** Abandon a session opened by analyzeSilenceAsync without encoding it (nothing to trim). */
    discardSessionAsync: async function (token) {
        const self = this;
        return this._runExclusiveAsync(async function () {
            const inName = self._silenceSessions[token];
            if (!inName) return { success: true };
            delete self._silenceSessions[token];
            try { await self._ffmpeg.deleteFile(inName); } catch (_) { /* */ }
            return { success: true };
        });
    },

    /**
     * Sample JPEG frames from a video URL for AI auto-review (no server ffmpeg).
     *
     * Modes:
     *  - "tail": last ~1.5s at ~2 fps (previous-clip continuity)
     *  - "span": ~3 frames across the clip (start / mid / end-ish)
     *
     * @param {string} url blob: or http(s)
     * @param {{ mode?: 'tail'|'span', count?: number, maxWidth?: number, quality?: number }} [opts]
     * @returns {{ success:boolean, frames?: { base64:string, mime:string }[], error?:string }}
     */
    extractFramesAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, error: "No URL" };
        const mode = (opts.mode || "span").toLowerCase();
        const count = Math.max(1, Math.min(6, opts.count != null ? opts.count : (mode === "tail" ? 3 : 3)));
        const maxWidth = opts.maxWidth != null ? opts.maxWidth : 640;
        const quality = opts.quality != null ? opts.quality : 5; // mjpeg q:v, lower = better

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return { success: false, error: load.error || "ffmpeg load failed" };

            const ffmpeg = self._ffmpeg;
            const fetchFile = (window.FFmpegUtil || {}).fetchFile;
            if (typeof fetchFile !== "function")
                return { success: false, error: "ffmpeg util fetchFile missing" };

            const inName = "frame_in.mp4";
            const written = [];
            try {
                onProgress && onProgress(10, "Loading video for frames…");
                const data = await fetchFile(url);
                await ffmpeg.writeFile(inName, data);
                written.push(inName);

                const scale = "scale='min(" + maxWidth + ",iw)':-2";
                const pattern = "frame_%02d.jpg";
                onProgress && onProgress(40, mode === "tail" ? "Sampling clip end…" : "Sampling clip…");

                try {
                    if (mode === "tail") {
                        // Last ~1.5s @ ~2 fps (matches former server ExtractTailFrames)
                        await ffmpeg.exec([
                            "-hide_banner", "-y",
                            "-sseof", "-1.5",
                            "-i", inName,
                            "-vf", scale + ",fps=2",
                            "-frames:v", String(count),
                            "-q:v", String(quality),
                            pattern,
                        ]);
                    } else {
                        // ~3 frames spaced through the clip
                        await ffmpeg.exec([
                            "-hide_banner", "-y",
                            "-i", inName,
                            "-vf", scale + ",fps=1/2",
                            "-frames:v", String(count),
                            "-q:v", String(quality),
                            pattern,
                        ]);
                    }
                } catch (execErr) {
                    // Fallback: single frame near start
                    self._log("frame extract primary failed: " + (execErr && execErr.message));
                    try {
                        await ffmpeg.exec([
                            "-hide_banner", "-y",
                            "-ss", "0.5",
                            "-i", inName,
                            "-vf", scale,
                            "-frames:v", "1",
                            "-q:v", String(quality),
                            "frame_01.jpg",
                        ]);
                    } catch (fbErr) {
                        return {
                            success: false,
                            error: "Frame extract failed: " + (fbErr.message || String(fbErr)),
                        };
                    }
                }

                onProgress && onProgress(80, "Encoding frames…");
                const frames = [];
                for (let i = 1; i <= count + 2; i++) {
                    const name = "frame_" + String(i).padStart(2, "0") + ".jpg";
                    try {
                        const out = await ffmpeg.readFile(name);
                        written.push(name);
                        if (!out || !out.length) continue;
                        const bytes = out instanceof Uint8Array ? out : new Uint8Array(out.buffer || out);
                        if (bytes.length < 64) continue;
                        frames.push({
                            base64: self._bytesToBase64(bytes),
                            mime: "image/jpeg",
                        });
                    } catch (_) {
                        // no more frames
                        if (i > 1) break;
                    }
                }

                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }

                if (frames.length === 0)
                    return { success: false, error: "No frames produced" };

                onProgress && onProgress(100, "Frames ready");
                return { success: true, frames: frames };
            } catch (err) {
                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                return { success: false, error: err.message || String(err) };
            }
        });
    },
};
