using System.Text.Json;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;

namespace PageToMovie.Engine;

/// <summary>
/// Manages schema versioning for PageToMovie projects and automatically executes
/// sequential migration steps (e.g. v0 → v1 clip naming & sidecars) on import, export, and load.
/// </summary>
public sealed class ProjectMigrationService
{
    public const string CurrentSchemaVersion = "v1";

    private readonly ClipSidecarService _sidecars;
    private readonly ILogger<ProjectMigrationService> _log;

    public ProjectMigrationService(
        ClipSidecarService sidecars,
        ILogger<ProjectMigrationService>? log = null)
    {
        _sidecars = sidecars;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectMigrationService>.Instance;
    }

    /// <summary>
    /// Check project schema version and execute necessary migrations up to CurrentSchemaVersion.
    /// </summary>
    public async Task<bool> MigrateIfNeededAsync(string projectDir, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            return false;

        var projectJsonPath = Path.Combine(projectDir, "project.json");
        var currentVersion = "v0";
        Dictionary<string, object?>? projectDict = null;

        if (File.Exists(projectJsonPath))
        {
            try
            {
                var text = await File.ReadAllTextAsync(projectJsonPath, ct).ConfigureAwait(false);
                projectDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(text, JsonDefaults.IndentedCaseInsensitive);
                if (projectDict is not null && projectDict.TryGetValue("schema_version", out var vObj) && vObj is JsonElement el)
                {
                    currentVersion = el.GetString() ?? "v0";
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed reading project.json schema_version at {Path}", projectJsonPath);
            }
        }

        if (string.Equals(currentVersion, CurrentSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            // Already on latest version, still ensure missing sidecars exist
            await _sidecars.EnsureAllSidecarsExistAsync(projectDir, ct).ConfigureAwait(false);
            return false;
        }

        _log.LogInformation("Migrating project at {Dir} from schema {OldVersion} → {NewVersion}", projectDir, currentVersion, CurrentSchemaVersion);

        // Step 0 → 1: Convert clip naming convention and write .clip.json sidecars
        if (currentVersion is "v0" or "unversioned" or "")
        {
            await _sidecars.ConvertProjectClipsToNewFormatAsync(projectDir, ct).ConfigureAwait(false);
            currentVersion = "v1";
        }

        // Update project.json with new schema version
        projectDict ??= new Dictionary<string, object?>();
        projectDict["schema_version"] = CurrentSchemaVersion;
        projectDict["migrated_at_utc"] = DateTime.UtcNow.ToString("o");

        try
        {
            var updatedJson = JsonSerializer.Serialize(projectDict, JsonDefaults.IndentedCaseInsensitive);
            await File.WriteAllTextAsync(projectJsonPath, updatedJson + "\n", ct).ConfigureAwait(false);
            _log.LogInformation("Successfully updated project.json to schema {Version} for {Dir}", CurrentSchemaVersion, projectDir);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed updating project.json schema version at {Path}", projectJsonPath);
        }

        return true;
    }
}
