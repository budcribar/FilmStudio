using System.Text.Json;
using FilmStudio.Core.Models;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly FilmStudioOptions _opts;
    private string _activeProjectId = "";

    public ProjectStore(IOptions<FilmStudioOptions> opts)
    {
        _opts = opts.Value;
        var root = ResolveWorkspaceRoot();
        var ws = Path.Combine(root, "projects", "workspace.json");
        if (File.Exists(ws))
        {
            try
            {
                var state = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(ws), JsonOpts);
                _activeProjectId = state?.ActiveProject ?? "";
            }
            catch { /* ignore */ }
        }
    }

    public string WorkspaceRoot => ResolveWorkspaceRoot();

    public string ActiveProjectId =>
        string.IsNullOrWhiteSpace(_activeProjectId)
            ? ListProjects().FirstOrDefault()?.Id ?? ""
            : _activeProjectId;

    public IReadOnlyList<ProjectInfo> ListProjects()
    {
        var projectsDir = Path.Combine(WorkspaceRoot, "projects");
        if (!Directory.Exists(projectsDir))
            return Array.Empty<ProjectInfo>();

        var list = new List<ProjectInfo>();
        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var id = Path.GetFileName(dir);
            if (string.Equals(id, "workspace.json", StringComparison.OrdinalIgnoreCase))
                continue;
            var metaPath = Path.Combine(dir, "project.json");
            string? title = null;
            string? label = null;
            if (File.Exists(metaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    if (doc.RootElement.TryGetProperty("title", out var t))
                        title = t.GetString();
                    if (doc.RootElement.TryGetProperty("label", out var l))
                        label = l.GetString();
                }
                catch { /* ignore */ }
            }
            list.Add(new ProjectInfo
            {
                Id = id,
                Title = title,
                Label = label ?? title ?? id,
                Path = dir,
            });
        }
        return list.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ProjectInfo? GetProject(string projectId)
    {
        return ListProjects().FirstOrDefault(p =>
            string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase));
    }

    public ProjectInfo Activate(string projectId)
    {
        var p = GetProject(projectId)
            ?? throw new InvalidOperationException($"Unknown project: {projectId}");
        _activeProjectId = p.Id;
        var wsPath = Path.Combine(WorkspaceRoot, "projects", "workspace.json");
        Directory.CreateDirectory(Path.GetDirectoryName(wsPath)!);
        File.WriteAllText(
            wsPath,
            JsonSerializer.Serialize(new WorkspaceState { ActiveProject = p.Id }, JsonOpts));
        return p;
    }

    public string GetProjectDir(string projectId)
    {
        var p = GetProject(projectId)
            ?? throw new InvalidOperationException($"Unknown project: {projectId}");
        return p.Path;
    }

    public string? FindBlueprintPath(string projectId)
    {
        var dir = GetProjectDir(projectId);
        var configPath = Path.Combine(dir, "pipeline_config.json");
        var name = "blueprint.clips.grok.json";
        if (File.Exists(configPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("blueprint_file", out var bf))
                {
                    var n = bf.GetString();
                    if (!string.IsNullOrWhiteSpace(n))
                        name = n;
                }
            }
            catch { /* ignore */ }
        }
        foreach (var candidate in new[]
                 {
                     name,
                     "blueprint.clips.grok.json",
                     "nickandme.clips.grok.json",
                 })
        {
            var full = Path.Combine(dir, candidate);
            if (File.Exists(full))
                return full;
        }
        return null;
    }

    public JsonDocument? LoadBlueprint(string projectId)
    {
        var path = FindBlueprintPath(projectId);
        if (path is null)
            return null;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private string ResolveWorkspaceRoot()
    {
        if (!string.IsNullOrWhiteSpace(_opts.WorkspaceRoot) &&
            Directory.Exists(_opts.WorkspaceRoot))
        {
            return Path.GetFullPath(_opts.WorkspaceRoot);
        }

        // host/FilmStudio.Engine → host → repo
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "projects")) &&
                Directory.Exists(Path.Combine(dir.FullName, "renderer")))
            {
                return dir.FullName;
            }
            // running from host/FilmStudio.Api/bin/...
            if (dir.Name.Equals("host", StringComparison.OrdinalIgnoreCase) &&
                dir.Parent is not null &&
                Directory.Exists(Path.Combine(dir.Parent.FullName, "projects")))
            {
                return dir.Parent.FullName;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
