using Microsoft.Extensions.Options;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class StudioPathTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;

    public StudioPathTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm-studio-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        _store = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _root }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void Normalize_defaults_to_full()
    {
        Assert.Equal(ProjectStudioPaths.Full, ProjectStudioPaths.Normalize(null));
        Assert.Equal(ProjectStudioPaths.Full, ProjectStudioPaths.Normalize(""));
        Assert.Equal(ProjectStudioPaths.Full, ProjectStudioPaths.Normalize("weird"));
        Assert.Equal(ProjectStudioPaths.SimpleVoice, ProjectStudioPaths.Normalize("simple-voice"));
        Assert.True(ProjectStudioPaths.IsSimpleVoice("SIMPLE-VOICE"));
    }

    [Fact]
    public async Task Create_and_set_studio_path_persists_on_project_json()
    {
        var p = await _store.CreateProjectAsync(
            "VoiceBook", title: "Voice Book", studioPath: ProjectStudioPaths.SimpleVoice);
        Assert.Equal(ProjectStudioPaths.SimpleVoice, p.StudioPath);

        var reloaded = await _store.GetProjectAsync(p.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(ProjectStudioPaths.SimpleVoice, reloaded!.StudioPath);

        var meta = await File.ReadAllTextAsync(Path.Combine(p.Path, "project.json"));
        Assert.Contains("simple-voice", meta, StringComparison.OrdinalIgnoreCase);

        var full = await _store.SetProjectStudioPathAsync(p.Id, ProjectStudioPaths.Full);
        Assert.Equal(ProjectStudioPaths.Full, full.StudioPath);
        reloaded = await _store.GetProjectAsync(p.Id);
        Assert.Equal(ProjectStudioPaths.Full, reloaded!.StudioPath);
    }
}
