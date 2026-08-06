using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Models;

namespace PageToMovie.Engine;

/// <summary>
/// Admin-only: probes public vendor docs/APIs and compares against models_catalog.json.
/// Status: unchanged (green), changed (red), not_found (yellow), error (yellow).
/// Does not write the catalog — caller accepts selected patches then SaveCatalogJson.
/// </summary>
public sealed class CatalogUpdateProbeService
{
    private readonly IHttpClientFactory _httpFactory;

    public CatalogUpdateProbeService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<CatalogUpdateScanResult> ScanAsync(CancellationToken ct = default)
    {
        SupportedModelCatalog.ReloadCatalog();
        var result = new CatalogUpdateScanResult
        {
            CheckedAtUtc = DateTime.UtcNow.ToString("o"),
        };

        foreach (var entry in SupportedModelCatalog.Entries.Where(e => e.Enabled))
        {
            var row = new CatalogModelProbeResult
            {
                Id = entry.Id,
                DisplayName = entry.DisplayName,
                Capability = entry.Capability.ToString(),
                ProviderId = entry.ProviderId,
                LabMode = entry.LabMode,
            };

            try
            {
                await ProbeEntryAsync(entry, row, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                row.Fields.Add(new CatalogFieldProbeResult
                {
                    Field = "(probe)",
                    CatalogValue = null,
                    LiveValue = null,
                    Status = "error",
                    Message = ex.Message,
                });
            }

            // Summarize
            if (row.Fields.Count == 0)
            {
                row.Fields.Add(new CatalogFieldProbeResult
                {
                    Field = "(no probes)",
                    Status = "not_found",
                    Message = "No automated probe registered for this model yet.",
                });
            }

            result.Models.Add(row);
        }

        try
        {
            await DiscoverNewModelsAsync(result, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.DiscoveryNotes.Add("New-model discovery error: " + ex.Message);
        }

        result.Summary = new CatalogUpdateSummary
        {
            ModelsScanned = result.Models.Count,
            UnchangedFields = result.Models.SelectMany(m => m.Fields).Count(f => f.Status == "unchanged"),
            ChangedFields = result.Models.SelectMany(m => m.Fields).Count(f => f.Status == "changed"),
            NotFoundFields = result.Models.SelectMany(m => m.Fields).Count(f => f.Status is "not_found" or "error"),
            NewModels = result.NewModels.Count,
        };
        return result;
    }

    private async Task ProbeEntryAsync(SupportedModelEntry entry, CatalogModelProbeResult row, CancellationToken ct)
    {
        var provider = entry.ProviderId ?? "";

        // Capability-agnostic: review dates age (informational, not live)
        if (!string.IsNullOrWhiteSpace(entry.PricingLastReviewedAt) &&
            DateTime.TryParse(entry.PricingLastReviewedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var reviewed) &&
            (DateTime.UtcNow.Date - reviewed.Date).TotalDays > 90)
        {
            row.Fields.Add(new CatalogFieldProbeResult
            {
                Field = "pricingLastReviewedAt",
                CatalogValue = entry.PricingLastReviewedAt,
                LiveValue = null,
                Status = "not_found",
                Message = "Last cost review > 90 days ago — re-check vendor pricing.",
            });
        }

        if (entry.Capability == ModelCapability.Video &&
            string.Equals(provider, "xai", StringComparison.OrdinalIgnoreCase))
        {
            await ProbeXaiVideoAsync(entry, row, ct).ConfigureAwait(false);
            return;
        }

        if (entry.Capability is ModelCapability.Chat or ModelCapability.Vision &&
            string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            await ProbeOpenAiModelExistsAsync(entry, row, ct).ConfigureAwait(false);
            return;
        }

        if (entry.Capability is ModelCapability.Chat or ModelCapability.Vision &&
            string.Equals(provider, "xai", StringComparison.OrdinalIgnoreCase))
        {
            await ProbeXaiChatExistsAsync(entry, row, ct).ConfigureAwait(false);
            return;
        }

        // Generic: mark key required fields as not_found when no probe
        row.Fields.Add(new CatalogFieldProbeResult
        {
            Field = "live_probe",
            CatalogValue = entry.Id,
            Status = "not_found",
            Message = $"No live probe for provider '{provider}' / {entry.Capability}. Review manually.",
            SourceUrl = entry.PricingNotes,
        });
    }

    private async Task ProbeXaiVideoAsync(SupportedModelEntry entry, CatalogModelProbeResult row, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("catalog-probe");
        // Public docs — extension duration 2–10, generation 1–15 (as of docs scan)
        string? extHtml = null;
        string? genHtml = null;
        try
        {
            extHtml = await client.GetStringAsync(
                "https://docs.x.ai/developers/model-capabilities/video/extension", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            row.Fields.Add(Field("docs.extension", null, null, "error", ex.Message,
                "https://docs.x.ai/developers/model-capabilities/video/extension"));
        }

        try
        {
            genHtml = await client.GetStringAsync(
                "https://docs.x.ai/developers/model-capabilities/video/generation", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            row.Fields.Add(Field("docs.generation", null, null, "error", ex.Message,
                "https://docs.x.ai/developers/model-capabilities/video/generation"));
        }

        // Extension max: docs say 2–10 seconds
        if (!string.IsNullOrEmpty(extHtml))
        {
            var m = Regex.Match(extHtml, @"extension duration range is\s+\*?\*?(\d+)\s*[–-]\s*(\d+)\s*seconds",
                RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(extHtml, @"(\d+)\s*[–-]\s*(\d+)\s*seconds", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[2].Value, out var liveExtMax))
            {
                var catalog = entry.MaxExtensionSeconds;
                row.Fields.Add(CompareInt("maxExtensionSeconds", catalog, liveExtMax,
                    "https://docs.x.ai/developers/model-capabilities/video/extension"));
            }
            else
            {
                row.Fields.Add(Field("maxExtensionSeconds", entry.MaxExtensionSeconds?.ToString(), null, "not_found",
                    "Could not parse extension duration from docs.",
                    "https://docs.x.ai/developers/model-capabilities/video/extension"));
            }
        }

        if (!string.IsNullOrEmpty(genHtml))
        {
            var m = Regex.Match(genHtml, @"allowed range is\s+(\d+)\s*[–-]\s*(\d+)\s*seconds",
                RegexOptions.IgnoreCase);
            if (!m.Success)
                m = Regex.Match(genHtml, @"(\d+)\s*[–-]\s*(\d+)\s*seconds", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[2].Value, out var liveMax))
            {
                row.Fields.Add(CompareInt("maxClipDurationSeconds", entry.MaxClipDurationSeconds, liveMax,
                    "https://docs.x.ai/developers/model-capabilities/video/generation"));
                row.Fields.Add(CompareInt("absMaxClipDurationSeconds", entry.AbsMaxClipDurationSeconds, liveMax,
                    "https://docs.x.ai/developers/model-capabilities/video/generation"));
            }
            else
            {
                row.Fields.Add(Field("maxClipDurationSeconds", entry.MaxClipDurationSeconds?.ToString(), null, "not_found",
                    "Could not parse generation duration from docs.",
                    "https://docs.x.ai/developers/model-capabilities/video/generation"));
            }
        }

        // Reference images: multi-image docs historically say 7
        try
        {
            var refHtml = await client.GetStringAsync(
                "https://docs.x.ai/developers/model-capabilities/video/reference-to-video", ct).ConfigureAwait(false);
            var rm = Regex.Match(refHtml, @"maximum of\s+\*?\*?(\d+)\s+reference images", RegexOptions.IgnoreCase);
            if (rm.Success && int.TryParse(rm.Groups[1].Value, out var liveRefs))
            {
                row.Fields.Add(CompareInt("maxReferenceImages", entry.MaxReferenceImages, liveRefs,
                    "https://docs.x.ai/developers/model-capabilities/video/reference-to-video"));
            }
            else
            {
                row.Fields.Add(Field("maxReferenceImages", entry.MaxReferenceImages?.ToString(), null, "not_found",
                    "Could not parse max reference images.",
                    "https://docs.x.ai/developers/model-capabilities/video/reference-to-video"));
            }
        }
        catch (Exception ex)
        {
            row.Fields.Add(Field("maxReferenceImages", entry.MaxReferenceImages?.ToString(), null, "error", ex.Message,
                "https://docs.x.ai/developers/model-capabilities/video/reference-to-video"));
        }
    }

    private async Task ProbeOpenAiModelExistsAsync(SupportedModelEntry entry, CatalogModelProbeResult row, CancellationToken ct)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            row.Fields.Add(Field("model_id", entry.Id, null, "not_found",
                "OPENAI_API_KEY not set — cannot list OpenAI models.", "https://platform.openai.com/docs/models"));
            return;
        }

        var client = _httpFactory.CreateClient("catalog-probe");
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            row.Fields.Add(Field("model_id", entry.Id, null, "error", $"OpenAI models list HTTP {(int)resp.StatusCode}",
                "https://api.openai.com/v1/models"));
            return;
        }

        using var doc = JsonDocument.Parse(body);
        var found = false;
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in data.EnumerateArray())
            {
                if (m.TryGetProperty("id", out var idEl) &&
                    string.Equals(idEl.GetString(), entry.Id, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }

        row.Fields.Add(new CatalogFieldProbeResult
        {
            Field = "model_id",
            CatalogValue = entry.Id,
            LiveValue = found ? entry.Id : null,
            Status = found ? "unchanged" : "not_found",
            Message = found ? "Present in OpenAI /v1/models." : "Not present in OpenAI /v1/models for this API key.",
            SourceUrl = "https://api.openai.com/v1/models",
        });
    }

    private async Task ProbeXaiChatExistsAsync(SupportedModelEntry entry, CatalogModelProbeResult row, CancellationToken ct)
    {
        var key = Environment.GetEnvironmentVariable("XAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            row.Fields.Add(Field("model_id", entry.Id, null, "not_found",
                "XAI_API_KEY not set — cannot list xAI models.", "https://api.x.ai/v1/models"));
            return;
        }

