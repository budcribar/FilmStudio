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

    /**
     * Re-encode a slice of a video URL (client-side silence trim / extend-tail).
     * @param {string} url
     * @param {{ startSec?:number, durationSec?:number }} opts  startSec = -ss, durationSec = -t
     * @returns {{ success:boolean, url?:string, error?:string }}
     */
    trimVideoAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, error: "No URL" };
        const self = this;
        return this._runExclusiveAsync(async function () {
            return self._trimVideoUnlockedAsync(url, opts, onProgress);
        });
    },

    /** Must hold exclusive lock. */
    _trimVideoUnlockedAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        const load = await this.ensureLoadedAsync(onProgress);
        if (!load.success) return load;
        const ffmpeg = this._ffmpeg;
        const fetchFile = (window.FFmpegUtil || {}).fetchFile;
        if (typeof fetchFile !== "function")
            return { success: false, error: "ffmpeg util fetchFile missing" };

        try {
            onProgress && onProgress(10, "Downloading…");
            const data = await fetchFile(url);
            await ffmpeg.writeFile("in.mp4", data);
            onProgress && onProgress(40, "Trimming…");
            const args = ["-hide_banner", "-y"];
            if (opts.startSec != null && opts.startSec > 0.001)
                args.push("-ss", String(opts.startSec));
            args.push("-i", "in.mp4");
            if (opts.durationSec != null && opts.durationSec > 0.05)
                args.push("-t", String(opts.durationSec));
            args.push(
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                "-c:a", "aac", "-b:a", "128k",
                "-movflags", "+faststart",
                "out.mp4");
            await ffmpeg.exec(args);
            onProgress && onProgress(90, "Preparing…");
            const out = await ffmpeg.readFile("out.mp4");
            const blob = new Blob([out.buffer], { type: "video/mp4" });
            this.revokePreviewUrl();
            this._blobUrl = URL.createObjectURL(blob);
            try { await ffmpeg.deleteFile("in.mp4"); } catch (_) { /* */ }
            try { await ffmpeg.deleteFile("out.mp4"); } catch (_) { /* */ }
            onProgress && onProgress(100, "Ready");
            return { success: true, url: this._blobUrl };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    /**
     * Extract the last tailSec seconds of a video (extend-tail / 15s clamp equivalent).
     */
    extractTailAsync: async function (url, tailSec, onProgress) {
        const self = this;
        return this._runExclusiveAsync(async function () {
            const probe = await self._probeDurationUnlockedAsync(url);
            if (!probe.success || !(probe.seconds > 0))
                return { success: false, error: probe.error || "Could not probe duration" };
            const keep = Math.max(0.5, tailSec || 1);
            const start = Math.max(0, probe.seconds - keep);
            return self._trimVideoUnlockedAsync(url, { startSec: start, durationSec: keep }, onProgress);
        });
    },

    // ── Silence trim (port of ClipSilenceTrimmer.cs) ──────────────────────
    MinClipSeconds: 3,
    DefaultKeepTailSeconds: 0.35,
    SpeechBreathTailSeconds: 0.90,

    _parseSilenceLog: function (log) {
        const starts = [];
        const ends = [];
        const reS = /silence_start:\s*([0-9]+(?:\.[0-9]+)?)/gi;
        const reE = /silence_end:\s*([0-9]+(?:\.[0-9]+)?)/gi;
        let m;
        while ((m = reS.exec(log || "")) !== null) starts.push(parseFloat(m[1]));
        while ((m = reE.exec(log || "")) !== null) ends.push(parseFloat(m[1]));
        starts.sort((a, b) => a - b);
        ends.sort((a, b) => a - b);
        return { starts, ends };
    },

    /** Port of ClipSilenceTrimmer.ComputeCutPoint */
    computeCutPoint: function (silenceLog, totalDuration, keepTailSeconds) {
        if (!silenceLog || !(totalDuration >= 1.0) || !isFinite(totalDuration)) return null;
        const { starts, ends } = this._parseSilenceLog(silenceLog);
        if (starts.length === 0) return null;

        let trailStart = null;
        for (const s of starts) {
            if (!ends.some(e => e > s + 0.05))
                trailStart = s;
        }
        if (trailStart == null && ends.length > 0 && starts.length > 0) {
            const lastEnd = ends[ends.length - 1];
            if (totalDuration - lastEnd < 0.35) {
                for (let i = starts.length - 1; i >= 0; i--) {
                    if (starts[i] < lastEnd) {
                        trailStart = starts[i];
                        break;
                    }
                }
            }
        }
        if (trailStart == null) return null;
        const silenceTail = totalDuration - trailStart;
        if (silenceTail < 0.35) return null;
        let cut = trailStart + keepTailSeconds;
        cut = Math.min(cut, totalDuration - 0.05);
        if (cut >= totalDuration - 0.2) return null;
        if (cut < this.MinClipSeconds - 0.25) return null;
        return cut;
    },

    /** Port of ClipSilenceTrimmer.ComputeLeadInPoint */
    computeLeadInPoint: function (silenceLog, totalDuration, keepHeadSeconds) {
        if (!silenceLog || !(totalDuration >= 1.0)) return null;
        const { starts, ends } = this._parseSilenceLog(silenceLog);
        if (starts.length === 0 || starts[0] > 0.35) {
            if (ends.length > 0 && ends[0] > 0.3 && ends[0] < totalDuration * 0.5 &&
                (starts.length === 0 || starts[0] > ends[0])) {
                const cut = Math.max(0, ends[0] - keepHeadSeconds);
                if (cut >= 0.2 && totalDuration - cut >= this.MinClipSeconds - 0.25)
                    return cut;
            }
            return null;
        }
        const leadStart = starts[0];
        const end = ends.find(e => e > leadStart + 0.05);
        if (end == null || end <= leadStart) return null;
        const leadLen = end - Math.max(0, leadStart);
        if (leadLen < 0.25) return null;
        const startAt = Math.max(0, end - keepHeadSeconds);
        if (startAt < 0.2) return null;
        if (totalDuration - startAt < this.MinClipSeconds - 0.25) return null;
        return startAt;
    },

    /**
     * Run silencedetect on a video URL; returns stderr log text.
     */
    silenceDetectAsync: async function (url, noiseDb, minSilenceSec) {
        const self = this;
        return this._runExclusiveAsync(async function () {
            return self._silenceDetectUnlockedAsync(url, noiseDb, minSilenceSec);
        });
    },

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

    _silenceDetectUnlockedAsync: async function (url, noiseDb, minSilenceSec) {
        const load = await this.ensureLoadedAsync();
        if (!load.success) return { success: false, error: load.error };
        const ffmpeg = this._ffmpeg;
        const fetchFile = (window.FFmpegUtil || {}).fetchFile;
        if (typeof fetchFile !== "function")
            return { success: false, error: "ffmpeg util fetchFile missing" };
        try {
            const data = await fetchFile(url);
            await ffmpeg.writeFile("sil_in.mp4", data);
            const r = await this._silenceDetectMemfsAsync("sil_in.mp4", noiseDb, minSilenceSec);
            try { await ffmpeg.deleteFile("sil_in.mp4"); } catch (_) { /* */ }
            return r;
        } catch (err) {
            return { success: false, error: err.message || String(err) };
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
     * Trim trailing (and optional leading) silence from a clip URL.
     * Port of server ClipSilenceTrimmer pipeline for ffmpeg.wasm.
     * Single download + exclusive MEMFS pipeline (probe → detect → re-encode).
     *
     * @param {string} url blob: or http(s) media URL
     * @param {{
     *   keepTailSeconds?: number,
     *   keepHeadSeconds?: number,
     *   trimLeading?: boolean,
     *   minTrimSavings?: number,
     *   noiseDb?: number,
     *   minSilenceSec?: number
     * }} [opts]
     * @returns {{ success:boolean, url?:string, trimmed?:boolean, beforeSec?:number, afterSec?:number, message?:string, error?:string }}
     */
    silenceTrimClipAsync: async function (url, opts, onProgress) {
        opts = opts || {};
        if (!url) return { success: false, error: "No URL" };

        const self = this;
        return this._runExclusiveAsync(async function () {
            const keepTail = opts.keepTailSeconds != null
                ? opts.keepTailSeconds
                : self.DefaultKeepTailSeconds;
            const keepHead = opts.keepHeadSeconds != null ? opts.keepHeadSeconds : 0.08;
            const trimLeading = !!opts.trimLeading;
            const minTailSave = opts.minTrimSavings != null ? opts.minTrimSavings : 0.4;
            const minHeadSave = 0.25;

            const load = await self.ensureLoadedAsync(onProgress);
            if (!load.success) {
                return {
                    success: true,
                    url: url,
                    trimmed: false,
                    message: "skip: ffmpeg load failed — " + (load.error || ""),
                };
            }

            const ffmpeg = self._ffmpeg;
            const fetchFile = (window.FFmpegUtil || {}).fetchFile;
            if (typeof fetchFile !== "function") {
                return {
                    success: true,
                    url: url,
                    trimmed: false,
                    message: "skip: ffmpeg util missing",
                };
            }

            const inName = "sil_in.mp4";
            const outName = "sil_out.mp4";
            try {
                onProgress && onProgress(8, "Loading clip…");
                const data = await fetchFile(url);
                await ffmpeg.writeFile(inName, data);

                onProgress && onProgress(18, "Probing duration…");
                const probe = await self._probeDurationMemfsAsync(inName);
                if (!probe.success || !(probe.seconds > 1.5)) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true,
                        url: url,
                        trimmed: false,
                        beforeSec: probe.seconds,
                        afterSec: probe.seconds,
                        message: "skip: duration unknown or too short",
                    };
                }
                const total = probe.seconds;

                onProgress && onProgress(30, "Detecting silence…");
                const det = await self._silenceDetectMemfsAsync(
                    inName, opts.noiseDb, opts.minSilenceSec);
                if (!det.success) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true,
                        url: url,
                        trimmed: false,
                        beforeSec: total,
                        afterSec: total,
                        message: "skip: silence detect failed — " + (det.error || ""),
                    };
                }

                let startSec = 0;
                let endSec = total;
                const notes = [];

                const cutAt = self.computeCutPoint(det.log, total, keepTail);
                if (cutAt != null && (total - cutAt) >= minTailSave) {
                    endSec = cutAt;
                    notes.push("tail −" + (total - cutAt).toFixed(2) + "s");
                }

                if (trimLeading) {
                    const lead = self.computeLeadInPoint(det.log, total, keepHead);
                    if (lead != null && lead >= minHeadSave &&
                        endSec - lead >= self.MinClipSeconds - 0.25) {
                        startSec = lead;
                        notes.push("head −" + lead.toFixed(2) + "s");
                    }
                }

                if (startSec <= 0.001 && endSec >= total - 0.05) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    return {
                        success: true,
                        url: url,
                        trimmed: false,
                        beforeSec: total,
                        afterSec: total,
                        message: notes.length ? notes.join("; ") : "skip: no trailing/leading silence",
                    };
                }

                const durationSec = Math.max(0.5, endSec - startSec);
                onProgress && onProgress(55, "Re-encoding trimmed clip…");
                const args = ["-hide_banner", "-y"];
                if (startSec > 0.001)
                    args.push("-ss", String(startSec));
                args.push("-i", inName);
                args.push("-t", String(durationSec));
                args.push(
                    "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
                    "-c:a", "aac", "-b:a", "128k",
                    "-movflags", "+faststart",
                    outName);
                try {
                    await ffmpeg.exec(args);
                } catch (encErr) {
                    try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                    try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }
                    return {
                        success: true,
                        url: url,
                        trimmed: false,
                        beforeSec: total,
                        afterSec: total,
                        message: "skip: re-encode failed — " + (encErr.message || String(encErr)),
                    };
                }

                onProgress && onProgress(90, "Preparing…");
                const out = await ffmpeg.readFile(outName);
                const blob = new Blob([out.buffer], { type: "video/mp4" });
                // Dedicated blob URL for the caller — do not share stitch preview slot
                // (caller should revoke after use).
                const outUrl = URL.createObjectURL(blob);
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }

                onProgress && onProgress(100, "Silence trim done");
                return {
                    success: true,
                    url: outUrl,
                    trimmed: true,
                    beforeSec: total,
                    afterSec: durationSec,
                    message: notes.length ? notes.join("; ") : "trimmed",
                };
            } catch (err) {
                try { await ffmpeg.deleteFile(inName); } catch (_) { /* */ }
                try { await ffmpeg.deleteFile(outName); } catch (_) { /* */ }
                return {
                    success: true,
                    url: url,
                    trimmed: false,
                    message: "skip: " + (err.message || String(err)),
                };
            }
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
