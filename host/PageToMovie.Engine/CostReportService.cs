using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Models;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PageToMovie.Tests")]

namespace PageToMovie.Engine;

/// <summary>
/// Cost ledger (pipeline_state.cost_ledger) + planning estimates from blueprint + list rates.
/// Rates come from <see cref="SupportedModelCatalog"/> for the project's selected video/image
/// models (vendor list prices), not free-form config dollars. Ledger events are still estimates,
/// not provider invoices.
/// </summary>
public sealed class CostReportService
{
    private static readonly Regex TimestampDur = new(
        @"^\s*(\d+):(\d{2})\s*-\s*(\d+):(\d{2})\s*$",
        RegexOptions.Compiled);

    private readonly ProjectStore _projects;
    private readonly CreditService? _credits;
    private readonly UserDatabaseService? _userDb;
    private readonly ClipTimingTelemetryRepository? _timingDb;

    private const int MinApiSamples = 8;
    private const int MinTimingSamples = 5;

    /// <summary>
    /// USD per reference image attached to a video generation call, used when no enabled video
    /// model publishes this as a distinct catalog line item (see
    /// <see cref="SupportedModelEntry.VideoReferenceImageCost"/>). As of 2026-08 no provider
    /// (including xAI's grok-imagine-video, per docs.x.ai/developers/pricing) itemizes reference
    /// images separately from its flat per-second output rate, so every catalog entry is null and
    /// this small Grok-era estimate is applied uniformly. Not vendor-verified — replace with a real
    /// catalog value the moment a provider publishes one.
    /// </summary>
    internal const double FallbackVideoRefImageCost = 0.002;

    /// <summary>
    /// USD per second billed for a video-extend/continuation call, used when no enabled video
    /// model publishes an extend-specific rate (see
    /// <see cref="SupportedModelEntry.VideoExtendCostPerSecond"/>). As of 2026-08 xAI — the only
    /// enabled provider with <see cref="SupportedModelEntry.SupportsVideoContinue"/> true — has no
    /// published extend rate distinct from its base per-second price on docs.x.ai/developers/pricing,
    /// so this small Grok-era estimate is applied uniformly. Not vendor-verified.
    /// </summary>
    internal const double FallbackVideoExtendCostPerSec = 0.01;

    public CostReportService(
        ProjectStore projects,
        CreditService? credits = null,
        UserDatabaseService? userDb = null,
        ClipTimingTelemetryRepository? timingDb = null)
    {
        _projects = projects;
        _credits = credits;
        _userDb = userDb;
        _timingDb = timingDb;
    }

    public async Task<CostReport> GetReportAsync(
        string projectId,
        string? draftResolution = null,
        string? heroResolution = null,
        double? assumeAvgRetries = null,
        int recentLimit = 200,
        CancellationToken ct = default)
    {
        var cfg = await LoadConfigMapAsync(projectId, ct).ConfigureAwait(false);
        var rates = RatesFromConfig(cfg);
        var draftRes = draftResolution
            ?? GetStr(cfg, "resolution", "480p");
        var heroRes = heroResolution ?? "720p";
        var retries = assumeAvgRetries
            ?? GetDouble(rates, "assume_avg_retries", 0);

        // Quality Gate Retry (config) + history-refined video multiplier.
        var qaRetryOnFail = GetCfgBool(cfg, "qa_retry_on_fail", defaultValue: true);
        var qaMaxRetries = GetCfgInt(cfg, "qa_max_retries", defaultValue: 1);
        qaMaxRetries = Math.Clamp(qaMaxRetries, 0, 5);
        var priorVideoMultiplier = 1.3;
        if (cfg.TryGetValue("cost_estimates", out var ceQa) && ceQa.ValueKind == JsonValueKind.Object)
        {
            if (ceQa.TryGetProperty("qa_retry_video_multiplier", out var qm) &&
                qm.TryGetDouble(out var qmv) && qmv >= 1.0)
                priorVideoMultiplier = Math.Clamp(qmv, 1.0, 3.0);
            else if (ceQa.TryGetProperty("qa_fail_rate", out var fr) && fr.TryGetDouble(out var frv) && frv >= 0)
                priorVideoMultiplier = 1.0 + Math.Clamp(frv, 0, 1) * Math.Max(1, qaMaxRetries);
        }

        var ledger = await GetCostLedgerAsync(projectId, ct).ConfigureAwait(false);
        var actual = SummarizeLedger(ledger);

        var refinement = await BuildHistoryRefinementAsync(
            projectId, priorVideoMultiplier, qaRetryOnFail, qaMaxRetries, actual, ct)
            .ConfigureAwait(false);
        var qaVideoMultiplier = qaRetryOnFail ? refinement.AppliedVideoMultiplier : 1.0;
        var qaExpectedExtraGens = qaRetryOnFail ? Math.Max(0, qaVideoMultiplier - 1.0) : 0.0;
        if (qaExpectedExtraGens > retries)
            retries = qaExpectedExtraGens;

        var blueprintClips = await LoadBlueprintClipsAsync(projectId, ct).ConfigureAwait(false);
        var estimateBasis = blueprintClips.Any(s => s.Clips.Count > 0) ? "shot_plan" : "none";
        if (estimateBasis == "none")
        {
            // Post-import (before shot plan): derive clip count from screenplay scene durations.
            blueprintClips = await LoadScreenplayDerivedClipsAsync(projectId, cfg, ct).ConfigureAwait(false);
            if (blueprintClips.Any(s => s.Clips.Count > 0))
                estimateBasis = "screenplay";
        }

        var onDisk = IndexOnDiskClips(projectId);
        var heroes = await LoadHeroMapAsync(projectId, ct).ConfigureAwait(false);

        var draftCfg = CloneCfg(cfg, draftRes, retries);
        var heroCfg = CloneCfg(cfg, heroRes, retries);
        var draftRates = RatesFromConfig(draftCfg);
        var heroRates = RatesFromConfig(heroCfg);

        double spent = 0, remainingDraft = 0, remainingHero = 0, allDraft = 0, allHero = 0;
        int clipsOnDisk = 0, clipsMissing = 0, clipsTotal = 0;
        double secOnDisk = 0, secMissing = 0;
        var rows = new List<CostSceneRow>();

        foreach (var scene in blueprintClips)
        {
            var sn = scene.SceneNumber;
            var diskMap = onDisk.GetValueOrDefault(sn) ?? new Dictionary<int, bool>();
            heroes.TryGetValue(sn, out var heroResForScene);
            var isHero = !string.IsNullOrEmpty(heroResForScene);

            double sSpent = 0, sMiss = 0, sHero = 0, sAllD = 0, sAllH = 0;
            double dOn = 0, dMiss = 0;
            int nDisk = 0, nMiss = 0, nAll = 0;

            foreach (var clip in scene.Clips)
            {
                nAll++;
                var on = diskMap.GetValueOrDefault(clip.ClipNumber);
                if (on) nDisk++;
                else nMiss++;

                var spentRes = isHero ? (heroResForScene ?? heroRes) : draftRes;
                var spentEst = EstimateClip(clip, spentRes, rates, retries);
                var missEst = EstimateClip(clip, draftRes, draftRates, retries);
                var heroEst = EstimateClip(clip, heroRes, heroRates, retries);
                var allD = EstimateClip(clip, draftRes, draftRates, retries);
                var allH = EstimateClip(clip, heroRes, heroRates, retries);

                sAllD += allD.Usd;
                sAllH += allH.Usd;
                if (on)
                {
                    sSpent += spentEst.Usd;
                    dOn += spentEst.DurationSec;
                    if (!isHero)
                        sHero += heroEst.Usd;
                }
                else
                {
                    sMiss += missEst.Usd;
                    dMiss += missEst.DurationSec;
                }
            }

            spent += sSpent;
            remainingDraft += sMiss;
            remainingHero += sHero;
            allDraft += sAllD;
            allHero += sAllH;
            clipsOnDisk += nDisk;
            clipsMissing += nMiss;
            clipsTotal += nAll;
            secOnDisk += dOn;
            secMissing += dMiss;

            actual.ByScene.TryGetValue(sn.ToString(CultureInfo.InvariantCulture), out var actualScene);

            rows.Add(new CostSceneRow
            {
                Scene = sn,
                Setting = scene.Setting.Length > 60 ? scene.Setting[..60] : scene.Setting,
                ClipsTotal = nAll,
                ClipsOnDisk = nDisk,
                ClipsMissing = nMiss,
                IsHero = isHero,
                HeroResolution = heroResForScene,
                CharactersOnScreen = scene.CharactersOnScreen,
                LocationIds = scene.LocationIds,
                PrimaryLocationId = scene.PrimaryLocationId,
                SpentUsd = Math.Round(sSpent, 2),
                ActualUsd = Math.Round(actualScene, 2),
                RemainingDraftUsd = Math.Round(sMiss, 2),
                HeroUpgradeUsd = Math.Round(sHero, 2),
                AllDraftUsd = Math.Round(sAllD, 2),
                AllHeroUsd = Math.Round(sAllH, 2),
                DurationOnDiskSec = Math.Round(dOn, 1),
                DurationMissingSec = Math.Round(dMiss, 1),
            });
        }

        rows.Sort((a, b) => a.Scene.CompareTo(b.Scene));

        var scenarios = BuildScenarios(blueprintClips, onDisk, cfg, rates, retries, draftRes, heroRes);

        // Non-video scope (model-dependent): cast portraits, optional voice, music, planning.
        var videoModel = GetStr(cfg, "model_name", "grok-imagine-video");
        var imageModel = GetStr(cfg, "image_model_name", "grok-imagine-image-quality");
        var planningModel = GetStr(cfg, "planning_model_name",
            GetStr(cfg, "chat_model_name", "grok-4.5"));
        var voiceModel = GetStr(cfg, "voice_model_name", "eleven_multilingual_v2");
        var audioModel = GetStr(cfg, "audio_model_name", "fal-ai/musicgen");

        var castPlan = EstimateCharacterGeneration(projectId, rates, cfg);
        var voicePlan = EstimateVoiceGeneration(projectId, rates, cfg);
        var musicPlan = EstimateMusicGeneration(blueprintClips, rates, cfg);
        var planningPlan = EstimatePlanningWork(blueprintClips, estimateBasis, rates, cfg);
        var reviewPlan = EstimateAutomatedReview(
            clipsTotal, rates, cfg, qaRetryOnFail, qaVideoMultiplier);

        var catalogVideoDraft = allDraft;
        var catalogVideoHero = allHero;
        var estimateByCategory = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [CostCategories.Screenplay] = Math.Round(planningPlan.Usd, 2),
            [CostCategories.Characters] = Math.Round(castPlan.Usd, 2),
            [CostCategories.Video] = Math.Round(allDraft, 2),
            [CostCategories.Voice] = Math.Round(voicePlan.Usd, 2),
            [CostCategories.Music] = Math.Round(musicPlan.Usd, 2),
            [CostCategories.Review] = Math.Round(reviewPlan.Usd, 2),
            [CostCategories.Other] = 0,
        };