        var client = _httpFactory.CreateClient("catalog-probe");
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.x.ai/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            row.Fields.Add(Field("model_id", entry.Id, null, "error", $"xAI models list HTTP {(int)resp.StatusCode}",
                "https://api.x.ai/v1/models"));
            return;
        }

        using var doc = JsonDocument.Parse(body);
        var found = false;
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in data.EnumerateArray())
            {
                if (m.TryGetProperty("id", out var idEl) &&
                    string.Equals(idEl.GetString(), entry.Id, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }

        row.Fields.Add(new CatalogFieldProbeResult
        {
            Field = "model_id",
            CatalogValue = entry.Id,
            LiveValue = found ? entry.Id : null,
            Status = found ? "unchanged" : "not_found",
            Message = found ? "Present in xAI /v1/models." : "Not present in xAI /v1/models for this API key.",
            SourceUrl = "https://api.x.ai/v1/models",
        });
    }

    private async Task DiscoverNewModelsAsync(CatalogUpdateScanResult result, CancellationToken ct)
    {
        var known = new HashSet<string>(
            SupportedModelCatalog.Entries.Select(e => e.Id),
            StringComparer.OrdinalIgnoreCase);

        await DiscoverFromOpenAiAsync(result, known, ct).ConfigureAwait(false);
        await DiscoverFromXaiAsync(result, known, ct).ConfigureAwait(false);
    }

    private async Task DiscoverFromOpenAiAsync(CatalogUpdateScanResult result, HashSet<string> known, CancellationToken ct)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            result.DiscoveryNotes.Add("OpenAI: skipped (no OPENAI_API_KEY).");
            return;
        }

        var client = _httpFactory.CreateClient("catalog-probe");
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            result.DiscoveryNotes.Add($"OpenAI: list failed HTTP {(int)resp.StatusCode}");
            return;
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return;
        var added = 0;
        foreach (var m in data.EnumerateArray())
        {
            var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || known.Contains(id)) continue;
            // Only surface chat-like gpt / o-series to avoid noise
            if (!(id.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
                  id.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
                  id.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
                  id.StartsWith("o4", StringComparison.OrdinalIgnoreCase)))
                continue;
            result.NewModels.Add(new CatalogNewModelHint
            {
                Id = id,
                Provider = "OpenAI",
                ProviderId = "openai",
                SuggestedCapability = "Chat",
                Source = "OpenAI GET /v1/models",
                LabMode = true,
                LabNotes = "Discovered via OpenAI models list — add as lab and fill limits/costs before production.",
            });
            known.Add(id);
            if (++added >= 25) break;
        }
        result.DiscoveryNotes.Add($"OpenAI: {added} candidate model(s) not in catalog.");
    }

    private async Task DiscoverFromXaiAsync(CatalogUpdateScanResult result, HashSet<string> known, CancellationToken ct)
    {
        var key = Environment.GetEnvironmentVariable("XAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            result.DiscoveryNotes.Add("xAI: skipped (no XAI_API_KEY).");
            return;
        }

        var client = _httpFactory.CreateClient("catalog-probe");
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.x.ai/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            result.DiscoveryNotes.Add($"xAI: list failed HTTP {(int)resp.StatusCode}");
            return;
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return;
        var added = 0;
        foreach (var m in data.EnumerateArray())
        {
            var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || known.Contains(id)) continue;
            result.NewModels.Add(new CatalogNewModelHint
            {
                Id = id,
                Provider = "Xai",
                ProviderId = "xai",
                SuggestedCapability = id.Contains("video", StringComparison.OrdinalIgnoreCase) ? "Video"
                    : id.Contains("image", StringComparison.OrdinalIgnoreCase) ? "Image" : "Chat",
                Source = "xAI GET /v1/models",
                LabMode = true,
                LabNotes = "Discovered via xAI models list — add as lab and fill limits/costs before production.",
            });
            known.Add(id);
            if (++added >= 25) break;
        }
        result.DiscoveryNotes.Add($"xAI: {added} candidate model(s) not in catalog.");
    }

    private static CatalogFieldProbeResult CompareInt(string field, int? catalog, int live, string? url)
    {
        if (catalog is null)
        {
            return Field(field, null, live.ToString(CultureInfo.InvariantCulture), "changed",
                "Catalog missing; live value available.", url);
        }
        if (catalog.Value == live)
        {
            return Field(field, catalog.Value.ToString(CultureInfo.InvariantCulture),
                live.ToString(CultureInfo.InvariantCulture), "unchanged", "Matches live docs/API.", url);
        }
        return Field(field, catalog.Value.ToString(CultureInfo.InvariantCulture),
            live.ToString(CultureInfo.InvariantCulture), "changed", "Catalog differs from live probe.", url);
    }

    private static CatalogFieldProbeResult Field(
        string field, string? catalog, string? live, string status, string? message, string? url = null) =>
        new()
        {
            Field = field,
            CatalogValue = catalog,
            LiveValue = live,
            Status = status,
            Message = message,
            SourceUrl = url,
        };
}

