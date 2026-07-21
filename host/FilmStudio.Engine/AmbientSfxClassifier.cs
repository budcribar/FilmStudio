using System.Text.Json;
using System.Text.RegularExpressions;
using FilmStudio.Core.Options;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

/// <summary>
/// Chat refine of ambient bed vs SFX for beats. Heuristic: <see cref="FountainStage1Importer.InferAmbientAndSfx"/>.
/// Policy: AI preferred → retry → keep heuristic. Never fall back merely because AI differs.
/// </summary>
public sealed class AmbientSfxClassifier
{
    public const string PromptVersion = "v1";
    public const string DefaultModel = "grok-4.5";
    public const int DefaultBatchSize = 30;

    private readonly IGrokChatClient _chat;
    private readonly FilmStudioOptions _opts;
    private readonly ILogger<AmbientSfxClassifier> _log;

    public AmbientSfxClassifier(
        IGrokChatClient chat,
        IOptions<FilmStudioOptions> opts,
        ILogger<AmbientSfxClassifier> log)
    {
        _chat = chat;
        _opts = opts.Value;
        _log = log;
    }

    public bool IsEnabled =>
        _opts.ClassifyAmbientSfxWithChat && _chat.IsConfigured;

    public async Task<AmbientSfxClassifyResult> ClassifyStage1Async(
        Dictionary<string, object?> stage1,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var model = string.IsNullOrWhiteSpace(_opts.AmbientSfxClassifyModel)
            ? DefaultModel
            : _opts.AmbientSfxClassifyModel.Trim();
        var temp = _opts.AmbientSfxClassifyTemperature;
        if (double.IsNaN(temp) || temp < 0) temp = 0;
        var maxAttempts = Math.Clamp(_opts.AmbientSfxClassifyMaxAttempts, 1, 5);
        var result = new AmbientSfxClassifyResult
        {
            PromptVersion = PromptVersion,
            Model = model,
            Temperature = temp,
            Enabled = IsEnabled,
        };

        var targets = CollectBeats(stage1);
        result.BeatCount = targets.Count;
        foreach (var t in targets)
        {
            var (a, s) = FountainStage1Importer.InferAmbientAndSfx(t.VisualEvent);
            t.HeuristicAmbient = a;
            t.HeuristicSfx = s;
            Apply(t.Beat, a, s);
        }

        if (!IsEnabled || targets.Count == 0)
        {
            result.FallbackCount = targets.Count;
            result.Note = !IsEnabled ? "disabled or chat not configured" : "no beats";
            onProgress?.Invoke($"Ambient/SFX: heuristic only ({targets.Count})");
            return result;
        }

        onProgress?.Invoke($"Classifying ambient/SFX for {targets.Count} beat(s)…");
        var byId = targets.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        var labeled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalAttempts = 0;

        for (var offset = 0; offset < targets.Count; offset += DefaultBatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = targets.Skip(offset).Take(DefaultBatchSize).ToList();
            var missing = chunk.Select(t => t.Id).ToList();
            for (var attempt = 1; attempt <= maxAttempts && missing.Count > 0; attempt++)
            {
                totalAttempts++;
                try
                {
                    var batch = missing.Select(id => byId[id]).ToList();
                    var raw = await CallAsync(batch, model, temp, ct).ConfigureAwait(false);
                    result.ChatCalls++;
                    var parsed = ParseLabels(raw);
                    foreach (var id in missing.ToList())
                    {
                        if (!parsed.TryGetValue(id, out var pair)) continue;
                        var t = byId[id];
                        Apply(t.Beat, pair.Ambient, pair.Sfx);
                        missing.Remove(id);
                        labeled.Add(id);
                    }
                    if (missing.Count > 0)
                        await BackoffAsync(attempt, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "AmbientSfx classify attempt {A} failed", attempt);
                    result.LastError = Trim(ex.Message, 200);
                    await BackoffAsync(attempt, ct).ConfigureAwait(false);
                }
            }
        }

        result.Attempts = totalAttempts;
        result.AiCount = labeled.Count;
        result.FallbackCount = targets.Count - labeled.Count;
        result.Note = $"AI {labeled.Count}/{targets.Count}; heuristic kept {result.FallbackCount}";
        onProgress?.Invoke($"Ambient/SFX: {result.Note}");
        return result;
    }

    private async Task<string> CallAsync(
        List<Target> batch, string model, double temp, CancellationToken ct)
    {
        var payload = batch.Select(b => new Dictionary<string, object?>
        {
            ["id"] = b.Id,
            ["visual_event"] = Trunc(b.VisualEvent, 320),
            ["heuristic_ambient"] = b.HeuristicAmbient,
            ["heuristic_sfx"] = b.HeuristicSfx,
        }).ToList();
        var user = "Split each beat into ambient bed vs sfx hits. JSON only.\n" +
                   JsonSerializer.Serialize(new { beats = payload });
        return await _chat.CompleteAsync(SystemPrompt(), user, model, temp, ct, ChatCallModes.AmbientSfxClassify)
            .ConfigureAwait(false);
    }

