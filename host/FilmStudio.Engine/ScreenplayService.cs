using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FilmStudio.Core.Models;

namespace FilmStudio.Engine;

/// <summary>
/// Fountain draft lifecycle: load/save, create from book/import, sign-off → Stage 1.
/// Canonical file: source/screenplay.fountain (+ source/screenplay_meta.json).
/// </summary>
public static class ScreenplayService
{
    public const string CanonicalFileName = "screenplay.fountain";
    public const string MetaFileName = "screenplay_meta.json";

    public sealed class ScreenplayDoc
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public string Text { get; init; } = "";
        public ScreenplayStatus Status { get; init; } = new();
    }

    public sealed class SaveResult
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public ScreenplayStatus Status { get; init; } = new();
        public string? Message { get; set; }
    }

    public sealed class SignOffResult
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public string? Title { get; init; }
        public int SceneCount { get; init; }
        public int CharacterCount { get; init; }
        public int LocationCount { get; init; }
        public bool HashChanged { get; init; }
        public ScreenplayStatus Status { get; init; } = new();
        public string? Message { get; init; }
    }

    private sealed class MetaDto
    {
        public string? SignedHash { get; set; }
        public string? SignedAt { get; set; }
        public string? LastSavedHash { get; set; }
        public string? LastSavedAt { get; set; }
    }

    public static string GetDraftPath(ProjectStore store, string projectId) =>
        Path.Combine(store.GetProjectDir(projectId), "source", CanonicalFileName);

    public static string GetMetaPath(ProjectStore store, string projectId) =>
        Path.Combine(store.GetProjectDir(projectId), "source", MetaFileName);

    public static string ComputeHash(string text)
    {
        var normalized = NormalizeText(text);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NormalizeText(string text)
    {
        text ??= "";
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (text.Length > 0 && !text.EndsWith('\n'))
            text += "\n";
        return text;
    }

    /// <summary>Read Fountain draft + sign-off status. Pass Stage1 from GetAdaptationStatus to avoid re-reading.</summary>
    public static ScreenplayStatus ReadStatus(ProjectStore store, string projectId, Stage1Status stage1)
    {
        // Surface imported .fountain files that never got the canonical name
        try { EnsureCanonicalDraft(store, projectId); } catch { /* status still useful */ }

        var draftPath = GetDraftPath(store, projectId);
        var meta = ReadMeta(store, projectId);
        var status = new ScreenplayStatus();

        if (File.Exists(draftPath))
        {
            var text = File.ReadAllText(draftPath);
            var hash = ComputeHash(text);
            var fi = new FileInfo(draftPath);
            status.DraftExists = true;
            status.DraftBytes = fi.Length;
            status.DraftHash = hash;
            status.DraftMtime = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
            var parsed = FountainParser.Parse(text);
            status.SceneHeadingCount = parsed.Elements.Count(e => e.Type == FountainParser.ElementType.SceneHeading);
            if (parsed.TitlePage.TryGetValue("Title", out var t) && !string.IsNullOrWhiteSpace(t))
                status.Title = t.Replace("\n", " ").Trim();
            else if (parsed.TitlePage.TryGetValue("title", out t) && !string.IsNullOrWhiteSpace(t))
                status.Title = t.Replace("\n", " ").Trim();
        }

        status.SignedHash = meta.SignedHash;
        status.SignedAt = meta.SignedAt;

        if (status.DraftExists)
        {
            status.Signed = !string.IsNullOrEmpty(meta.SignedHash) &&
                            string.Equals(meta.SignedHash, status.DraftHash, StringComparison.OrdinalIgnoreCase);
            status.Dirty = !status.Signed;
        }
        else
        {
            // Legacy: Stage 1 without a Fountain draft — treat as ready (no draft to re-sign)
            status.Signed = stage1.Present && stage1.SceneCount > 0;
            status.Dirty = false;
        }

        status.ReadyForShots = stage1.Present && stage1.SceneCount > 0 &&
                               (status.Signed || !status.DraftExists);

        return status;
    }

    public static ScreenplayDoc Get(ProjectStore store, string projectId)
    {
        // Prefer canonical draft; if missing, adopt any source/*.fountain from import
        EnsureCanonicalDraft(store, projectId);
        var draftPath = GetDraftPath(store, projectId);
        var stage1 = ReadStage1Lite(store, projectId);
        var status = ReadStatus(store, projectId, stage1);
        var text = File.Exists(draftPath) ? File.ReadAllText(draftPath) : "";
        return new ScreenplayDoc
        {
            Ok = true,
            Text = text,
            Status = status,
        };
    }

    /// <summary>
    /// If screenplay.fountain is missing, copy the newest source/*.fountain (or project root *.fountain)
    /// into the canonical path so the editor has something to load after import.
    /// </summary>
    public static bool EnsureCanonicalDraft(ProjectStore store, string projectId)
    {
        var draftPath = GetDraftPath(store, projectId);
        if (File.Exists(draftPath) && new FileInfo(draftPath).Length > 0)
            return false;

        var projectDir = store.GetProjectDir(projectId);
        var sourceDir = Path.Combine(projectDir, "source");
        Directory.CreateDirectory(sourceDir);

        string? best = null;
        DateTime bestTime = DateTime.MinValue;
        void Consider(string path)
        {
            if (!File.Exists(path)) return;
            if (Path.GetFileName(path).Equals(CanonicalFileName, StringComparison.OrdinalIgnoreCase))
                return;
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0) return;
                if (fi.LastWriteTimeUtc >= bestTime)
                {
                    bestTime = fi.LastWriteTimeUtc;
                    best = path;
                }
            }
            catch { /* ignore */ }
        }

        if (Directory.Exists(sourceDir))
        {
            foreach (var f in Directory.EnumerateFiles(sourceDir, "*.fountain"))
                Consider(f);
            foreach (var f in Directory.EnumerateFiles(sourceDir, "*.spmd"))
                Consider(f);
        }
        foreach (var f in Directory.EnumerateFiles(projectDir, "*.fountain"))
            Consider(f);

        if (best is null)
            return false;

        var text = File.ReadAllText(best);
        File.WriteAllText(draftPath, NormalizeText(text));
        var meta = ReadMeta(store, projectId);
        meta.LastSavedHash = ComputeHash(text);
        meta.LastSavedAt = DateTime.UtcNow.ToString("o");
        // If Stage 1 already exists from a prior import, treat as signed so shot plan stays available
        var stage1 = ReadStage1Lite(store, projectId);
        if (stage1.Present && stage1.SceneCount > 0 && string.IsNullOrEmpty(meta.SignedHash))
        {
            meta.SignedHash = meta.LastSavedHash;
            meta.SignedAt = meta.LastSavedAt;
        }
        WriteMeta(store, projectId, meta);
        return true;
    }

    public static SaveResult SaveDraft(ProjectStore store, string projectId, string text)
    {
        text = NormalizeText(text ?? "");
        var sourceDir = Path.Combine(store.GetProjectDir(projectId), "source");
        Directory.CreateDirectory(sourceDir);
        var draftPath = GetDraftPath(store, projectId);
        File.WriteAllText(draftPath, text);

        var hash = ComputeHash(text);
        var meta = ReadMeta(store, projectId);
        meta.LastSavedHash = hash;
        meta.LastSavedAt = DateTime.UtcNow.ToString("o");
        WriteMeta(store, projectId, meta);

        var stage1 = ReadStage1Lite(store, projectId);
        var status = ReadStatus(store, projectId, stage1);
        return new SaveResult
        {
            Ok = true,
            Status = status,
            Message = status.Dirty
                ? "Draft saved — approve when ready"
                : "Draft saved",
        };
    }

    /// <summary>Import Fountain text as the editable draft (does not materialise Stage 1).</summary>
    public static SaveResult ImportAsDraft(
        ProjectStore store,
        string projectId,
        string text,
        string? originalFileName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SaveResult { Ok = false, Error = "Empty screenplay text" };

        var result = SaveDraft(store, projectId, text);

        // Keep a copy under the original name for reference when different
        if (!string.IsNullOrWhiteSpace(originalFileName))
        {
            var safe = Path.GetFileName(originalFileName);
            if (!string.IsNullOrWhiteSpace(safe) &&
                !safe.Equals(CanonicalFileName, StringComparison.OrdinalIgnoreCase))
            {
                if (!safe.EndsWith(".fountain", StringComparison.OrdinalIgnoreCase) &&
                    !safe.EndsWith(".spmd", StringComparison.OrdinalIgnoreCase))
                    safe = Path.GetFileNameWithoutExtension(safe) + ".fountain";
                var copyPath = Path.Combine(store.GetProjectDir(projectId), "source", safe);
                try { File.WriteAllText(copyPath, NormalizeText(text)); } catch { /* ignore */ }
            }
        }

        result.Message = "Screenplay draft ready — review and approve on Screenplay";
        return result;
    }

    /// <summary>
    /// Build a first-pass Fountain draft from prepared book text (or raw TXT).
    /// Does not sign off.
    /// </summary>
    public static SaveResult CreateDraftFromBook(ProjectStore store, string projectId)
    {
        var projectDir = store.GetProjectDir(projectId);
        var bookPath = Path.Combine(projectDir, "source", "book_full.txt");
        if (!File.Exists(bookPath))
            return new SaveResult { Ok = false, Error = "No prepared book text yet" };

        var book = File.ReadAllText(bookPath);
        if (string.IsNullOrWhiteSpace(book))
            return new SaveResult { Ok = false, Error = "Book text is empty" };

        var title = projectId;
        try
        {
            var pj = Path.Combine(projectDir, "project.json");
            if (File.Exists(pj))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pj));
                if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                    title = t.GetString() ?? title;
                else if (doc.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    title = n.GetString() ?? title;
            }
        }
        catch { /* ignore */ }

        var fountain = BookTextToFountainDraft(title, book);
        var save = SaveDraft(store, projectId, fountain);
        if (!save.Ok) return save;
        save.Message = "Draft from book ready — edit and approve on Screenplay";
        return save;
    }

    public static SignOffResult SignOff(ProjectStore store, string projectId, string? text = null)
    {
        // Optional body text: save first
        if (text is not null)
        {
            var save = SaveDraft(store, projectId, text);
            if (!save.Ok)
                return new SignOffResult { Ok = false, Error = save.Error };
        }

        var draftPath = GetDraftPath(store, projectId);
        if (!File.Exists(draftPath))
            return new SignOffResult { Ok = false, Error = "No screenplay draft to approve" };

        var draftText = File.ReadAllText(draftPath);
        if (string.IsNullOrWhiteSpace(draftText))
            return new SignOffResult { Ok = false, Error = "Screenplay draft is empty" };

        var hash = ComputeHash(draftText);
        var metaBefore = ReadMeta(store, projectId);
        var hashChanged = string.IsNullOrEmpty(metaBefore.SignedHash) ||
                          !string.Equals(metaBefore.SignedHash, hash, StringComparison.OrdinalIgnoreCase);

        var import = FountainStage1Importer.ImportToProject(store, projectId, draftText, CanonicalFileName);
        if (!import.Ok)
            return new SignOffResult { Ok = false, Error = import.Error ?? "Could not build screenplay" };

        // Ensure canonical path (importer may write same name)
        if (!string.Equals(Path.GetFullPath(import.FountainSavedPath ?? ""), Path.GetFullPath(draftPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(draftPath, NormalizeText(draftText));
        }

        var meta = ReadMeta(store, projectId);
        meta.SignedHash = hash;
        meta.SignedAt = DateTime.UtcNow.ToString("o");
        meta.LastSavedHash = hash;
        meta.LastSavedAt = meta.SignedAt;
        WriteMeta(store, projectId, meta);

        var stage1 = ReadStage1Lite(store, projectId);
        var status = ReadStatus(store, projectId, stage1);

        return new SignOffResult
        {
            Ok = true,
            Title = import.Title,
            SceneCount = import.SceneCount,
            CharacterCount = import.CharacterCount,
            LocationCount = import.LocationCount,
            HashChanged = hashChanged,
            Status = status,
            Message =
                $"Screenplay approved · {import.SceneCount} scenes · {import.CharacterCount} cast" +
                (hashChanged ? " · update shot plan if you already built one" : ""),
        };
    }

    /// <summary>Turn plain book prose into a minimal editable Fountain draft.</summary>
    public static string BookTextToFountainDraft(string title, string bookText)
    {
        var sb = new StringBuilder();
        sb.Append("Title: ").Append(title.Trim()).Append('\n');
        sb.Append("Draft date: ").Append(DateTime.Now.ToString("M/d/yyyy")).Append('\n');
        sb.Append('\n');
        sb.Append("INT. STORY - DAY\n\n");

        // Collapse runs of blank lines; keep paragraphs as Action
        var lines = bookText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var para = new StringBuilder();
        void FlushPara()
        {
            var p = para.ToString().Trim();
            para.Clear();
            if (p.Length == 0) return;
            // Soft wrap long lines at ~100 chars for readability
            foreach (var chunk in WrapWords(p, 100))
                sb.Append(chunk).Append('\n');
            sb.Append('\n');
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushPara();
                continue;
            }
            if (para.Length > 0) para.Append(' ');
            para.Append(line.Trim());
        }
        FlushPara();

        // Cap extremely large books in the draft (user can still expand)
        const int maxChars = 400_000;
        var result = sb.ToString();
        if (result.Length > maxChars)
        {
            result = result[..maxChars] + "\n\n[[Draft truncated — paste remaining book text as needed.]]\n";
        }
        return NormalizeText(result);
    }

    private static IEnumerable<string> WrapWords(string text, int width)
    {
        if (text.Length <= width) { yield return text; yield break; }
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var w in words)
        {
            if (line.Length == 0)
            {
                line.Append(w);
                continue;
            }
            if (line.Length + 1 + w.Length > width)
            {
                yield return line.ToString();
                line.Clear();
                line.Append(w);
            }
            else
            {
                line.Append(' ').Append(w);
            }
        }
        if (line.Length > 0) yield return line.ToString();
    }

    private static MetaDto ReadMeta(ProjectStore store, string projectId)
    {
        var path = GetMetaPath(store, projectId);
        if (!File.Exists(path)) return new MetaDto();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MetaDto>(json, JsonDefaults.CaseInsensitive) ?? new MetaDto();
        }
        catch
        {
            return new MetaDto();
        }
    }

    private static void WriteMeta(ProjectStore store, string projectId, MetaDto meta)
    {
        var path = GetMetaPath(store, projectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(meta, JsonDefaults.Indented);
        File.WriteAllText(path, json + "\n");
    }

    /// <summary>Lightweight Stage1 presence without full adaptation graph (avoids recursion).</summary>
    private static Stage1Status ReadStage1Lite(ProjectStore store, string projectId)
    {
        var path = store.ResolveScenesJsonPath(projectId);
        if (!File.Exists(path))
            return new Stage1Status { Present = false };
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var scenes = root.TryGetProperty("scenes", out var s) && s.ValueKind == JsonValueKind.Array
                ? s.GetArrayLength()
                : 0;
            var title = root.TryGetProperty("movie_title", out var t) ? t.GetString() : null;
            return new Stage1Status
            {
                Present = scenes > 0,
                SceneCount = scenes,
                MovieTitle = title,
                ScenesFile = path,
            };
        }
        catch
        {
            return new Stage1Status { Present = File.Exists(path) };
        }
    }
}
