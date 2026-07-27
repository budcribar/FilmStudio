/**
 * Client-side video stitching, audio silence trim, and frame sampling via ffmpeg.wasm.
 * All static ffmpeg assets are served same-origin from /js/ffmpeg/ for maximum speed & zero CORS issues.
 */
function reportProgress(onProgress, pct, msg) {
    if (typeof onProgress === "function") {
        try { onProgress(pct, msg); } catch (_) { }
    }
}

window.PageToMovieFfmpeg = {
    _ffmpeg: null,
    _loaded: false,
    _loading: null,
    _blobUrl: null,
    _silenceSessions: {},
    _silenceSessionSeq: 0,
    _lock: Promise.resolve(),

    _assets: {
        ffmpegJs: {
            url: "/js/ffmpeg/ffmpeg.js",
        },
        utilJs: {
            url: "/js/ffmpeg/util.js",
        },
        workerJs: "/js/ffmpeg/814.ffmpeg.js",
        coreJs: "/js/ffmpeg/ffmpeg-core.js",
        wasmJs: "/js/ffmpeg/ffmpeg-core.wasm",
    },

    _runExclusiveAsync: function (fn) {
        const next = this._lock.then(fn, fn);
        this._lock = next.then(() => {}, () => {});
        return next;
    },

    _log: function (msg) {
        if (typeof msg === "string" && msg.trim().length > 0) {
            console.debug("[PageToMovieFfmpeg]", msg);
        }
    },

    /** Load local ffmpeg assets from same-origin /js/ffmpeg/. */
    ensureLoadedAsync: async function (onProgress) {
        if (this._loaded && this._ffmpeg) return { success: true };
        if (this._loading) return this._loading;

        const self = this;
        this._loading = (async () => {
            try {
                reportProgress(onProgress, 0, "Loading video tools…");
                await self._ensureScript(self._assets.ffmpegJs.url);
                await self._ensureScript(self._assets.utilJs.url);

                const FFmpegClass = (window.FFmpegWASM && window.FFmpegWASM.FFmpeg)
                    || (window.FFmpeg && window.FFmpeg.FFmpeg)
                    || window.FFmpeg;
                if (!FFmpegClass) {
                    throw new Error("ffmpeg.wasm UMD not available");
                }

                const ffmpeg = new FFmpegClass();
                ffmpeg.on("log", ({ message }) => self._log(message));
                ffmpeg.on("progress", ({ progress }) => {
                    const pct = Math.max(0, Math.min(99, Math.round((progress || 0) * 100)));
                    reportProgress(onProgress, pct, "Combining…");
                });

                reportProgress(onProgress, 5, "Loading ffmpeg engine…");
                const origin = window.location.origin;

                await ffmpeg.load({
                    coreURL: origin + self._assets.coreJs,
                    wasmURL: origin + self._assets.wasmJs,
                    classWorkerURL: origin + self._assets.workerJs,
                });

                self._ffmpeg = ffmpeg;
                self._loaded = true;
                reportProgress(onProgress, 10, "Ready");
                return { success: true };
            } catch (err) {
                self._loading = null;
                console.error("ffmpeg.wasm load failed:", err);
                return { success: false, error: err.message || String(err) };
            } finally {
                self._loading = null;
            }
        })();

        return this._loading;
    },

    _ensureScript: function (src) {
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
            s.onload = () => { s.dataset.loaded = "1"; resolve(); };
            s.onerror = () => reject(new Error("Failed to load script: " + src));
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
     * Fetch ordered video URLs and concatenate into one MP4 blob URL for <video src>.
     * @param {string[]} urls absolute or root-relative clip/scene URLs
     * @param {(pct:number,msg:string)=>void} [onProgress]
     * @returns {{ success:boolean, url?:string, error?:string, count?:number }}
     */
    concatVideosAsync: async function (urls, onProgress) {
        if (!urls || urls.length === 0) {
            return { success: false, error: "No video URLs to combine" };
        }

        // Single file — no stitch needed
        if (urls.length === 1) {
            reportProgress(onProgress, 100, "Ready");
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
                reportProgress(onProgress, 12, "Downloading clips…");
                for (let i = 0; i < urls.length; i++) {
                    const name = "in" + String(i).padStart(3, "0") + ".mp4";
                    reportProgress(onProgress,
                        12 + Math.round((i / urls.length) * 40),
                        "Downloading " + (i + 1) + "/" + urls.length + "…");
                    const data = await fetchFile(urls[i]);
                    await ffmpeg.writeFile(name, data);
                    written.push(name);
                }

                // concat demuxer list
                const listBody = written.map(n => "file '" + n + "'").join("\n");
                await ffmpeg.writeFile("list.txt", listBody);

                reportProgress(onProgress, 55, "Stitching…");
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

                reportProgress(onProgress, 92, "Preparing player…");
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

                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: self._blobUrl, count: urls.length };
            } catch (err) {
                console.error("concatVideosAsync failed:", err);
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    probeDurationAsync: async function (url) {
        if (!url) return { success: false, error: "No URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync();
            if (!load.success) return { success: false, error: load.error };
            const fetchFile = (window.FFmpegUtil || {}).fetchFile;
            if (typeof fetchFile !== "function") return { success: false, error: "fetchFile missing" };
            const inName = "probe_tmp.mp4";
            try {
                const data = await fetchFile(url);
                await self._ffmpeg.writeFile(inName, data);
                const probe = await self._probeDurationMemfsAsync(inName);
                try { await self._ffmpeg.deleteFile(inName); } catch (_) {}
                return probe;
            } catch (err) {
                try { await self._ffmpeg.deleteFile(inName); } catch (_) {}
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    _probeDurationMemfsAsync: async function (inName) {
        let durationSec = 0;
        const ffmpeg = this._ffmpeg;
        const logHandler = ({ message }) => {
            if (!message) return;
            const m = message.match(/Duration:\s*(\d+):(\d+):(\d+\.\d+)/);
            if (m) {
                const hrs = parseFloat(m[1]);
                const mins = parseFloat(m[2]);
                const secs = parseFloat(m[3]);
                durationSec = hrs * 3600 + mins * 60 + secs;
            }
        };
        ffmpeg.on("log", logHandler);
        try {
            await ffmpeg.exec(["-hide_banner", "-i", inName]);
        } catch (_) {}
        ffmpeg.off("log", logHandler);
        return { success: durationSec > 0, seconds: durationSec };
    },

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
                reportProgress(onProgress, 8, "Loading clip…");
                const data = await fetchFile(url);
                await ffmpeg.writeFile(inName, data);

                reportProgress(onProgress, 18, "Probing duration…");
                const probe = await self._probeDurationMemfsAsync(inName);
                if (!probe.success || !(probe.seconds > 1.5)) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true, token: null, totalSec: probe.seconds || 0, log: "",
                        error: "skip: duration unknown or too short",
                    };
                }

                reportProgress(onProgress, 30, "Detecting silence…");
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

    encodeSliceAsync: async function (token, startSec, durationSec, onProgress) {
        const self = this;
        return this._runExclusiveAsync(async function () {
            const inName = self._silenceSessions[token];
            if (!inName) return { success: false, error: "Unknown or expired silence-trim session" };
            delete self._silenceSessions[token];

            const ffmpeg = self._ffmpeg;
            const outName = token + "_out.mp4";
            try {
                reportProgress(onProgress, 55, "Re-encoding trimmed clip…");
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

                reportProgress(onProgress, 90, "Preparing…");
                const out = await ffmpeg.readFile(outName);
                const blob = new Blob([out.buffer], { type: "video/mp4" });
                const outUrl = URL.createObjectURL(blob);
                reportProgress(onProgress, 100, "Silence trim done");
                return { success: true, url: outUrl };
            } catch (err) {
                return { success: false, error: err.message || String(err) };
            } finally {
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }
            }
        });
    },

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

    extractFramesAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, error: "No URL" };
        const mode = (opts.mode || "span").toLowerCase();
        const count = Math.max(1, Math.min(6, opts.count != null ? opts.count : (mode === "tail" ? 3 : 3)));
        const maxWidth = opts.maxWidth != null ? opts.maxWidth : 640;
        const quality = opts.quality != null ? opts.quality : 5;

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
                reportProgress(onProgress, 10, "Loading video for frames…");
                const data = await fetchFile(url);
                await ffmpeg.writeFile(inName, data);
                written.push(inName);

                const scale = "scale='min(" + maxWidth + ",iw)':-2";
                const pattern = "frame_%02d.jpg";
                reportProgress(onProgress, 40, mode === "tail" ? "Sampling clip end…" : "Sampling clip…");

                try {
                    if (mode === "tail") {
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

                reportProgress(onProgress, 80, "Encoding frames…");
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
                        if (i > 1) break;
                    }
                }

                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }

                if (frames.length === 0)
                    return { success: false, error: "No frames produced" };

                reportProgress(onProgress, 100, "Frames ready");
                return { success: true, frames: frames };
            } catch (err) {
                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    _bytesToBase64: function (bytes) {
        let binary = "";
        const chunk = 0x8000;
        for (let i = 0; i < bytes.length; i += chunk) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        }
        return btoa(binary);
    },
};