    public static string SystemPrompt() => """
You label film audio layers from silent or action visual prose (any story).

Return continuous ambient BED vs transient SFX hits as short lowercase phrases.

Rules:
- ambient: ongoing bed (rain, wind, room tone, fire crackle, distant traffic, soft underscore, waves). Empty if none.
- sfx: discrete events (knock, door slam, crash, footsteps as a hit, glass break, phone ring). Empty if none.
- Do not invent weather or doors not implied by the visual.
- Prefer short tokens suitable for an audio_payload (comma-separated).
- You may refine or correct the heuristic_* fields.

JSON only:
{"labels":[{"id":"s1_b1","ambient":"rain, distant traffic","sfx":"door slam"}]}
""";

    public static Dictionary<string, (string Ambient, string Sfx)> ParseLabels(string raw)
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        raw = StripFences(raw);
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var arr = root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("labels", out var l) ? l : default;
            if (arr.ValueKind != JsonValueKind.Array) return map;
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var a = el.TryGetProperty("ambient", out var aEl) ? aEl.GetString() ?? "" : "";
                var s = el.TryGetProperty("sfx", out var sEl) ? sEl.GetString() ?? "" : "";
                map[id!] = (NormalizeList(a), NormalizeList(s));
            }
        }
        catch { /* retry */ }
        return map;
    }

    /// <summary>Jaccard of comma/space tokens; empty vs empty = 1.</summary>
    public static double TokenJaccard(string? a, string? b)
    {
        var ta = Tokens(a);
        var tb = Tokens(b);
        if (ta.Count == 0 && tb.Count == 0) return 1.0;
        if (ta.Count == 0 || tb.Count == 0) return 0.0;
        var inter = ta.Intersect(tb, StringComparer.OrdinalIgnoreCase).Count();
        var union = ta.Union(tb, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 1.0 : (double)inter / union;
    }

    public static HashSet<string> Tokens(string? s)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(s)) return set;
        foreach (var part in Regex.Split(s.ToLowerInvariant(), @"[,;/|]+|\s{2,}"))
        {
            var t = part.Trim().Trim('.', ' ');
            if (t.Length < 2) continue;
            // also split single commas already handled; keep multiword phrases as one token
            set.Add(t);
        }
        if (set.Count == 0)
        {
            foreach (var w in s!.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (w.Length >= 3) set.Add(w);
        }
        return set;
    }

    private static void Apply(Dictionary<string, object?> beat, string ambient, string sfx)
    {
        beat["ambient"] = ambient;
        beat["sfx"] = sfx;
        if (beat.TryGetValue("audio", out var a) && a is Dictionary<string, object?> audio)
        {
            audio["ambient"] = ambient;
            audio["sfx"] = sfx;
        }
    }

    private static List<Target> CollectBeats(Dictionary<string, object?> stage1)
    {
        var list = new List<Target>();
        var scenes = stage1.TryGetValue("scenes", out var sObj) && sObj is List<object?> sl ? sl : new();
        var si = 0;
        foreach (var sItem in scenes)
        {
            if (sItem is not Dictionary<string, object?> scene) continue;
            si++;
            var beats = scene.TryGetValue("story_beats", out var sb) && sb is List<object?> bl ? bl : new();
            var bi = 0;
            foreach (var bItem in beats)
            {
                if (bItem is not Dictionary<string, object?> beat) continue;
                bi++;
                var ve = beat.TryGetValue("visual_event", out var v) ? v?.ToString()?.Trim() ?? "" : "";
                if (ve.Length == 0) continue;
                list.Add(new Target { Id = $"s{si}_b{bi}", VisualEvent = ve, Beat = beat });
            }
        }
        return list;
    }

    private async Task BackoffAsync(int attempt, CancellationToken ct)
    {
        var baseMs = Math.Max(0, _opts.SilentBeatClassifyBackoffBaseMs);
        if (baseMs == 0) return;
        await Task.Delay(Math.Min(4000, baseMs * attempt * attempt), ct).ConfigureAwait(false);
    }

    private static string NormalizeList(string s) =>
        string.Join(", ", Tokens(s).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static string StripFences(string raw)
    {
        raw = (raw ?? "").Trim();
        if (!raw.StartsWith("```")) return raw;
        raw = Regex.Replace(raw, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        return Regex.Replace(raw, @"\s*```\s*$", "");
    }

    private static string Trunc(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";
    private static string Trim(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";

    private sealed class Target
    {
        public required string Id { get; init; }
        public string VisualEvent { get; init; } = "";
        public required Dictionary<string, object?> Beat { get; init; }
        public string HeuristicAmbient { get; set; } = "";
        public string HeuristicSfx { get; set; } = "";
    }
}

public sealed class AmbientSfxClassifyResult
{
    public bool Enabled { get; set; }
    public string PromptVersion { get; set; } = "";
    public string Model { get; set; } = "";
    public double Temperature { get; set; }
    public int BeatCount { get; set; }
    public int AiCount { get; set; }
    public int FallbackCount { get; set; }
    public int Attempts { get; set; }
    public int ChatCalls { get; set; }
    public string Note { get; set; } = "";
    public string? LastError { get; set; }

    public Dictionary<string, object?> ToMetaDict() => new()
    {
        ["enabled"] = Enabled,
        ["prompt_version"] = PromptVersion,
        ["model"] = Model,
        ["beats"] = BeatCount,
        ["ai_labels"] = AiCount,
        ["heuristic_fallback"] = FallbackCount,
        ["chat_calls"] = ChatCalls,
        ["note"] = Note,
    };
}
