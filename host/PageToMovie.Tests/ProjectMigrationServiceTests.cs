using System.Text.Json;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectMigrationServiceTests : IDisposable
{
    private readonly string _tempWorkspace;

    public ProjectMigrationServiceTests()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ptm-migration-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempWorkspace)) Directory.Delete(_tempWorkspace, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public async Task MigrateIfNeededAsync_upgrades_v0_project_to_v1_and_updates_schema_version()
    {
        var projects = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var sidecars = new ClipSidecarService(projects);
        var migration = new ProjectMigrationService(sidecars);

        var projectDir = Path.Combine(_tempWorkspace, "projects", "UnversionedMovie");
        var videoDir = Path.Combine(projectDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        // Create legacy v0 project.json without schema_version
        var projectJson = Path.Combine(projectDir, "project.json");
        await File.WriteAllTextAsync(projectJson, "{\"id\":\"UnversionedMovie\",\"title\":\"Unversioned Movie\"}");

        // Create legacy MP4 clip
        var legacyMp4 = Path.Combine(videoDir, "scene_01_clip_02.mp4");
        await File.WriteAllBytesAsync(legacyMp4, new byte[512]);

        var migrated = await migration.MigrateIfNeededAsync(projectDir);
        Assert.True(migrated);

        // Check project.json updated to schema v1
        var text = await File.ReadAllTextAsync(projectJson);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        Assert.Equal("v1", root.GetProperty("schema_version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("migrated_at_utc").GetString()));

        // Check clip sidecar created
        var sidecarsList = Directory.GetFiles(videoDir, "*.clip.json");
        Assert.NotEmpty(sidecarsList);
    }

    [Fact]
    public async Task MigrateIfNeededAsync_is_noop_for_already_v1_project()
    {
        var projects = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var sidecars = new ClipSidecarService(projects);
        var migration = new ProjectMigrationService(sidecars);

        var projectDir = Path.Combine(_tempWorkspace, "projects", "VersionedMovie");
        Directory.CreateDirectory(projectDir);

        var projectJson = Path.Combine(projectDir, "project.json");
        await File.WriteAllTextAsync(projectJson, "{\"id\":\"VersionedMovie\",\"schema_version\":\"v1\"}");

        var migrated = await migration.MigrateIfNeededAsync(projectDir);
        Assert.False(migrated);
    }
}
