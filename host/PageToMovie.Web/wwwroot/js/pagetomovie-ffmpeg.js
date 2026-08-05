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
    _trimTailSeq: 0,
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

    /**
     * SHA-256 hex of the media at url (blob: or http). Used for film_build.studio.sha256.
     * @param {string} url
     * @returns {Promise<{ success:boolean, sha256?:string, byteLength?:number, error?:string }>}
     */
    hashUrlAsync: async function (url) {
        if (!url) return { success: false, error: "No URL" };
        try {
            const resp = await fetch(url);
            if (!resp.ok) return { success: false, error: "fetch " + resp.status };
            const buf = await resp.arrayBuffer();
            const digest = await crypto.subtle.digest("SHA-256", buf);
            const bytes = new Uint8Array(digest);
            let hex = "";
            for (let i = 0; i < bytes.length; i++)
                hex += bytes[i].toString(16).padStart(2, "0");
            return { success: true, sha256: hex, byteLength: buf.byteLength };
        } catch (err) {
            return { success: false, error: (err && err.message) ? err.message : String(err) };
        }
    },

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
                console.log("[concat] stitched " + written.length + " clips");

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
                // Hash the stitched bytes for film_build.studio.sha256 before handing out the URL.
                let sha256 = null;
                let byteLength = null;
                try {
                    const ab = await blob.arrayBuffer();
                    byteLength = ab.byteLength;
                    const dig = await crypto.subtle.digest("SHA-256", ab);
                    const bytes = new Uint8Array(dig);
                    sha256 = "";
                    for (let i = 0; i < bytes.length; i++)
                        sha256 += bytes[i].toString(16).padStart(2, "0");
                    // Re-wrap so the blob URL still works after arrayBuffer()
                    blob = new Blob([ab], { type: blob.type || "video/mp4" });
                } catch (hashErr) {
                    self._log("stitch sha256 skipped: " + (hashErr && hashErr.message));
                }

                self._blobUrl = URL.createObjectURL(blob);

                // Cleanup MEMFS
                for (const n of written) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                try { await ffmpeg.deleteFile("list.txt"); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }

                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: self._blobUrl, count: list.length, sha256: sha256, byteLength: byteLength };
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

    /**
     * Run ffmpeg `silencedetect` over an in-MEMFS clip and return its raw log lines. The caller
     * parses `silence_start` / `silence_end` from the log (see parseSilenceLog / ClipSilenceTrimmer).
     * noiseDb (e.g. -30) and minSilenceSec (e.g. 0.3) are the silencedetect thresholds.
     * @returns {{ success:boolean, log?:string, error?:string }}
     */
    _silenceDetectMemfsAsync: async function (inName, noiseDb, minSilenceSec) {
        const ffmpeg = this._ffmpeg;
        const db = (typeof noiseDb === "number" && noiseDb < 0) ? noiseDb : -30;
        const minSil = (typeof minSilenceSec === "number" && minSilenceSec > 0) ? minSilenceSec : 0.3;
        let log = "";
        const logHandler = ({ message }) => {
            if (typeof message === "string" && message.indexOf("silence_") >= 0) {
                log += message + "\n";
            }
        };
        ffmpeg.on("log", logHandler);
        try {
            // -af silencedetect writes silence_start/silence_end to the log; -f null discards output.
            await ffmpeg.exec([
                "-hide_banner",
                "-i", inName,
                "-af", "silencedetect=noise=" + db + "dB:d=" + minSil,
                "-f", "null", "-",
            ]);
        } catch (err) {
            ffmpeg.off("log", logHandler);
            return { success: false, error: (err && err.message) ? err.message : String(err) };
        }
        ffmpeg.off("log", logHandler);
        return { success: true, log: log };
    },

    /**
     * Detect the NON-SILENT (speech) windows of a clip via silencedetect, returned as
     * [{ startSec, endSec }] in clip time. This is the free, local, PRIMARY timestamp source for
     * voice substitution — the known dialogue lines from the shot plan are matched onto these
     * windows server-side (VoiceAlignmentStore.MatchSegmentsToLines).
     * @param {string} url clip URL (blob: or http)
     * @param {{noiseDb?:number,minSilenceSec?:number}} [opts]
     * @returns {{ success:boolean, totalSec?:number, segments?:{startSec:number,endSec:number}[], error?:string }}
     */
    detectSpeechSegmentsAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, error: "No URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return { success: false, error: load.error || "ffmpeg load failed" };

            const ffmpeg = self._ffmpeg;
            const inName = "speechdet_in.mp4";
            try {
                reportProgress(onProgress, 10, "Loading clip…");
                await ffmpeg.writeFile(inName, await self._safeFetchFile(url));

                reportProgress(onProgress, 30, "Probing duration…");
                const probe = await self._probeDurationMemfsAsync(inName);
                const totalSec = probe.success && probe.seconds > 0 ? probe.seconds : 0;

                reportProgress(onProgress, 55, "Detecting speech…");
                const det = await self._silenceDetectMemfsAsync(inName, opts.noiseDb, opts.minSilenceSec);
                if (!det.success) {
                    return { success: false, error: det.error || "silence detect failed" };
                }

                const segments = self._invertSilenceToSpeech(det.log || "", totalSec, opts.minSilenceSec);
                reportProgress(onProgress, 100, "Speech detected");
                return { success: true, totalSec: totalSec, segments: segments };
            } catch (err) {
                return { success: false, error: (err && err.message) ? err.message : String(err) };
            } finally {
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
            }
        });
    },

    /**
     * Turn a silencedetect log into non-silent [start,end] windows over [0,totalSec].
     * Silence runs are the complement of speech; a clip with no detected silence is one speech run.
     */
    _invertSilenceToSpeech: function (log, totalSec, minSilenceSec) {
        const total = totalSec > 0 ? totalSec : 0;
        const minGap = (typeof minSilenceSec === "number" && minSilenceSec > 0) ? minSilenceSec : 0.3;
        // Collect (start,end) silence intervals from the log.
        const silences = [];
        let curStart = null;
        const lines = String(log).split("\n");
        for (const line of lines) {
            let m = line.match(/silence_start:\s*(-?\d+(?:\.\d+)?)/);
            if (m) { curStart = Math.max(0, parseFloat(m[1])); continue; }
            m = line.match(/silence_end:\s*(-?\d+(?:\.\d+)?)/);
            if (m) {
                const end = parseFloat(m[1]);
                if (curStart !== null) { silences.push([curStart, end]); curStart = null; }
            }
        }
        if (curStart !== null && total > 0) silences.push([curStart, total]);

        // Speech = complement of silence within [0,total].
        if (total <= 0) {
            // Unknown duration: fall back to a single open window if any speech implied.
            return silences.length === 0 ? [] : [];
        }
        const speech = [];
        let cursor = 0;
        for (const [s, e] of silences) {
            const gs = Math.max(0, s);
            if (gs - cursor > 0.05) speech.push({ startSec: cursor, endSec: gs });
            cursor = Math.max(cursor, Math.min(total, e));
        }
        if (total - cursor > 0.05) speech.push({ startSec: cursor, endSec: total });

        // Merge windows separated by less than minGap (avoids chopping one line into fragments).
        const merged = [];
        for (const w of speech) {
            if (merged.length > 0 && w.startSec - merged[merged.length - 1].endSec < minGap) {
                merged[merged.length - 1].endSec = w.endSec;
            } else {
                merged.push({ startSec: w.startSec, endSec: w.endSec });
            }
        }
        return merged;
    },

    /**
     * Overlay cloned-voice speech clips onto a video's ORIGINAL audio at given time windows, ducking
     * (lowering) the original only during those windows so ambience/music/SFX stay intact everywhere
     * else. This is the client-side compose step for voice substitution — the API host never spawns
     * native ffmpeg.
     *
     * Each segment: { audioUrl, startSec, endSec }. The cloned audio is delayed to startSec; the
     * original track is ducked via a volume envelope that dips inside each [startSec,endSec] window.
     * If a cloned line is longer than its window it simply plays past it (over ducked original); a
     * future enhancement can atempo-fit it to the window (see design doc TODO).
     *
     * @param {string} videoUrl
     * @param {{audioUrl:string,startSec:number,endSec:number}[]} segments
     * @param {{duckVolume?:number}} [opts] duckVolume 0-1 for original during speech (default 0.15)
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    overlayVoiceSegmentsAsync: async function (videoUrl, segments, opts, onProgress) {
        if (!videoUrl) return { success: false, error: "No video URL" };
        const list = Array.isArray(segments) ? segments.filter(s => s && s.audioUrl) : [];
        if (list.length === 0) return { success: true, url: videoUrl }; // nothing to overlay

        opts = opts || {};
        const duck = Math.max(0, Math.min(1, opts.duckVolume != null ? opts.duckVolume : 0.15));
        // muteBase: drop the original clip audio entirely and use the cloned voice as the whole
        // soundtrack (narrator-only scenes) — no bed to duck, so no double voice.
        const muteBase = !!opts.muteBase;

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;

            const ffmpeg = self._ffmpeg;
            const inVideo = "ov_in_video.mp4";
            const outName = "ov_out.mp4";
            const audioNames = [];
            try {
                reportProgress(onProgress, 8, "Loading picture…");
                await ffmpeg.writeFile(inVideo, await self._safeFetchFile(videoUrl));

                console.log("[dub] overlay: " + list.length + " voice segment(s)");

                // Write + PRE-DECODE each cloned-voice clip to a clean 48 kHz stereo WAV. Feeding the
                // raw TTS mp3 straight into amix was unreliable: if ffmpeg.wasm couldn't decode that
                // particular mp3 the mix silently fell back to just the (ducked) base — "background,
                // no voice". Transcoding first makes decode failures loud and hands amix known-good PCM.
                for (let i = 0; i < list.length; i++) {
                    reportProgress(onProgress, 8 + Math.round((i / list.length) * 22),
                        "Loading voice " + (i + 1) + "/" + list.length + "…");
                    const seg = list[i];
                    // Flat log (survives console export) so bad timing is visible: start/end vs clip.
                    console.log("[dub] seg " + i + ": start=" + seg.startSec + "s end=" + seg.endSec + "s");
                    let ext = ".mp3";
                    if (/\.wav(\?|$)/i.test(seg.audioUrl) || (seg.audioUrl.indexOf("audio/wav") >= 0)) ext = ".wav";
                    else if (/\.m4a(\?|$)/i.test(seg.audioUrl) || (seg.audioUrl.indexOf("audio/mp4") >= 0)) ext = ".m4a";
                    const rawName = "ov_voice_raw_" + i + ext;
                    const wavName = "ov_voice_" + i + ".wav";
                    const bytes = await self._safeFetchFile(seg.audioUrl);
                    console.log("[dub] voice " + i + ": " + (bytes ? bytes.length : 0) + " bytes");
                    if (!bytes || bytes.length < 512) {
                        console.warn("[dub] voice " + i + " suspiciously small — TTS likely returned silence/empty.");
                    }
                    await ffmpeg.writeFile(rawName, bytes);
                    try {
                        await ffmpeg.exec(["-hide_banner", "-y", "-i", rawName,
                            "-ar", "48000", "-ac", "2", wavName]);
                    } catch (decErr) {
                        console.error("[dub] voice " + i + " decode→wav FAILED:", decErr && decErr.message);
                        try { await ffmpeg.deleteFile(rawName); } catch (_) { /* */ }
                        throw new Error("Cloned voice audio could not be decoded (segment " + i + ")");
                    }
                    try { await ffmpeg.deleteFile(rawName); } catch (_) { /* */ }
                    audioNames.push(wavName);
                }

                // Build filter_complex — deliberately simple. The earlier version used a per-frame
                // volume='if(between(t,…))' envelope on the bed plus an un-padded, short second input;
                // in ffmpeg.wasm that combination produced a mix where the (verified ~-28 dB) voice was
                // absent while the bed survived. This version drops both fragile pieces:
                //  - bed: constant gentle duck (no per-frame expression).
                //  - voice: format-normalized, delayed on ALL channels, boosted, and apad-ed to full
                //    length so amix never drops it partway.
                const inputs = ["-i", inVideo];
                for (const n of audioNames) inputs.push("-i", n);

                // aformat first — amix does not resample, so a rate/layout mismatch silences an input.
                const fmt = "aformat=sample_rates=48000:channel_layouts=stereo";

                const parts = [];
                let filter;
                if (muteBase) {
                    // Narrator-only scene: original clip audio is dropped; the cloned narration is the
                    // whole soundtrack. Rather than warp each line to its own window (varying pace), we
                    // calibrate ONE stretch factor from up to 3 sample points (median of natural ÷ window
                    // ratios) so the voice keeps a single consistent pace matched to the original speaker,
                    // then place each line at its window start via a leading silence and mix.
                    const segInfo = [];
                    for (let i = 0; i < list.length; i++) {
                        const seg = list[i];
                        const startSec = Math.max(0, +seg.startSec || 0);
                        const targetDur = Math.max(0.2, (+seg.endSec || 0) - startSec);
                        const probe = await self._probeDurationMemfsAsync(audioNames[i]);
                        const natSec = probe && probe.success && probe.seconds > 0 ? probe.seconds : targetDur;
                        segInfo.push({ i: i, startSec: startSec, natSec: natSec, ratio: natSec / targetDur });
                    }

                    // Sample up to 3 points (first / middle / last for a representative spread), take the
                    // median ratio → one calibrated stretch factor (atempo). Clamp so we never warble.
                    const sample = segInfo.length <= 3
                        ? segInfo.slice()
                        : [segInfo[0], segInfo[Math.floor(segInfo.length / 2)], segInfo[segInfo.length - 1]];
                    const ratios = sample.map(s => s.ratio).sort((a, b) => a - b);
                    let tempo = ratios.length ? ratios[Math.floor(ratios.length / 2)] : 1.0;
                    tempo = Math.max(0.5, Math.min(2.0, tempo));
                    console.log("[dub] calibrated stretch factor: " + tempo.toFixed(3) +
                        " (from " + sample.length + " of " + segInfo.length + " line(s))");

                    const vl = [];
                    for (const s of segInfo) {
                        const voice = "[" + (s.i + 1) + ":a]" + fmt + ",atempo=" + tempo.toFixed(4) +
                            ",volume=1.3,asetpts=PTS-STARTPTS";
                        if (s.startSec >= 0.05) {
                            parts.push("anullsrc=channel_layout=stereo:sample_rate=48000,atrim=duration=" +
                                s.startSec.toFixed(3) + ",asetpts=PTS-STARTPTS[sil" + s.i + "]");
                            parts.push(voice + "[sv" + s.i + "]");
                            parts.push("[sil" + s.i + "][sv" + s.i + "]concat=n=2:v=0:a=1[v" + s.i + "]");
                        } else {
                            parts.push(voice + "[v" + s.i + "]");
                        }
                        vl.push("[v" + s.i + "]");
                    }
                    if (vl.length === 1)
                        parts.push(vl[0] + "apad[a]");
                    else
                        parts.push(vl.join("") + "amix=inputs=" + vl.length +
                            ":duration=longest:normalize=0,apad[a]");
                    filter = parts.join(";");
                } else {
                    // Duck the bed to ~0.30 so the narrator sits on top (ambience/music still audible).
                    parts.push("[0:a]" + fmt + ",volume=0.30[base]");
                    const mixLabels = ["[base]"];
                    for (let i = 0; i < list.length; i++) {
                        // No adelay (it zeroes the voice in this ffmpeg.wasm build); the narrator plays
                        // from the clip start, amix silence-pads the tail. Boost ~2.2× to sit over the bed.
                        parts.push("[" + (i + 1) + ":a]" + fmt + ",volume=2.2[v" + i + "]");
                        mixLabels.push("[v" + i + "]");
                    }
                    parts.push(mixLabels.join("") + "amix=inputs=" + mixLabels.length +
                        ":duration=first:normalize=0[a]");
                    filter = parts.join(";");
                }
                console.log("[dub] filter" + (muteBase ? " (muteBase)" : "") + ": " + filter);

                reportProgress(onProgress, 45, "Overlaying voice…");
                await ffmpeg.exec([
                    "-hide_banner", "-y",
                    ...inputs,
                    "-filter_complex", filter,
                    "-map", "0:v", "-map", "[a]",
                    "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                    "-shortest",
                    outName,
                ]);

                reportProgress(onProgress, 90, "Saving clip…");
                const url = await self._readAndCleanupAsync(
                    ffmpeg, outName, "video/mp4", [inVideo].concat(audioNames));
                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: url };
            } catch (err) {
                console.error("overlayVoiceSegmentsAsync failed:", err);
                for (const n of [inVideo, outName].concat(audioNames)) {
                    try { await ffmpeg.deleteFile(n); } catch (_) { /* */ }
                }
                return { success: false, error: err.message || String(err) };
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

    // Trims a video down to its last `keepSeconds` — used to prepare a video-extend continuation
    // source (see FilmJobService.GenerateOneClipAsync): the model rejects input video longer than
    // its own max clip length, so the client keeps only the tail before uploading it. Standalone
    // (not tied to the silence-trim session bookkeeping that encodeSliceAsync uses) since the
    // caller only ever wants one trim, not an analyze-then-slice round trip.
    trimTailAsync: async function (url, keepSeconds, onProgress) {
        if (!url) return { success: false, error: "No URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return { success: false, error: load.error };

            const ffmpeg = self._ffmpeg;
            const seq = ++self._trimTailSeq;
            const inName = "trimtail_in_" + seq + ".mp4";
            const outName = "trimtail_out_" + seq + ".mp4";
            try {
                reportProgress(onProgress, 10, "Loading clip…");
                const data = await self._safeFetchFile(url);
                await ffmpeg.writeFile(inName, data);

                reportProgress(onProgress, 30, "Probing duration…");
                const probe = await self._probeDurationMemfsAsync(inName);
                if (!probe.success || !(probe.seconds > 0)) {
                    return { success: false, error: "Could not read source duration" };
                }

                const totalSec = probe.seconds;
                const keepSec = Math.max(0.5, Math.min(keepSeconds, totalSec));
                const startSec = Math.max(0, totalSec - keepSec);

                reportProgress(onProgress, 55, "Trimming tail…");
                const args = ["-hide_banner", "-y"];
                if (startSec > 0.001) args.push("-ss", String(startSec));
                args.push("-i", inName);
                args.push("-t", String(keepSec));
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
                reportProgress(onProgress, 100, "Trim done");
                return { success: true, url: outUrl, sourceDurationSec: totalSec, keptSec: keepSec };
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

    /**
     * Strip all original audio from a clip and replace with a single TTS (or other) track.
     * Video stream is copied; new audio is AAC. If TTS is shorter than video, pad with silence;
     * if longer, cut to video length (-shortest against padded audio matching video duration).
     * @param {string} videoUrl blob: or http(s) URL
     * @param {string} audioUrl blob: / data: / http(s) URL for the replacement speech
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    replaceVideoAudioAsync: async function (videoUrl, audioUrl, onProgress) {
        if (!videoUrl) return { success: false, error: "No video URL" };
        if (!audioUrl) return { success: false, error: "No audio URL" };

        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;

            const ffmpeg = self._ffmpeg;
            const inVideo = "rv_in_video.mp4";
            const inAudio = "rv_in_audio";
            const outName = "rv_out.mp4";
            try {
                reportProgress(onProgress, 8, "Loading picture…");
                await ffmpeg.writeFile(inVideo, await self._safeFetchFile(videoUrl));
                reportProgress(onProgress, 28, "Loading voice…");
                // Keep extension so ffmpeg can sniff container (mp3/wav/m4a)
                let audioName = inAudio + ".bin";
                if (typeof audioUrl === "string") {
                    if (audioUrl.indexOf("audio/wav") >= 0 || /\.wav(\?|$)/i.test(audioUrl)) audioName = inAudio + ".wav";
                    else if (audioUrl.indexOf("audio/mp4") >= 0 || /\.m4a(\?|$)/i.test(audioUrl)) audioName = inAudio + ".m4a";
                    else if (audioUrl.indexOf("audio/mpeg") >= 0 || /\.mp3(\?|$)/i.test(audioUrl)) audioName = inAudio + ".mp3";
                    else audioName = inAudio + ".mp3";
                }
                await ffmpeg.writeFile(audioName, await self._safeFetchFile(audioUrl));

                const probe = await self._probeDurationMemfsAsync(inVideo);
                const durationSec = probe.success && probe.seconds > 0 ? probe.seconds : 0;

                reportProgress(onProgress, 50, "Replacing audio…");
                // Drop original audio entirely; use TTS only. Pad TTS with silence to video length
                // when known so picture does not cut short; -shortest still guards runaway audio.
                if (durationSec > 0.05) {
                    const filter =
                        "[1:a]aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=mono," +
                        "apad=whole_dur=" + durationSec.toFixed(3) + "[a]";
                    await ffmpeg.exec([
                        "-hide_banner", "-y",
                        "-i", inVideo, "-i", audioName,
                        "-filter_complex", filter,
                        "-map", "0:v", "-map", "[a]",
                        "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                        "-t", durationSec.toFixed(3),
                        outName,
                    ]);
                } else {
                    await ffmpeg.exec([
                        "-hide_banner", "-y",
                        "-i", inVideo, "-i", audioName,
                        "-map", "0:v", "-map", "1:a",
                        "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                        "-shortest",
                        outName,
                    ]);
                }

                reportProgress(onProgress, 90, "Saving clip…");
                const url = await self._readAndCleanupAsync(
                    ffmpeg, outName, "video/mp4", [inVideo, audioName]);
                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: url };
            } catch (err) {
                console.error("replaceVideoAudioAsync failed:", err);
                for (const n of [inVideo, outName]) { try { await ffmpeg.deleteFile(n); } catch (_) { /* */ } }
                return { success: false, error: err.message || String(err) };
            }
        });
    },

    /**
     * Strip all audio from a video (silent picture). Used when a clip has no dialogue.
     * @param {string} videoUrl
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    stripVideoAudioAsync: async function (videoUrl, onProgress) {
        if (!videoUrl) return { success: false, error: "No video URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) return load;
            const ffmpeg = self._ffmpeg;
            const inVideo = "sa_in.mp4";
            const outName = "sa_out.mp4";
            try {
                reportProgress(onProgress, 20, "Loading picture…");
                await ffmpeg.writeFile(inVideo, await self._safeFetchFile(videoUrl));
                reportProgress(onProgress, 55, "Removing audio…");
                await ffmpeg.exec([
                    "-hide_banner", "-y",
                    "-i", inVideo,
                    "-map", "0:v", "-an",
                    "-c:v", "copy",
                    outName,
                ]);
                reportProgress(onProgress, 90, "Saving…");
                const url = await self._readAndCleanupAsync(ffmpeg, outName, "video/mp4", [inVideo]);
                reportProgress(onProgress, 100, "Ready");
                return { success: true, url: url };
            } catch (err) {
                console.error("stripVideoAudioAsync failed:", err);
                for (const n of [inVideo, outName]) { try { await ffmpeg.deleteFile(n); } catch (_) { /* */ } }
                return { success: false, error: err.message || String(err) };
            }
        });
    },
};