        // Blend unit costs with portfolio averages when sample sizes allow.
        ApplyHistoryUnitCosts(estimateByCategory, refinement, clipsTotal);

        allDraft = estimateByCategory.GetValueOrDefault(CostCategories.Video);
        var nonVideo =
            estimateByCategory.GetValueOrDefault(CostCategories.Screenplay) +
            estimateByCategory.GetValueOrDefault(CostCategories.Characters) +
            estimateByCategory.GetValueOrDefault(CostCategories.Voice) +
            estimateByCategory.GetValueOrDefault(CostCategories.Music) +
            estimateByCategory.GetValueOrDefault(CostCategories.Review) +
            estimateByCategory.GetValueOrDefault(CostCategories.Other);
        var fullDraft = allDraft + nonVideo;
        var videoScale = catalogVideoDraft > 0.01 ? allDraft / catalogVideoDraft : 1.0;
        var fullHero = catalogVideoHero * videoScale + nonVideo;
        // Remaining first-pass: missing video + unfinished cast/voice/music (planning mostly already spent).
        var remainingExtras = castPlan.RemainingUsd + voicePlan.RemainingUsd + musicPlan.RemainingUsd
            + reviewPlan.RemainingUsd;
        remainingDraft += remainingExtras;

        var basisNote = estimateBasis switch
        {
            "shot_plan" => "Clip count from the shot plan.",
            "screenplay" => "Clip count estimated from screenplay scene lengths (before shot plan).",
            _ => "Import a book to unlock a film estimate.",
        };