public sealed class CatalogUpdateScanResult
{
    public string CheckedAtUtc { get; set; } = "";
    public CatalogUpdateSummary Summary { get; set; } = new();
    public List<CatalogModelProbeResult> Models { get; set; } = new();
    public List<CatalogNewModelHint> NewModels { get; set; } = new();
    public List<string> DiscoveryNotes { get; set; } = new();
}

public sealed class CatalogUpdateSummary
{
    public int ModelsScanned { get; set; }
    public int UnchangedFields { get; set; }
    public int ChangedFields { get; set; }
    public int NotFoundFields { get; set; }
    public int NewModels { get; set; }
}

public sealed class CatalogModelProbeResult
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Capability { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public bool LabMode { get; set; }
    public List<CatalogFieldProbeResult> Fields { get; set; } = new();
}

public sealed class CatalogFieldProbeResult
{
    /// <summary>unchanged | changed | not_found | error</summary>
    public string Status { get; set; } = "not_found";
    public string Field { get; set; } = "";
    public string? CatalogValue { get; set; }
    public string? LiveValue { get; set; }
    public string? Message { get; set; }
    public string? SourceUrl { get; set; }
}

public sealed class CatalogNewModelHint
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string SuggestedCapability { get; set; } = "Chat";
    public string Source { get; set; } = "";
    public bool LabMode { get; set; } = true;
    public string? LabNotes { get; set; }
}
