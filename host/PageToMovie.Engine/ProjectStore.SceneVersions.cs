using System.Security.Cryptography;
using System.Text.Json;

namespace PageToMovie.Engine;

public sealed partial class ProjectStore
{
    internal static string SanitizeProjectIdForPath(string projectId) =>
        (projectId ?? "").Replace('\\', '_').Replace('/', '__').Replace(':', '_').Trim();

    string GetSceneVersionsRoot() => SceneVersionsRootPath;

    string SceneVersionPrefix(string projectId, int sceneNumber) =>
        $"{SanitizeProjectIdForPath(projectId)}__scene{sceneNumber:D3}_";

    public Task<bool> SoftDeleteSceneAsync(string projectId, int sceneNumber, CancellationToken ct = default)
    {
        var dir = GetProjectDir(projectId);
        if (string.IsNullOrEmpty(dir)) return Task.FromResult(false);
        var sceneDir = Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}");
        if (!Directory.Exists(sceneDir))
            sceneDir = Path.Combine(dir, "scenes", sceneNumber.ToString());
        if (!Directory.Exists(sceneDir)) return Task.FromResult(false);
        var tomb = sceneDir + ".deleted";
        try
        {
            if (Directory.Exists(tomb)) Directory.Delete(tomb, true);
            Directory.Move(sceneDir, tomb);
            return Task.FromResult(true);
        }
        catch { return Task.FromResult(false); }
    }

    public async Task<bool> UpdateClipPromptAsync(string projectId, int sceneNumber, int clipNumber, string prompt, CancellationToken ct = default)
    {
        var dir = GetProjectDir(projectId);
        if (string.IsNullOrEmpty(dir)) return false;
        var candidates = new[]
        {
            Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}", "shot-plan.json"),
            Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}", "scene-plan.json"),
            Path.Combine(dir, "shot-plan.json"),
            Path.Combine(dir, "scene-plan.json"),
        };
        var planPath = candidates.FirstOrDefault(File.Exists);
        if (planPath is null)
        {
            var sceneDir = Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}");
            Directory.CreateDirectory(sceneDir);
            planPath = Path.Combine(sceneDir, "shot-plan.json");
            await File.WriteAllTextAsync(planPath, "{\"sceneNumber\":" + sceneNumber + ",\"clips\":[]}", ct).ConfigureAwait(false);
        }
        try
        {
            await File.WriteAllTextAsync(planPath + ".clip" + clipNumber + ".prompt.txt", prompt ?? "", ct).ConfigureAwait(false);
            var json = await File.ReadAllTextAsync(planPath, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(planPath, json + "\n/* clip " + clipNumber + " @" + DateTime.UtcNow.ToString("o") + " */\n", ct).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Snapshot scene plan under scene-versions/ (path-safe for owner/name ids).</summary>
    public async Task<string?> SnapshotSceneVersionAsync(string projectId, int sceneNumber, string? message = null, CancellationToken ct = default)
    {
        var dir = GetProjectDir(projectId);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

        var candidates = new[]
        {
            Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}", "shot-plan.json"),
            Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}", "scene-plan.json"),
            Path.Combine(dir, "scenes", sceneNumber.ToString(), "shot-plan.json"),
            Path.Combine(dir, "shot-plan.json"),
            Path.Combine(dir, "scene-plan.json"),
        };
        var planPath = candidates.FirstOrDefault(File.Exists);
        if (planPath is null) return null;

        var bytes = await File.ReadAllBytesAsync(planPath, ct).ConfigureAwait(false);
        var hash = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
        var versionsRoot = GetSceneVersionsRoot();
        Directory.CreateDirectory(versionsRoot);
        var dest = Path.Combine(versionsRoot, SceneVersionPrefix(projectId, sceneNumber) + hash);
        if (!Directory.Exists(dest))
        {
            Directory.CreateDirectory(dest);
            await File.WriteAllBytesAsync(Path.Combine(dest, "shot-plan.json"), bytes, ct).ConfigureAwait(false);
            var meta = new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["sceneNumber"] = sceneNumber,
                ["commitHash"] = hash,
                ["message"] = message ?? "Scene snapshot",
                ["utc"] = DateTime.UtcNow,
            };
            await using var fs = File.Create(Path.Combine(dest, "meta.json"));
            await JsonSerializer.SerializeAsync(fs, meta, cancellationToken: ct).ConfigureAwait(false);
        }
        return hash;
    }

    public Task<IReadOnlyList<SceneHistoryEntry>> GetSceneHistoryAsync(string projectId, int sceneNumber, CancellationToken ct = default)
    {
        var versionsRoot = GetSceneVersionsRoot();
        var list = new List<SceneHistoryEntry>();
        if (!Directory.Exists(versionsRoot))
            return Task.FromResult((IReadOnlyList<SceneHistoryEntry>)list);

        var prefix = SceneVersionPrefix(projectId, sceneNumber);
        foreach (var d in Directory.GetDirectories(versionsRoot))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(d);
            if (name is null || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var hash = name.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(hash)) continue;
            string? msg = null;
            var utc = Directory.GetCreationTimeUtc(d);
            var metaPath = Path.Combine(d, "meta.json");
            if (File.Exists(metaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    if (doc.RootElement.TryGetProperty("message", out var m)) msg = m.GetString();
                    if (doc.RootElement.TryGetProperty("utc", out var u) && u.TryGetDateTime(out var dt)) utc = dt;
                }
                catch { /* ignore */ }
            }
            list.Add(new SceneHistoryEntry(hash, msg ?? hash[..Math.Min(8, hash.Length)], utc));
        }
        return Task.FromResult((IReadOnlyList<SceneHistoryEntry>)list.OrderByDescending(e => e.Utc).ToList());
    }

    public async Task<string?> RevertSceneAsync(string projectId, int sceneNumber, string commitHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commitHash)) return null;
        commitHash = commitHash.Trim();
        var versionsRoot = GetSceneVersionsRoot();
        var prefix = SceneVersionPrefix(projectId, sceneNumber);
        var planSrc = Path.Combine(versionsRoot, prefix + commitHash, "shot-plan.json");
        if (!File.Exists(planSrc) && Directory.Exists(versionsRoot))
        {
            var match = Directory.GetDirectories(versionsRoot)
                .Select(Path.GetFileName)
                .FirstOrDefault(n => n is not null
                    && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && n.Length > prefix.Length
                    && n.Substring(prefix.Length).StartsWith(commitHash, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                planSrc = Path.Combine(versionsRoot, match, "shot-plan.json");
        }
        if (!File.Exists(planSrc)) return null;

        var dir = GetProjectDir(projectId);
        if (string.IsNullOrEmpty(dir)) return null;

        try { await SnapshotSceneVersionAsync(projectId, sceneNumber, "pre-revert snapshot", ct).ConfigureAwait(false); }
        catch { /* best-effort */ }

        var text = await File.ReadAllTextAsync(planSrc, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sceneDir = Path.Combine(dir, "scenes", $"scene_{sceneNumber:D3}");
        Directory.CreateDirectory(sceneDir);
        await File.WriteAllTextAsync(Path.Combine(sceneDir, "shot-plan.json"), text, ct).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(dir, "shot-plan.json"), text, ct).ConfigureAwait(false);
        return text;
    }
}

public sealed record SceneHistoryEntry(string CommitHash, string Message, DateTime Utc);
