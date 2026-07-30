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
        // ffmpeg-worker-bundle.js has ffmpeg-core.js inlined — no importScripts() or
        // dynamic import() needed inside the worker, sidestepping all module/classic
        // worker loader conflicts.
        workerBundleJs: "/js/ffmpeg/ffmpeg-worker-bundle.js",
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

    _safeFetchFile: async function (url) {
        if (typeof url === "string" && !url.startsWith("blob:") && !url.startsWith("data:")) {
            const res = await fetch(url);
            if (!res.ok) {
                throw new Error("Clip video missing (" + res.status + " " + res.statusText + "). Please generate clip first.");
            }
            const buf = await res.arrayBuffer();
            return new Uint8Array(buf);
        }
        const util = window.FFmpegUtil || {};
        if (typeof util.fetchFile === "function") {
            return await util.fetchFile(url);
        }
        throw new Error("ffmpeg util fetchFile missing");
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
                // ffmpeg-worker-bundle.js has ffmpeg-core.js inlined, so no coreURL import
                // is needed inside the worker. wasmURL must be absolute so the inlined core
                // can locate the .wasm binary. classWorkerURL must be absolute because
                // relative paths resolve against blob: origin inside ffmpeg.load().
                const origin = window.location.origin;
                await ffmpeg.load({
                    coreURL: origin + "/js/ffmpeg/ffmpeg-core.js", // used only to derive default wasmURL path
                    wasmURL: origin + self._assets.wasmJs,
                    classWorkerURL: origin + self._assets.workerBundleJs,
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

    /** Fetch ordered URLs into MEMFS as sequentially-named files ("in000.<ext>", …). Shared by
     * concatVideosAsync/concatAudioSegmentsAsync so the download/write loop lives once. */
    _writeSequentialInputsAsync: async function (ffmpeg, urls, ext, onProgress, startPct, endPct) {
        const written = [];
        for (let i = 0; i < urls.length; i++) {
            const name = "in" + String(i).padStart(3, "0") + "." + ext;
            reportProgress(onProgress,
                startPct + Math.round((i / urls.length) * (endPct - startPct)),
                "Downloading " + (i + 1) + "/" + urls.length + "…");
            const data = await this._safeFetchFile(urls[i]);
            await ffmpeg.writeFile(name, data);
            written.push(name);
        }
        return written;
    },

    /** Read an output file as a blob URL and clean up its MEMFS inputs + itself. */
    _readAndCleanupAsync: async function (ffmpeg, outName, mimeType, cleanupNames) {
        const out = await ffmpeg.readFile(outName);
        const blob = new Blob([out.buffer], { type: mimeType });
        const url = URL.createObjectURL(blob);
        for (const n of cleanupNames) {
            try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
        }
        try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }
        return url;
    },

    /**
     * Fetch ordered video URLs and concatenate into one MP4 blob URL for <video src>.
     * @param {string[]} urls absolute or root-relative clip/scene URLs
     * @param {(pct:number,msg:string)=>void} [onProgress]
     * @returns {{ success:boolean, url?:string, error?:string, count?:number }}
     */
    concatVideosAsync: async function (urls, onProgress) {
        let list = [];
        if (Array.isArray(urls)) {
            list = urls;
        } else if (typeof urls === "string") {
            list = Array.from(arguments).filter(a => typeof a === "string" && a.length > 0 && typeof a !== "function");
        } else if (arguments.length > 0) {
            list = Array.from(arguments).filter(a => typeof a === "string" && a.length > 0 && typeof a !== "function");
        }

        if (!list || list.length === 0) {
            return { success: false, error: "No video URLs to combine" };
        }

        // Single file — no stitch needed
        if (list.length === 1) {
            reportProgress(onProgress, 100, "Ready");
            return { success: true, url: list[0], count: 1, single: true };
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

            let written = [];
            try {
                reportProgress(onProgress, 12, "Downloading clips…");
                written = await self._writeSequentialInputsAsync(ffmpeg, list, "mp4", onProgress, 12, 52);

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
                // Do NOT auto-revoke the previous _blobUrl here — CollectAndMixSceneSegmentsAsync
                // (C#) calls this function once per scene to build several *simultaneous*
                // intermediate segments before combining them in one final call. Auto-revoking on
                // every call was yanking the URL out from under an earlier scene's still-in-use
                // blob the moment a later scene's concat ran, so the final combine's fetch() on
                // that now-revoked blob: URL failed with a bare "Failed to fetch". Explicit
                // top-level preview replacement already calls revokePreviewUrl() itself (see
                // RevokePreviewUrlAsync callers) before requesting a new preview, so nothing here
                // relied on this implicit revoke for correctness — only for eager cleanup.
                self._blobUrl = URL.createObjectURL(blob);

                // Cleanup MEMFS
                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                try { await ffmpeg.deleteFile("list.txt"); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }

                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: self._blobUrl, count: list.length };
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
            const inName = "probe_tmp.mp4";
            try {
                const data = await self._safeFetchFile(url);
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
            const token = "sil" + (++self._silenceSessionSeq);
            const inName = token + "_in.mp4";
            try {
                reportProgress(onProgress, 8, "Loading clip…");
                const data = await self._safeFetchFile(url);
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
            const inName = "frame_in.mp4";
            const written = [];
            try {
                reportProgress(onProgress, 10, "Loading video for frames…");
                const data = await self._safeFetchFile(url);
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

    /**
     * Concatenate ordered background-music segment URLs (WAV) into one continuous AAC track.
     * Segments come from IAudioClient.MaxSegmentDurationSeconds-sized provider calls (see
     * FilmJobService's music job) — most scenes produce just one segment, handled as a no-op.
     * @param {string[]} urls
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    concatAudioSegmentsAsync: async function (urls, onProgress) {
        const list = Array.isArray(urls) ? urls.filter(u => typeof u === "string" && u.length > 0) : [];
        if (list.length === 0) return { success: false, error: "No audio URLs to combine" };
        if (list.length === 1) {
            reportProgress(onProgress, 100, "Ready");
            return { success: true, url: list[0], single: true };
        }

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;

            const ffmpeg = self._ffmpeg;
            let written = [];
            try {
                reportProgress(onProgress, 12, "Downloading music segments…");
                written = await self._writeSequentialInputsAsync(ffmpeg, list, "wav", onProgress, 12, 55);

                const listBody = written.map(n => "file '" + n + "'").join("\n");
                await ffmpeg.writeFile("music_list.txt", listBody);

                reportProgress(onProgress, 60, "Combining music…");
                await ffmpeg.exec([
                    "-f", "concat", "-safe", "0", "-i", "music_list.txt",
                    "-c:a", "aac", "-b:a", "192k",
                    "out_music.m4a",
                ]);

                reportProgress(onProgress, 90, "Preparing…");
                const url = await self._readAndCleanupAsync(
                    ffmpeg, "out_music.m4a", "audio/mp4", written.concat(["music_list.txt"]));
                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: url };
            } catch (err) {
                console.error("concatAudioSegmentsAsync failed:", err);
                for (const n of written) { try { await ffmpeg.deleteFile(n); } catch (_) { /* */ } }
                try { await ffmpeg.deleteFile("music_list.txt"); } catch (_) { /* */ }
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    /**
     * Layer a background-music track under a scene video with volume ducking + a 1.5s fade-out,
     * replacing the server-side ffmpeg filter_complex this feature used to run — the API host
     * never spawns native ffmpeg, so this composite step happens entirely in the browser now,
     * the same as clip/scene stitching.
     * @param {string} videoUrl
     * @param {string} musicUrl single (already-concatenated) music track URL
     * @param {number} volumePercent 0-100
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    mixSceneAudioAsync: async function (videoUrl, musicUrl, volumePercent, onProgress) {
        if (!videoUrl) return { success: false, error: "No video URL" };
        if (!musicUrl) return { success: true, url: videoUrl }; // nothing to mix — pass through

        const volRatio = Math.max(0.05, Math.min(1.0, (volumePercent != null ? volumePercent : 20) / 100));

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;

            const ffmpeg = self._ffmpeg;
            const inVideo = "mix_in_video.mp4";
            const inMusic = "mix_in_music.m4a";
            const outName = "mix_out.mp4";
            try {
                reportProgress(onProgress, 10, "Loading video…");
                await ffmpeg.writeFile(inVideo, await self._safeFetchFile(videoUrl));
                reportProgress(onProgress, 30, "Loading music…");
                await ffmpeg.writeFile(inMusic, await self._safeFetchFile(musicUrl));

                const probe = await self._probeDurationMemfsAsync(inVideo);
                const durationSec = probe.success && probe.seconds > 0 ? probe.seconds : 0;
                const fadeStart = Math.max(0, durationSec - 1.5);
                const musicFilter = "[1:a]volume=" + volRatio.toFixed(2) +
                    (durationSec > 0 ? ",afade=t=out:st=" + fadeStart.toFixed(1) + ":d=1.5" : "") +
                    "[bg]";

                reportProgress(onProgress, 50, "Mixing audio…");
                await ffmpeg.exec([
                    "-hide_banner", "-y",
                    "-i", inVideo, "-i", inMusic,
                    "-filter_complex", musicFilter + ";[0:a][bg]amix=inputs=2:duration=first[a]",
                    "-map", "0:v", "-map", "[a]",
                    "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                    "-shortest",
                    outName,
                ]);

                reportProgress(onProgress, 90, "Preparing player…");
                const url = await self._readAndCleanupAsync(ffmpeg, outName, "video/mp4", [inVideo, inMusic]);
                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: url };
            } catch (err) {
                console.error("mixSceneAudioAsync failed:", err);
                for (const n of [inVideo, inMusic, outName]) { try { await ffmpeg.deleteFile(n); } catch (_) { /* */ } }
                return { success: false, error: err.message || String(err) };
            }
        });
    },
};