        return new CostReport
        {
            ProjectId = projectId,
            DraftResolution = draftRes,
            HeroResolution = heroRes,
            ModelName = videoModel,
            VideoProvider = GetStr(cfg, "video_provider", "grok"),
            ImageModelName = imageModel,
            PlanningModelName = planningModel,
            VoiceModelName = voicePlan.Included ? voiceModel : null,
            EstimateBasis = estimateBasis,
            VoiceIncludedInEstimate = voicePlan.Included,
            OutputRateDraft = OutputRate(draftRes, draftRates),
            OutputRateHero = OutputRate(heroRes, heroRates),
            AssumeAvgRetries = retries,
            Summary = new CostReportSummary
            {
                ClipsTotal = clipsTotal,
                ClipsOnDisk = clipsOnDisk,
                ClipsMissing = clipsMissing,
                SecOnDisk = Math.Round(secOnDisk, 1),
                SecMissing = Math.Round(secMissing, 1),
                SpentUsd = Math.Round(spent, 2),
                ActualUsd = actual.ActualUsd,
                ActualEvents = actual.EventCount,
                ActualVideoJobs = actual.VideoJobs,
                ActualVideoSec = actual.VideoSec,
                RemainingFirstPassUsd = Math.Round(remainingDraft, 2),
                RemainingHeroUpgradeUsd = Math.Round(remainingHero, 2),
                FinishDraftUsd = Math.Round(spent + remainingDraft, 2),
                FinishDraftPlusHeroUsd = Math.Round(spent + remainingDraft + remainingHero, 2),
                FinishFromActualUsd = Math.Round(actual.ActualUsd + remainingDraft, 2),
                FullFilmAllDraftUsd = Math.Round(fullDraft, 2),
                FullFilmAllHeroUsd = Math.Round(fullHero, 2),
                ScenesWithMedia = rows.Count(r => r.ClipsOnDisk > 0),
                ScenesHero = rows.Count(r => r.IsHero),
                ScenesTotal = rows.Count,
            },
            Actual = actual,
            EstimateByCategory = estimateByCategory,
            Refinement = refinement,
            Scenes = rows,
            Scenarios = scenarios,
            RecentEvents = ledger
                .OrderByDescending(e => e.Ts ?? "")
                .Take(Math.Clamp(recentLimit, 1, 200))
                .ToList(),
            Notes =
                basisNote + " " +
                $"Rates from selected models (video={videoModel}, image={imageModel}" +
                (voicePlan.Included ? $", voice={voiceModel}" : "") + "). " +
                (qaRetryOnFail
                    ? $"Quality gate retry ON (admin auto-regen; video ×{qaVideoMultiplier:0.##}). "
                    : "Quality gate retry OFF. ") +
                (string.IsNullOrWhiteSpace(refinement.Notes) ? "" : refinement.Notes + " ") +
                "Actual = tracked spend at list rates when work completed.",
        };
    }

    public async Task<CostBackfillResult> BackfillFromDiskAsync(
        string projectId,
        bool onlyMissing = true,
        CancellationToken ct = default)
    {
        var cfg = await LoadConfigMapAsync(projectId, ct).ConfigureAwait(false);
        var defaultRates = RatesFromConfig(cfg);
        var ledger = await GetCostLedgerRawAsync(projectId, ct).ConfigureAwait(false);
        var seen = new HashSet<(int, int)>();
        foreach (var e in ledger)
        {
            if (!string.Equals(GetRawKind(e), "video", StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryGetInt(e, "scene", out var sn) && TryGetInt(e, "clip", out var cn))
                seen.Add((sn, cn));
        }

        var blueprint = await LoadBlueprintClipsAsync(projectId, ct).ConfigureAwait(false);
        var onDisk = IndexOnDiskClips(projectId);
        var clipJobs = await LoadClipJobsAsync(projectId, ct).ConfigureAwait(false);
        var defaultRes = GetStr(cfg, "resolution", "480p");
        var defaultModel = GetStr(cfg, "model_name", "grok-imagine-video");
        var imageModel = GetStr(cfg, "image_model_name", "grok-imagine-image-quality");
        var defaultDur = GetDouble(cfg, "duration_seconds", 8);
        var assumeRef = GetBool(defaultRates, "assume_ref_image_per_clip", true);

        var added = 0;
        var skipped = 0;
        foreach (var scene in blueprint)
        {
            var diskMap = onDisk.GetValueOrDefault(scene.SceneNumber) ?? new Dictionary<int, bool>();
            foreach (var clip in scene.Clips)
            {
                if (!diskMap.GetValueOrDefault(clip.ClipNumber))
                {
                    skipped++;
                    continue;
                }

                if (onlyMissing && seen.Contains((scene.SceneNumber, clip.ClipNumber)))
                {
                    skipped++;
                    continue;
                }

                clipJobs.TryGetValue($"{scene.SceneNumber}_{clip.ClipNumber}", out var job);
                var duration = clip.DurationSec > 0 ? clip.DurationSec : defaultDur;
                if (job is not null && job.TryGetValue("duration_sec", out var ds) &&
                    ds.TryGetDouble(out var jdur) && jdur > 0)
                    duration = jdur;

                var res = defaultRes;
                if (job is not null && job.TryGetValue("resolution", out var jr) &&
                    jr.ValueKind == JsonValueKind.String && jr.GetString() is { Length: > 0 } rs)
                    res = rs;

                var model = defaultModel;
                if (job is not null && job.TryGetValue("model", out var jm) &&
                    jm.ValueKind == JsonValueKind.String && jm.GetString() is { Length: > 0 } md)
                    model = md;

                var isExtend = string.Equals(
                    clip.Continuation, "extend_previous", StringComparison.OrdinalIgnoreCase);
                var rates = RatesFromModels(model, imageModel, cfg);
                var priced = PriceVideo(duration, res, rates, assumeRef, isExtend, attempts: 1);

                var evt = new Dictionary<string, object?>
                {
                    ["kind"] = "video",
            ["category"] = CostCategories.Video,
                    ["scene"] = scene.SceneNumber,
                    ["clip"] = clip.ClipNumber,
                    ["model"] = model,
                    ["provider"] = rates.TryGetValue("video_provider", out var vp) ? vp : null,
                    ["request_id"] = job is not null && job.TryGetValue("request_id", out var rid)
                        ? rid.GetString() ?? ""
                        : "",
                    ["has_ref_image"] = assumeRef,
                    ["is_extend"] = isExtend,
                    ["source"] = "backfill",
                    ["duration_sec"] = priced.DurationSec,
                    ["attempts"] = 1.0,
                    ["resolution"] = res,
                    ["output_rate_per_sec"] = priced.RatePerSec,
                    ["video_output_usd"] = priced.VideoOut,
                    ["ref_image_usd"] = priced.RefImg,
                    ["extend_input_usd"] = priced.ExtendIn,
                    ["usd"] = priced.Usd,
                    ["currency"] = "USD",
                    ["extra"] = new Dictionary<string, object?> { ["backfill"] = true },
                };
                await AppendCostEventAsync(projectId, evt, save: true, ct).ConfigureAwait(false);
                seen.Add((scene.SceneNumber, clip.ClipNumber));
                added++;
            }
        }

        var summary = SummarizeLedger(await GetCostLedgerAsync(projectId, ct).ConfigureAwait(false));
        return new CostBackfillResult
        {
            Added = added,
            Skipped = skipped,
            LedgerEvents = summary.EventCount,
            ActualUsd = summary.ActualUsd,
        };
    }

    /// <param name="durationSec">
    /// Billed length — prefer probed final file seconds after silence trim; fall back to API request duration.
    /// </param>
    /// <param name="requestedDurationSec">
    /// Optional API-requested duration when <paramref name="durationSec"/> is measured (for audit).
    /// </param>
    public async Task RecordVideoGenerationAsync(
        string projectId,
        int scene,
        int clip,
        double durationSec,
        string resolution,
        string model,
        bool hasRefImage = false,
        bool isExtend = false,
        string? requestId = null,
        double? requestedDurationSec = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var cfg = await LoadConfigMapAsync(projectId, ct).ConfigureAwait(false);
        // Price this event with the model that actually ran (vendor catalog), not a stale config table.
        var rates = RatesFromModels(
            videoModelId: model,
            imageModelId: GetStr(cfg, "image_model_name", "grok-imagine-image-quality"),
            cfgOverrides: cfg);
        var priced = PriceVideo(durationSec, resolution, rates, hasRefImage, isExtend, 1);
        var evt = new Dictionary<string, object?>
        {
            ["kind"] = "video",
            ["category"] = CostCategories.Video,
            ["scene"] = scene,
            ["clip"] = clip,
            ["model"] = model,
            ["provider"] = rates.TryGetValue("video_provider", out var vp) ? vp : null,
            ["request_id"] = requestId ?? "",
            ["has_ref_image"] = hasRefImage,
            ["is_extend"] = isExtend,
            ["source"] = "list_rate",
            // Primary duration used for pricing (probed when available)
            ["duration_sec"] = priced.DurationSec,
            ["attempts"] = 1.0,
            ["resolution"] = resolution,
            ["output_rate_per_sec"] = priced.RatePerSec,
            ["video_output_usd"] = priced.VideoOut,
            ["ref_image_usd"] = priced.RefImg,
            ["extend_input_usd"] = priced.ExtendIn,
            ["usd"] = priced.Usd,
            ["currency"] = "USD",
            ["user_id"] = userId ?? "",
        };
        if (requestedDurationSec is > 0 &&
            Math.Abs(requestedDurationSec.Value - priced.DurationSec) >= 0.05)
        {
            evt["request_duration_sec"] = Math.Round(requestedDurationSec.Value, 3);
            evt["duration_source"] = "probed";
        }
        else
        {
            evt["duration_source"] = requestedDurationSec is > 0 ? "request" : "probed_or_request";
        }

        await AppendCostEventAsync(projectId, evt, save: true, ct).ConfigureAwait(false);

        if (_credits is not null && priced.Usd > 0)
        {
            await _credits.TryDebitUsageAsync(
                userId,
                priced.Usd,
                projectId,
                metaKind: "video",
                note: $"S{scene:D2}C{clip} {model} {priced.DurationSec:F1}s",
                ct: ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<CostEvent>> GetCostLedgerAsync(
        string projectId,
        CancellationToken ct = default)
    {
        var raw = await GetCostLedgerRawAsync(projectId, ct).ConfigureAwait(false);
        var list = new List<CostEvent>();
        foreach (var e in raw)
            list.Add(ParseEvent(e));
        return list;
    }

    public async Task RecordImageGenerationAsync(
        string projectId,
        int nImages,
        string model,
        bool quality = true,
        string? character = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var n = Math.Max(0, nImages);
        var entry = SupportedModelCatalog.Find(model, ModelCapability.Image)
                    ?? SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Image);
        var unit = entry.ImageCostPerImage
                   ?? (quality ? 0.05 : 0.02);
        var usd = Math.Round(unit * n, 4);
        await AppendCostEventAsync(projectId, new Dictionary<string, object?>
        {
            ["kind"] = "image",
            ["category"] = CostCategories.Characters,
            ["model"] = model,
            ["character"] = character ?? "",
            ["n_images"] = n,
            ["unit_usd"] = unit,
            ["usd"] = usd,
            ["currency"] = "USD",
            ["source"] = "list_rate",
            ["provider"] = entry.ProviderId,
            ["user_id"] = userId ?? "",
        }, save: true, ct).ConfigureAwait(false);

        if (_credits is not null && usd > 0)
        {
            await _credits.TryDebitUsageAsync(
                userId,
                usd,
                projectId,
                metaKind: "image",
                note: string.IsNullOrWhiteSpace(character)
                    ? $"{n}× {model}"
                    : $"{n}× {model} ({character})",
                ct: ct).ConfigureAwait(false);
        }
    }

    // ---- internals ----

    private List<CostScenarioRow> BuildScenarios(
        List<BlueprintSceneClips> scenes,
        Dictionary<int, Dictionary<int, bool>> onDisk,
        Dictionary<string, JsonElement> cfg,
        Dictionary<string, object?> baseRates,
        double retries,
        string draftRes,
        string heroRes)
    {
        var model = GetStr(cfg, "model_name", "grok-imagine-video");
        var rows = new List<CostScenarioRow>();
        foreach (var res in new[] { "480p", "720p", "1080p" })
        {
            var rates = RatesFromConfig(CloneCfg(cfg, res, retries));
            double full = 0, missing = 0, regen = 0;
            foreach (var scene in scenes)
            {
                var disk = onDisk.GetValueOrDefault(scene.SceneNumber) ?? new Dictionary<int, bool>();
                foreach (var clip in scene.Clips)
                {
                    var est = EstimateClip(clip, res, rates, retries);
                    full += est.Usd;
                    if (disk.GetValueOrDefault(clip.ClipNumber))
                        regen += est.Usd;
                    else
                        missing += est.Usd;
                }
            }

            rows.Add(new CostScenarioRow
            {
                Label = $"{model} @ {res}",
                Resolution = res,
                ModelName = model,
                RatePerSec = OutputRate(res, rates),
                FullFilmUsd = Math.Round(full, 2),
                RemainingMissingUsd = Math.Round(missing, 2),
                RegenOnDiskUsd = Math.Round(regen, 2),
                AssumeAvgRetries = retries,
            });
        }

        // highlight draft/hero even if already listed
        _ = draftRes;
        _ = heroRes;
        return rows;
    }

    private static (double Usd, double DurationSec) EstimateClip(
        BlueprintClip clip,
        string resolution,
        Dictionary<string, object?> rates,
        double retries)
    {
        var duration = clip.DurationSec > 0 ? clip.DurationSec : 8;
        var attempts = 1.0 + Math.Max(0, retries);
        var outRate = OutputRate(resolution, rates);
        var videoOut = duration * outRate * attempts;
        var refImg = 0.0;
        if (GetBool(rates, "assume_ref_image_per_clip", true))
            refImg = GetDouble(rates, "video_input_image", FallbackVideoRefImageCost) * attempts;
        var extend = 0.0;
        if (string.Equals(clip.Continuation, "extend_previous", StringComparison.OrdinalIgnoreCase))
            extend = duration * GetDouble(rates, "video_input_per_sec", FallbackVideoExtendCostPerSec) * attempts;
        return (videoOut + refImg + extend, duration);
    }

    internal static (double Usd, double DurationSec, double RatePerSec, double VideoOut, double RefImg, double ExtendIn)
        PriceVideo(
            double durationSec,
            string resolution,
            Dictionary<string, object?> rates,
            bool hasRef,
            bool isExtend,
            double attempts)
    {
        var duration = Math.Max(0, durationSec);
        attempts = Math.Max(1, attempts);
        var outRate = OutputRate(resolution, rates);
        var videoOut = duration * outRate * attempts;
        var refImg = hasRef ? GetDouble(rates, "video_input_image", FallbackVideoRefImageCost) * attempts : 0;
        var extend = isExtend
            ? duration * GetDouble(rates, "video_input_per_sec", FallbackVideoExtendCostPerSec) * attempts
            : 0;
        var usd = Math.Round(videoOut + refImg + extend, 4);
        return (usd, duration, outRate, Math.Round(videoOut, 4), Math.Round(refImg, 4), Math.Round(extend, 4));
    }

    private static CostLedgerSummary SummarizeLedger(IReadOnlyList<CostEvent> events)
    {
        double total = 0, videoSec = 0;
        var byKind = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var byScene = new Dictionary<string, double>(StringComparer.Ordinal);
        var byModel = new Dictionary<string, double>(StringComparer.Ordinal);
        var videoJobs = 0;
        var imageJobs = 0;
        foreach (var e in events)
        {
            total += e.Usd;
            var kind = string.IsNullOrEmpty(e.Kind) ? "other" : e.Kind;
            byKind[kind] = byKind.GetValueOrDefault(kind) + e.Usd;
            if (e.Scene is int sn)
            {
                var key = sn.ToString(CultureInfo.InvariantCulture);
                byScene[key] = byScene.GetValueOrDefault(key) + e.Usd;
            }
            if (!string.IsNullOrEmpty(e.Model))
                byModel[e.Model] = byModel.GetValueOrDefault(e.Model) + e.Usd;
            if (string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase))
            {
                videoJobs++;
                videoSec += e.DurationSec ?? 0;
            }
            else if (string.Equals(kind, "image", StringComparison.OrdinalIgnoreCase))
            {
                imageJobs++;
            }
        }

        return new CostLedgerSummary
        {
            ActualUsd = Math.Round(total, 2),
            EventCount = events.Count,
            VideoJobs = videoJobs,
            ImageJobs = imageJobs,
            VideoSec = Math.Round(videoSec, 1),
            ByKind = byKind.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2)),
            ByScene = byScene.OrderBy(kv => int.TryParse(kv.Key, out var n) ? n : 0)
                .ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2)),
            ByModel = byModel.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2)),
        };
    }

    private static CostEvent ParseEvent(JsonElement e)
    {
        return new CostEvent
        {
            Id = e.TryGetProperty("id", out var id) ? id.GetString() : null,
            Ts = e.TryGetProperty("ts", out var ts) ? ts.GetString() : null,
            Kind = e.TryGetProperty("kind", out var k) ? k.GetString() ?? "other" : "other",
            Category = CostCategories.Resolve(
                e.TryGetProperty("kind", out var k2) ? k2.GetString() : null,
                e.TryGetProperty("mode", out var mo) ? mo.GetString() : null,
                e.TryGetProperty("category", out var cat) ? cat.GetString() : null),
            Scene = TryGetInt(e, "scene", out var sn) ? sn : null,
            Clip = TryGetInt(e, "clip", out var cn) ? cn : null,
            Model = e.TryGetProperty("model", out var m) ? m.GetString() : null,
            Resolution = e.TryGetProperty("resolution", out var r) ? r.GetString() : null,
            DurationSec = TryGetDouble(e, "duration_sec", out var d) ? d : null,
            Usd = TryGetDouble(e, "usd", out var u) ? u : 0,
            Currency = e.TryGetProperty("currency", out var c) ? c.GetString() ?? "USD" : "USD",
            Source = e.TryGetProperty("source", out var s) ? s.GetString() : null,
            Character = e.TryGetProperty("character", out var ch) ? ch.GetString() : null,
            OutputRatePerSec = TryGetDouble(e, "output_rate_per_sec", out var or) ? or : null,
            HasRefImage = e.TryGetProperty("has_ref_image", out var hr) &&
                          (hr.ValueKind is JsonValueKind.True or JsonValueKind.False)
                ? hr.GetBoolean()
                : null,
            IsExtend = e.TryGetProperty("is_extend", out var ie) &&
                       (ie.ValueKind is JsonValueKind.True or JsonValueKind.False)
                ? ie.GetBoolean()
                : null,
        };
    }

    private Task<List<JsonElement>> GetCostLedgerRawAsync(
        string projectId,
        CancellationToken ct = default) =>
        GetCostLedgerRawCoreAsync(projectId, ct);

    private async Task<List<JsonElement>> GetCostLedgerRawCoreAsync(
        string projectId,
        CancellationToken ct)
    {
        var path = await StatePathAsync(projectId, ct).ConfigureAwait(false);
        if (!File.Exists(path))
            return new List<JsonElement>();
        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("cost_ledger", out var ledger) ||
                ledger.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();
            return ledger.EnumerateArray().Select(x => x.Clone()).ToList();
        }
        catch
        {
            return new List<JsonElement>();
        }
    }

    private async Task AppendCostEventAsync(
        string projectId,
        Dictionary<string, object?> evt,
        bool save,
        CancellationToken ct = default)
    {
        var path = await StatePathAsync(projectId, ct).ConfigureAwait(false);

        JsonDocument rawDoc;
        if (File.Exists(path))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                rawDoc = JsonDocument.Parse(bytes);
            }
            catch
            {
                rawDoc = JsonDocument.Parse("{}");
            }
        }
        else
        {
            rawDoc = JsonDocument.Parse("{}");
        }

        using (rawDoc)
        {
            var ledgerList = new List<object?>();
            if (rawDoc.RootElement.TryGetProperty("cost_ledger", out var existing) &&
                existing.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in existing.EnumerateArray())
                    ledgerList.Add(item.Deserialize<object>());
            }

            var ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            evt.TryAdd("id", $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{ledgerList.Count:D4}");
            evt.TryAdd("ts", ts);
            evt.TryAdd("currency", "USD");
            ledgerList.Add(evt);
            if (ledgerList.Count > 20000)
                ledgerList = ledgerList.TakeLast(20000).ToList();

            var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in rawDoc.RootElement.EnumerateObject())
            {
                if (p.Name is "cost_ledger" or "cost_totals")
                    continue;
                merged[p.Name] = p.Value.Deserialize<object>();
            }

            merged["cost_ledger"] = ledgerList;
            var prevUsd = 0.0;
            var prevEvents = 0;
            if (rawDoc.RootElement.TryGetProperty("cost_totals", out var tot) &&
                tot.ValueKind == JsonValueKind.Object)
            {
                if (tot.TryGetProperty("usd", out var u) && u.TryGetDouble(out var ud))
                    prevUsd = ud;
                if (tot.TryGetProperty("events", out var ev) && ev.TryGetInt32(out var en))
                    prevEvents = en;
            }

            var addUsd = 0.0;
            if (evt.TryGetValue("usd", out var usdObj) && usdObj is not null)
                addUsd = Convert.ToDouble(usdObj, CultureInfo.InvariantCulture);

            merged["cost_totals"] = new Dictionary<string, object?>
            {
                ["usd"] = Math.Round(prevUsd + addUsd, 4),
                ["events"] = prevEvents + 1,
                ["updated_at"] = ts,
            };

            if (save)
            {
                var json = JsonSerializer.Serialize(merged, JsonDefaults.Indented);
                await File.WriteAllTextAsync(path, json + "\n", ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> StatePathAsync(string projectId, CancellationToken ct)
    {
        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        var meta = Path.Combine(dir, "project.json");
        var name = "pipeline_state.json";
        if (File.Exists(meta))
        {
            try
            {
                await using var stream = File.OpenRead(meta);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                    .ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("state_file", out var sf) &&
                    sf.GetString() is { Length: > 0 } n)
                    name = n;
            }
            catch { /* ignore */ }
        }
        return Path.Combine(dir, name);
    }

    private async Task<Dictionary<string, JsonElement>> LoadConfigMapAsync(
        string projectId,
        CancellationToken ct)
    {
        var cfg = await _projects.GetConfigAsync(projectId, ct).ConfigureAwait(false);
        return new Dictionary<string, JsonElement>(cfg, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, JsonElement> CloneCfg(
        Dictionary<string, JsonElement> cfg,
        string resolution,
        double retries)
    {
        // We don't mutate JsonElements; rates use resolution + retries passed separately.
        _ = cfg;
        _ = resolution;
        _ = retries;
        return cfg;
    }

    private async Task<List<BlueprintSceneClips>> LoadBlueprintClipsAsync(
        string projectId,
        CancellationToken ct)
    {
        var list = new List<BlueprintSceneClips>();
        using var bp = await _projects.LoadBlueprintAsync(projectId, ct).ConfigureAwait(false);
        if (bp is null ||
            !bp.RootElement.TryGetProperty("scenes", out var scenes) ||
            scenes.ValueKind != JsonValueKind.Array)
            return list;

        var defaultDur = 8.0;
        var cfg = await LoadConfigMapAsync(projectId, ct).ConfigureAwait(false);
        defaultDur = GetDouble(cfg, "duration_seconds", 8);

        foreach (var s in scenes.EnumerateArray())
        {
            var sn = s.TryGetProperty("scene_number", out var sne) && sne.TryGetInt32(out var n) ? n : 0;
            var setting = s.TryGetProperty("setting", out var set) ? set.GetString() ?? "" : "";
            var clips = new List<BlueprintClip>();
            if (s.TryGetProperty("veo_clips", out var vc) && vc.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in vc.EnumerateArray())
                {
                    var cn = c.TryGetProperty("clip_number", out var cne) && cne.TryGetInt32(out var cv) ? cv : 0;
                    if (cn <= 0) continue;
                    var dur = defaultDur;
                    if (c.TryGetProperty("duration_seconds", out var d) && d.TryGetDouble(out var dd) && dd > 0)
                        dur = dd;
                    else if (c.TryGetProperty("duration_seconds", out var di) && di.TryGetInt32(out var idi) && idi > 0)
                        dur = idi;
                    else if (c.TryGetProperty("timestamp", out var ts) && ts.GetString() is { } tss)
                    {
                        var m = TimestampDur.Match(tss);
                        if (m.Success)
                        {
                            var inv = System.Globalization.CultureInfo.InvariantCulture;
                            var a = int.Parse(m.Groups[1].Value, inv) * 60 + int.Parse(m.Groups[2].Value, inv);
                            var b = int.Parse(m.Groups[3].Value, inv) * 60 + int.Parse(m.Groups[4].Value, inv);
                            if (b > a) dur = b - a;
                        }
                    }

                    clips.Add(new BlueprintClip
                    {
                        ClipNumber = cn,
                        DurationSec = dur,
                        Continuation = c.TryGetProperty("veo_continuation_source", out var cont)
                            ? cont.GetString() ?? "none"
                            : "none",
                    });
                }
            }

            var chars = new List<string>();
            if (s.TryGetProperty("characters_on_screen", out var cos) && cos.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in cos.EnumerateArray())
                {
                    var name = x.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        chars.Add(name!);
                }
            }

            var locs = new List<string>();
            if (s.TryGetProperty("location_ids", out var lids) && lids.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in lids.EnumerateArray())
                {
                    var name = x.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        locs.Add(name!);
                }
            }

            string? primaryLoc = null;
            if (s.TryGetProperty("primary_location_id", out var pl) &&
                pl.GetString() is { Length: > 0 } plId)
            {
                primaryLoc = plId;
                if (!locs.Contains(plId, StringComparer.OrdinalIgnoreCase))
                    locs.Insert(0, plId);
            }

            list.Add(new BlueprintSceneClips
            {
                SceneNumber = sn,
                Setting = setting,
                Clips = clips,
                CharactersOnScreen = chars,
                LocationIds = locs,
                PrimaryLocationId = primaryLoc,
            });
        }

        return list.OrderBy(x => x.SceneNumber).ToList();
    }

    private Dictionary<int, Dictionary<int, bool>> IndexOnDiskClips(string projectId)
    {
        var map = new Dictionary<int, Dictionary<int, bool>>();
        var videoDir = Path.Combine(_projects.GetProjectDir(projectId), "assets", "video");
        if (!Directory.Exists(videoDir))
            return map;
        try
        {
            // DirectoryInfo avoids a second FileInfo stat per path for Length.
            foreach (var fi in new DirectoryInfo(videoDir).EnumerateFiles("scene_*_clip_*.mp4"))
            {
                var name = fi.Name;
                // Exact scene_01_clip_02.mp4 only (not .native.mp4 sidecars)
                if (!ClipFileNaming.IsExactClipFileName(name)) continue;
                var stem = Path.GetFileNameWithoutExtension(name);
                var parts = stem.Split('_');
                if (parts.Length >= 4 &&
                    int.TryParse(parts[1], out var sn) &&
                    int.TryParse(parts[3], out var cn))
                {
                    try
                    {
                        if (fi.Length < 1024) continue;
                    }
                    catch { continue; }

                    if (!map.TryGetValue(sn, out var inner))
                    {
                        inner = new Dictionary<int, bool>();
                        map[sn] = inner;
                    }
                    inner[cn] = true;
                }
            }
        }
        catch { /* ignore */ }
        return map;
    }

    private async Task<Dictionary<int, string>> LoadHeroMapAsync(
        string projectId,
        CancellationToken ct)
    {
        var map = new Dictionary<int, string>();
        var path = await StatePathAsync(projectId, ct).ConfigureAwait(false);
        if (!File.Exists(path)) return map;
        try
        {
            var doc = await _projects.ReadCache.GetOrLoadJsonDocumentAsync(path, ct).ConfigureAwait(false);
            if (doc is null || !doc.RootElement.TryGetProperty("scene_hero", out var hero) ||
                hero.ValueKind != JsonValueKind.Object)
                return map;
            foreach (var p in hero.EnumerateObject())
            {
                if (!int.TryParse(p.Name, out var sn)) continue;
                if (p.Value.ValueKind == JsonValueKind.Object &&
                    p.Value.TryGetProperty("resolution", out var r) &&
                    r.GetString() is { Length: > 0 } res)
                    map[sn] = res;
                else if (p.Value.ValueKind is JsonValueKind.True)
                    map[sn] = "720p";
            }
        }
        catch { /* ignore */ }
        return map;
    }

    private async Task<Dictionary<string, Dictionary<string, JsonElement>>> LoadClipJobsAsync(
        string projectId,
        CancellationToken ct)
    {
        var map = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
        var path = await StatePathAsync(projectId, ct).ConfigureAwait(false);
        if (!File.Exists(path)) return map;
        try
        {
            var doc = await _projects.ReadCache.GetOrLoadJsonDocumentAsync(path, ct).ConfigureAwait(false);
            if (doc is null || !doc.RootElement.TryGetProperty("clip_jobs", out var jobs) ||
                jobs.ValueKind != JsonValueKind.Object)
                return map;
            foreach (var p in jobs.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Object) continue;
                var inner = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var q in p.Value.EnumerateObject())
                    inner[q.Name] = q.Value.Clone();
                map[p.Name] = inner;
            }
        }
        catch { /* ignore */ }
        return map;
    }

    /// <summary>
    /// Build planning/ledger rates from the project's selected models (vendor catalog),
    /// then apply non-rate planning assumptions from <c>cost_estimates</c> (retries, etc.).
    /// Manual $/sec overrides in config are ignored so estimates stay vendor-accurate.
    /// </summary>
    private static Dictionary<string, object?> RatesFromConfig(Dictionary<string, JsonElement> cfg)
    {
        var videoModelId = GetStr(cfg, "model_name", "grok-imagine-video");
        var imageModelId = GetStr(cfg, "image_model_name", "grok-imagine-image-quality");
        return RatesFromModels(videoModelId, imageModelId, cfg);
    }

    /// <summary>
    /// Vendor list rates for a video + image model pair from <see cref="SupportedModelCatalog"/>.
    /// </summary>
    internal static Dictionary<string, object?> RatesFromModels(
        string? videoModelId,
        string? imageModelId,
        Dictionary<string, JsonElement>? cfgOverrides = null)
    {
        var video = SupportedModelCatalog.ResolveOrDefault(
            videoModelId, ModelCapability.Video, fallbackId: "grok-imagine-video");
        var imagePrimary = SupportedModelCatalog.Find(imageModelId, ModelCapability.Image)
            ?? SupportedModelCatalog.ResolveOrDefault(
                imageModelId, ModelCapability.Image, fallbackId: "grok-imagine-image-quality");
        // Prefer a cheaper "standard" sibling in the same family when the project uses a quality image model.
        var imageStandard = SupportedModelCatalog.ForCapability(ModelCapability.Image)
            .Where(e => e.Provider == imagePrimary.Provider
                        && e.ImageCostPerImage is not null
                        && !string.Equals(e.Id, imagePrimary.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.ImageCostPerImage)
            .FirstOrDefault()
            ?? imagePrimary;

        var videoTable = BuildVideoRateTable(video);
        var qualityUnit = imagePrimary.ImageCostPerImage ?? 0.05;
        var standardUnit = imageStandard.ImageCostPerImage ?? imagePrimary.ImageCostPerImage ?? 0.02;

        // Reference-image and extend-per-second add-ons: prefer a real per-model catalog value
        // (published by the vendor) and only fall back to the small Grok-era estimate when the
        // catalog has no verified number for this model. As of 2026-08 no enabled video provider
        // publishes either as a distinct line item (checked against docs.x.ai/developers/pricing for
        // xAI, the only model with SupportsVideoContinue=true), so these fields are null everywhere
        // today and the fallback constants apply uniformly — but the catalog is checked first so a
        // future vendor-verified number takes over automatically.
        var refImageCostReal = video.VideoReferenceImageCost;
        var extendCostReal = video.VideoExtendCostPerSecond;
        var refImageSource = refImageCostReal is not null ? "model_catalog" : "estimated_fallback";
        var extendSource = extendCostReal is not null ? "model_catalog" : "estimated_fallback";

        // Overall video pricing is only "fully real" when the per-resolution output table, the
        // reference-image add-on, and (only if this model can extend) the extend add-on are all
        // sourced from the catalog rather than a fallback estimate.
        var videoOutputIsCatalog = video.VideoCostPerSecondByResolution is { Count: > 0 };
        var videoPricingFullyReal = videoOutputIsCatalog
            && refImageCostReal is not null
            && (!video.SupportsVideoContinue || extendCostReal is not null);

        var rates = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["currency"] = "USD",
            ["source"] = "model_catalog",
            ["video_model"] = video.Id,
            ["video_provider"] = video.ProviderId,
            ["image_model"] = imagePrimary.Id,
            ["image_provider"] = imagePrimary.ProviderId,
            ["video_output_per_sec"] = videoTable,
            ["video_input_image"] = refImageCostReal ?? FallbackVideoRefImageCost,
            ["video_input_image_source"] = refImageSource,
            ["video_input_per_sec"] = extendCostReal ?? FallbackVideoExtendCostPerSec,
            ["video_input_per_sec_source"] = extendSource,
            ["video_pricing_source"] = videoPricingFullyReal ? "model_catalog" : "estimated_fallback",
            ["image_output_quality"] = qualityUnit,
            ["image_output_standard"] = standardUnit,
            ["assume_ref_image_per_clip"] = true,
            ["assume_extend_fraction"] = 0.0,
            ["assume_avg_retries"] = 0.0,
        };

        // Planning knobs only — do not let old manual $/sec tables override vendor rates.
        if (cfgOverrides is not null &&
            cfgOverrides.TryGetValue("cost_estimates", out var ce) &&
            ce.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in ce.EnumerateObject())
            {
                if (p.NameEquals("video_output_per_sec") ||
                    p.NameEquals("image_output_quality") ||
                    p.NameEquals("image_output_standard") ||
                    p.NameEquals("source") ||
                    p.NameEquals("video_model") ||
                    p.NameEquals("video_provider") ||
                    p.NameEquals("image_model") ||
                    p.NameEquals("image_provider") ||
                    p.NameEquals("currency") ||
                    p.NameEquals("notes"))
                    continue;

                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDouble(out var d))
                    rates[p.Name] = d;
                else if (p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    rates[p.Name] = p.Value.GetBoolean();
                else if (p.Value.ValueKind == JsonValueKind.String)
                    rates[p.Name] = p.Value.GetString();
            }
        }

        return rates;
    }

    /// <summary>
    /// Per-resolution $/sec from the video catalog entry; fill missing 480p/720p/1080p from nearest vendor tier.
    /// </summary>
    internal static Dictionary<string, double> BuildVideoRateTable(SupportedModelEntry video)
    {
        var table = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (video.VideoCostPerSecondByResolution is { Count: > 0 } src)
        {
            foreach (var kv in src)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value >= 0)
                    table[kv.Key.Trim()] = kv.Value;
            }
        }

        FillMissingRes(table, "720p", "1080p", "480p");
        FillMissingRes(table, "480p", "720p", "1080p");
        FillMissingRes(table, "1080p", "720p", "480p");

        // Absolute last resort (Grok-era defaults) if catalog has no video pricing at all.
        if (table.Count == 0)
        {
            table["480p"] = 0.05;
            table["720p"] = 0.07;
            table["1080p"] = 0.25;
        }

        return table;
    }

    private static void FillMissingRes(
        Dictionary<string, double> table,
        string res,
        params string[] prefer)
    {
        if (table.ContainsKey(res)) return;
        foreach (var p in prefer)
        {
            if (table.TryGetValue(p, out var v))
            {
                table[res] = v;
                return;
            }
        }
        if (table.Count > 0)
            table[res] = table.Values.Min();
    }

    private static double OutputRate(string resolution, Dictionary<string, object?> rates)
    {
        var res = (resolution ?? "720p").ToLowerInvariant().Trim();
        if (rates.TryGetValue("video_output_per_sec", out var t) &&
            t is Dictionary<string, double> table)
        {
            if (table.TryGetValue(res, out var r)) return r;
            if (table.TryGetValue("720p", out var d)) return d;
            if (table.Count > 0) return table.Values.First();
        }
        return 0.07;
    }

    private static string GetStr(Dictionary<string, JsonElement> cfg, string key, string fallback) =>
        cfg.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? fallback
            : fallback;

    private static double GetDouble(Dictionary<string, JsonElement> cfg, string key, double fallback) =>
        cfg.TryGetValue(key, out var el) && el.TryGetDouble(out var v) ? v : fallback;

    private static double GetDouble(Dictionary<string, object?> rates, string key, double fallback)
    {
        if (!rates.TryGetValue(key, out var v) || v is null) return fallback;
        return Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    private static bool GetBool(Dictionary<string, object?> rates, string key, bool fallback)
    {
        if (!rates.TryGetValue(key, out var v) || v is null) return fallback;
        return v is bool b ? b : Convert.ToBoolean(v, CultureInfo.InvariantCulture);
    }

    private static string GetRawKind(JsonElement e) =>
        e.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";

    private static bool TryGetInt(JsonElement e, string name, out int v)
    {
        v = 0;
        if (!e.TryGetProperty(name, out var p)) return false;
        if (p.TryGetInt32(out v)) return true;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out v)) return true;
        return false;
    }

    private static bool TryGetDouble(JsonElement e, string name, out double v)
    {
        v = 0;
        if (!e.TryGetProperty(name, out var p)) return false;
        if (p.TryGetDouble(out v)) return true;
        if (p.ValueKind == JsonValueKind.String &&
            double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            return true;
        return false;
    }


    /// <summary>
    /// When no shot plan yet: invent clip slots from Fountain scene duration targets so
    /// post-import estimates work (model rates × planned seconds).
    /// </summary>
    private async Task<List<BlueprintSceneClips>> LoadScreenplayDerivedClipsAsync(
        string projectId,
        Dictionary<string, JsonElement> cfg,
        CancellationToken ct)
    {
        await Task.Yield();
        var list = new List<BlueprintSceneClips>();
        var model = ScreenplayService.TryBuildModelFromProject(_projects, projectId);
        if (model is null) return list;

        if (!model.TryGetValue("scenes", out var scenesObj) || scenesObj is not List<object?> scenes)
            return list;

        var defaultDur = GetDouble(cfg, "duration_seconds", 8);
        if (defaultDur < 2) defaultDur = 8;

        var sn = 0;
        foreach (var raw in scenes)
        {
            if (raw is not Dictionary<string, object?> s) continue;
            sn++;
            string setting = "";
            if (s.TryGetValue("setting", out var setObj) && setObj is not null)
                setting = setObj.ToString() ?? "";
            else if (s.TryGetValue("heading", out var hObj) && hObj is not null)
                setting = hObj.ToString() ?? "";
            var target = ToPositiveDouble(
                s.TryGetValue("duration_target_seconds", out var d1) ? d1 : null,
                ToPositiveDouble(
                    s.TryGetValue("estimated_duration_seconds", out var d2) ? d2 : null,
                    24));
            target = Math.Clamp(target, defaultDur, 600);

            var nClips = Math.Max(1, (int)Math.Ceiling(target / defaultDur));
            var clips = new List<BlueprintClip>();
            var remaining = target;
            for (var i = 1; i <= nClips; i++)
            {
                var dur = i == nClips ? Math.Max(1, remaining) : Math.Min(defaultDur, remaining);
                clips.Add(new BlueprintClip
                {
                    ClipNumber = i,
                    DurationSec = dur,
                    Continuation = i > 1 ? "prev" : "none",
                });
                remaining -= dur;
            }

            var chars = new List<string>();
            if (s.TryGetValue("characters_on_screen", out var cos) && cos is List<object?> cosList)
            {
                foreach (var x in cosList)
                {
                    var name = x?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        chars.Add(name!);
                }
            }

            list.Add(new BlueprintSceneClips
            {
                SceneNumber = s.TryGetValue("scene_number", out var snObj) && snObj is not null
                    && int.TryParse(snObj.ToString(), out var snParsed) ? snParsed : sn,
                Setting = setting ?? "",
                Clips = clips,
                CharactersOnScreen = chars,
            });
        }

        return list.OrderBy(x => x.SceneNumber).ToList();
    }

    private readonly record struct ScopeEstimate(double Usd, double RemainingUsd, bool Included);

    /// <summary>Character portraits: variants × image-model unit cost (catalog).</summary>
    private ScopeEstimate EstimateCharacterGeneration(
        string projectId,
        Dictionary<string, object?> rates,
        Dictionary<string, JsonElement> cfg)
    {
        var unit = GetDouble(rates, "image_output_quality", 0.05);
        var variants = 3;
        if (cfg.TryGetValue("cost_estimates", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            if (ce.TryGetProperty("character_variants", out var cv) && cv.TryGetInt32(out var n) && n > 0)
                variants = Math.Clamp(n, 1, 6);
        }

        var chars = _projects.ListCharacters(projectId);
        var onScreen = chars.Where(c => !c.VoiceOnly).ToList();
        if (onScreen.Count == 0)
            return new ScopeEstimate(0, 0, Included: false);

        double total = 0, remaining = 0;
        foreach (var c in onScreen)
        {
            // Plan for a full generate cycle per character; locked looks are already paid for.
            var planned = variants * unit;
            total += planned;
            if (!c.Locked)
                remaining += planned;
        }

        return new ScopeEstimate(Math.Round(total, 4), Math.Round(remaining, 4), Included: true);
    }

    /// <summary>
    /// Personal voice only when the project has opted in (clone samples / profiles) or
    /// cost_estimates.include_voice is true. Priced from voice model catalog when present.
    /// </summary>
    private ScopeEstimate EstimateVoiceGeneration(
        string projectId,
        Dictionary<string, object?> rates,
        Dictionary<string, JsonElement> cfg)
    {
        var include = false;
        double cloneUsd = 0.0;
        double ttsPerCharUsd = 0.0;

        if (cfg.TryGetValue("cost_estimates", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            if (ce.TryGetProperty("include_voice", out var iv) &&
                iv.ValueKind is JsonValueKind.True or JsonValueKind.False)
                include = iv.GetBoolean();
            if (ce.TryGetProperty("voice_clone_usd", out var vc) && vc.TryGetDouble(out var v1))
                cloneUsd = v1;
            if (ce.TryGetProperty("voice_tts_per_character_usd", out var vt) && vt.TryGetDouble(out var v2))
                ttsPerCharUsd = v2;
        }

        var chars = _projects.ListCharacters(projectId);
        var withVoice = chars.Where(c =>
            c.HasVoiceCloneSample || !string.IsNullOrWhiteSpace(c.VoiceProfile)).ToList();
        if (withVoice.Count > 0)
            include = true;

        if (!include || chars.Count == 0)
            return new ScopeEstimate(0, 0, Included: false);

        // Prefer catalog voice entry if it ever gains unit prices; else planning knobs / defaults.
        var voiceId = GetStr(cfg, "voice_model_name", "eleven_multilingual_v2");
        var voiceEntry = SupportedModelCatalog.Find(voiceId, ModelCapability.Voice);
        // Defaults: clone ~$0.00 when sample already on disk; TTS dialogue budget per speaking role.
        if (cloneUsd <= 0) cloneUsd = 0.0; // clone API often free tier / included — keep 0 unless configured
        if (ttsPerCharUsd <= 0) ttsPerCharUsd = 0.12; // rough list-rate stand-in per speaking character

        var targets = withVoice.Count > 0 ? withVoice : chars.Where(c => !c.VoiceOnly).ToList();
        if (targets.Count == 0)
            targets = chars.ToList();

        double total = 0, remaining = 0;
        foreach (var c in targets)
        {
            var line = ttsPerCharUsd + (c.HasVoiceCloneSample ? 0 : cloneUsd);
            total += line;
            // Still generating film dialogue for everyone in scope until video is done — keep remaining = total for now
            // unless voice profile already approved and we only need TTS at film time (still unpaid).
            remaining += line;
        }

        _ = voiceEntry;
        _ = rates;
        return new ScopeEstimate(Math.Round(total, 4), Math.Round(remaining, 4), Included: true);
    }

    /// <summary>Background music: one track per scene with media plan, using audio model if priced.</summary>
    private static ScopeEstimate EstimateMusicGeneration(
        List<BlueprintSceneClips> scenes,
        Dictionary<string, object?> rates,
        Dictionary<string, JsonElement> cfg)
    {
        if (scenes.Count == 0)
            return new ScopeEstimate(0, 0, Included: false);

        var perScene = 0.08;
        if (cfg.TryGetValue("cost_estimates", out var ce) && ce.ValueKind == JsonValueKind.Object &&
            ce.TryGetProperty("music_per_scene_usd", out var m) && m.TryGetDouble(out var mv) && mv >= 0)
            perScene = mv;

        // Disable when audio model is "none"
        var audioModel = GetStr(cfg, "audio_model_name", "");
        if (string.Equals(audioModel, "none", StringComparison.OrdinalIgnoreCase))
            return new ScopeEstimate(0, 0, Included: false);

        var n = scenes.Count(s => s.Clips.Count > 0);
        if (n == 0) n = scenes.Count;
        var total = n * perScene;
        _ = rates;
        return new ScopeEstimate(Math.Round(total, 4), Math.Round(total, 4), Included: total > 0);
    }

    /// <summary>
    /// Screenplay / shot-plan LLM work. Uses chat model token rates when available;
    /// after import the screenplay pass is treated as done (remaining ≈ shot plan only).
    /// </summary>

    /// <summary>
    /// Post-clip automated review (Gemini dialogue check / optional clip auto-review).
    /// When quality-gate retry is on, count re-review after expected failed regens.
    /// Extra <em>video</em> cost for regens is folded into clip attempts via retries.
    /// </summary>

    private ApiCostHistoryStats? _historyApiStats;

    private async Task<CostEstimateRefinement> BuildHistoryRefinementAsync(
        string projectId,
        double priorVideoMultiplier,
        bool qaRetryOnFail,
        int qaMaxRetries,
        CostLedgerSummary projectActual,
        CancellationToken ct)
    {
        var refn = new CostEstimateRefinement
        {
            PriorVideoMultiplier = priorVideoMultiplier,
            AppliedVideoMultiplier = qaRetryOnFail ? priorVideoMultiplier : 1.0,
            ProjectLedgerEvents = projectActual.EventCount,
        };

        double? failRate = null;
        var timingSamples = 0;
        if (_timingDb is not null)
        {
            var global = await _timingDb.GetDialogueFailRateAsync(MinTimingSamples, projectId: null, ct)
                .ConfigureAwait(false);
            var project = await _timingDb.GetDialogueFailRateAsync(MinTimingSamples, projectId, ct)
                .ConfigureAwait(false);
            if (project is { } p)
            {
                failRate = p.FailRate;
                timingSamples = p.Samples;
            }
            else if (global is { } g)
            {
                failRate = g.FailRate;
                timingSamples = g.Samples;
            }
        }
        refn.TimingSamples = timingSamples;
        refn.LearnedFailRate = failRate;

        ApiCostHistoryStats? apiStats = null;
        if (_userDb is not null)
        {
            var projectStats = await _userDb.GetApiCostHistoryStatsAsync(userId: null, projectId, ct)
                .ConfigureAwait(false);
            var globalStats = await _userDb.GetApiCostHistoryStatsAsync(userId: null, projectId: null, ct)
                .ConfigureAwait(false);
            apiStats = projectStats.TotalCalls >= MinApiSamples ? projectStats : globalStats;
            if (apiStats.ByCategory.TryGetValue(CostCategories.Video, out var v))
                refn.VideoApiSamples = v.Count;
            if (apiStats.ByCategory.TryGetValue(CostCategories.Review, out var r))
                refn.ReviewApiSamples = r.Count;
        }

        var learnedMult = priorVideoMultiplier;
        if (qaRetryOnFail && failRate is double fr)
        {
            learnedMult = 1.0 + Math.Clamp(fr, 0, 0.9) * Math.Max(1, qaMaxRetries);
            learnedMult = Math.Clamp(learnedMult, 1.0, 2.5);
        }

        var w = timingSamples <= 0 ? 0.0 : Math.Min(1.0, timingSamples / 30.0);
        refn.HistoryWeight = w;
        if (qaRetryOnFail)
            refn.AppliedVideoMultiplier = Math.Round(priorVideoMultiplier * (1 - w) + learnedMult * w, 3);

        refn.UsedHistory = timingSamples >= MinTimingSamples || refn.VideoApiSamples >= MinApiSamples
            || projectActual.EventCount >= MinApiSamples;

        var bits = new List<string>();
        if (timingSamples >= MinTimingSamples && failRate is double fr2)
            bits.Add($"timing QA fail ~{fr2:P0} (n={timingSamples})");
        if (refn.VideoApiSamples > 0)
            bits.Add($"{refn.VideoApiSamples} video API samples");
        if (refn.ReviewApiSamples > 0)
            bits.Add($"{refn.ReviewApiSamples} review API samples");
        if (projectActual.EventCount > 0)
            bits.Add($"{projectActual.EventCount} project ledger events");
        if (bits.Count == 0)
            bits.Add("no history yet — using catalog priors");
        else if (w > 0 && qaRetryOnFail)
            bits.Add($"video mult {priorVideoMultiplier:0.##}→{refn.AppliedVideoMultiplier:0.##} (weight {w:0.00})");
        refn.Notes = "History: " + string.Join("; ", bits) + ".";

        _historyApiStats = apiStats;
        return refn;
    }

    private void ApplyHistoryUnitCosts(
        Dictionary<string, double> estimateByCategory,
        CostEstimateRefinement refinement,
        int clipsTotal)
    {
        var stats = _historyApiStats;
        if (stats is null || stats.TotalCalls < MinApiSamples)
            return;

        void Blend(string cat, double plannedUnits, double catalogTotal)
        {
            if (!stats.ByCategory.TryGetValue(cat, out var row) || row.Count < MinApiSamples)
                return;
            if (plannedUnits <= 0 || catalogTotal <= 0)
                return;
            var empiric = row.AvgUsd * plannedUnits;
            var w = Math.Min(1.0, row.Count / 40.0);
            var blended = catalogTotal * (1 - w) + empiric * w;
            blended = Math.Clamp(blended, catalogTotal * 0.4, catalogTotal * 2.5);
            estimateByCategory[cat] = Math.Round(blended, 2);
            refinement.UsedHistory = true;
            refinement.HistoryWeight = Math.Max(refinement.HistoryWeight, w);
        }

        Blend(CostCategories.Video, Math.Max(1, clipsTotal), estimateByCategory.GetValueOrDefault(CostCategories.Video));
        Blend(CostCategories.Review, Math.Max(1, clipsTotal), estimateByCategory.GetValueOrDefault(CostCategories.Review));
        Blend(CostCategories.Characters, 1, estimateByCategory.GetValueOrDefault(CostCategories.Characters));
        Blend(CostCategories.Screenplay, 1, estimateByCategory.GetValueOrDefault(CostCategories.Screenplay));
        Blend(CostCategories.Voice, 1, estimateByCategory.GetValueOrDefault(CostCategories.Voice));
        Blend(CostCategories.Music, Math.Max(1, clipsTotal > 0 ? clipsTotal / 4.0 : 1),
            estimateByCategory.GetValueOrDefault(CostCategories.Music));
    }

    private static ScopeEstimate EstimateAutomatedReview(
        int clipsTotal,
        Dictionary<string, object?> rates,
        Dictionary<string, JsonElement> cfg,
        bool qaRetryOnFail,
        double qaVideoMultiplier)
    {
        if (clipsTotal <= 0)
            return new ScopeEstimate(0, 0, Included: false);

        // Prefer quality/vision model for Gemini-style native video dialogue QA.
        var reviewModelId = GetStr(cfg, "quality_model_name",
            GetStr(cfg, "vision_model_name",
                GetStr(cfg, "planning_model_name", "grok-4.5")));
        var entry = SupportedModelCatalog.Find(reviewModelId, ModelCapability.Vision)
                    ?? SupportedModelCatalog.Find(reviewModelId, ModelCapability.Chat)
                    ?? SupportedModelCatalog.ResolveOrDefault(reviewModelId, ModelCapability.Chat);

        var inRate = (entry.InputCostPerMillionTokens ?? 2.0) / 1_000_000.0;
        var outRate = (entry.OutputCostPerMillionTokens ?? 10.0) / 1_000_000.0;

        // One dialogue/speaker verification pass per clip (video+audio multimodal prior).
        // Token priors are stand-ins until we average from telemetry.
        var inTok = 28_000.0;
        var outTok = 1_200.0;
        // Review passes track video multiplier: ~1.3× checks when QA retry is on (re-check after regen).
        var reviewPasses = qaRetryOnFail ? Math.Max(1.0, qaVideoMultiplier) : 1.0;
        if (cfg.TryGetValue("cost_estimates", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            if (ce.TryGetProperty("review_input_tokens_per_clip", out var it) && it.TryGetDouble(out var itv) && itv > 0)
                inTok = itv;
            if (ce.TryGetProperty("review_output_tokens_per_clip", out var ot) && ot.TryGetDouble(out var otv) && otv > 0)
                outTok = otv;
            if (ce.TryGetProperty("review_usd_per_clip", out var fixedUsd) && fixedUsd.TryGetDouble(out var fu) && fu > 0)
            {
                var totalFixed = clipsTotal * fu * reviewPasses;
                return new ScopeEstimate(Math.Round(totalFixed, 4), Math.Round(totalFixed, 4), Included: true);
            }
        }

        var perClip = inTok * inRate + outTok * outRate;
        var total = clipsTotal * perClip * reviewPasses;
        _ = rates;
        return new ScopeEstimate(Math.Round(total, 4), Math.Round(total, 4), Included: true);
    }

    private static bool GetCfgBool(Dictionary<string, JsonElement> cfg, string key, bool defaultValue)
    {
        if (!cfg.TryGetValue(key, out var el)) return defaultValue;
        if (el.ValueKind is JsonValueKind.True) return true;
        if (el.ValueKind is JsonValueKind.False) return false;
        if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b)) return b;
        return defaultValue;
    }

    private static int GetCfgInt(Dictionary<string, JsonElement> cfg, string key, int defaultValue)
    {
        if (!cfg.TryGetValue(key, out var el)) return defaultValue;
        if (el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var j)) return j;
        if (el.TryGetDouble(out var d)) return (int)Math.Round(d);
        return defaultValue;
    }

    private static ScopeEstimate EstimatePlanningWork(
        List<BlueprintSceneClips> scenes,
        string estimateBasis,
        Dictionary<string, object?> rates,
        Dictionary<string, JsonElement> cfg)
    {
        if (scenes.Count == 0 && estimateBasis == "none")
            return new ScopeEstimate(0, 0, Included: false);

        var planningId = GetStr(cfg, "planning_model_name",
            GetStr(cfg, "chat_model_name", "grok-4.5"));
        var entry = SupportedModelCatalog.Find(planningId, ModelCapability.Chat)
                    ?? SupportedModelCatalog.ResolveOrDefault(planningId, ModelCapability.Chat);

        // Rough token budgets: import screenplay ~80k in / 20k out; shot plan ~40k / 25k.
        var inRate = (entry.InputCostPerMillionTokens ?? 2.0) / 1_000_000.0;
        var outRate = (entry.OutputCostPerMillionTokens ?? 10.0) / 1_000_000.0;

        var sceneN = Math.Max(1, scenes.Count);
        var importUsd = (80_000 * inRate) + (20_000 * outRate);
        // Scale mild with scene count
        importUsd *= Math.Clamp(sceneN / 12.0, 0.6, 2.5);
        var shotPlanUsd = (40_000 * inRate) + (25_000 * outRate);
        shotPlanUsd *= Math.Clamp(sceneN / 12.0, 0.6, 2.5);

        double total, remaining;
        if (estimateBasis == "shot_plan")
        {
            // Both passes done for planning purposes
            total = importUsd + shotPlanUsd;
            remaining = 0;
        }
        else if (estimateBasis == "screenplay")
        {
            total = importUsd + shotPlanUsd;
            remaining = shotPlanUsd; // shot plan still ahead
        }
        else
        {
            total = importUsd + shotPlanUsd;
            remaining = total;
        }

        _ = rates;
        return new ScopeEstimate(Math.Round(total, 4), Math.Round(remaining, 4), Included: true);
    }

    private static double ToPositiveDouble(object? v, double fallback)
    {
        if (v is null) return fallback;
        try
        {
            var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            return d > 0 ? d : fallback;
        }
        catch { return fallback; }
    }

    private sealed class BlueprintSceneClips
    {
        public int SceneNumber { get; set; }
        public string Setting { get; set; } = "";
        public List<BlueprintClip> Clips { get; set; } = new();
        public List<string> CharactersOnScreen { get; set; } = new();
        public List<string> LocationIds { get; set; } = new();
        public string? PrimaryLocationId { get; set; }
    }

    private sealed class BlueprintClip
    {
        public int ClipNumber { get; set; }
        public double DurationSec { get; set; }
        public string Continuation { get; set; } = "none";
    }
}
