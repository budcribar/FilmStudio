using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectIdIntegrityTests
{
    private static string NewTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDir(string dir)
    {
        try { Directory.Delete(dir, true); } catch { }
    }

    /// <summary>
    /// Reproduces exactly the scenario a copied/renamed project folder produces: project.json's own
    /// "id" field still names the OLD folder it was copied from. ListProjectsAsync must report the
    /// id derived from the actual folder path, not the stale embedded one — otherwise
    /// GetProjectDir(reportedId) round-trips to a completely different (the old, unrelated) folder
    /// on disk instead of the one that was actually scanned.
    /// </summary>
    [Fact]
    public async Task ListProjectsAsync_prefers_folder_derived_id_over_stale_project_json_id()
    {
        var root = NewTempDir("ptm_project_id_integrity");
        try
        {
            var renamedDir = Path.Combine(root, "projects", "someowner", "BookV8");
            Directory.CreateDirectory(renamedDir);
            await File.WriteAllTextAsync(Path.Combine(renamedDir, "project.json"), """
                {"id":"someowner/BookV7","title":"BookV7"}
                """);

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
            var store = new ProjectStore(opts);

            var list = await store.ListProjectsAsync();
            var info = Assert.Single(list);
            Assert.Equal("someowner/BookV8", info.Id);

            // The reported id must actually resolve back to the folder it came from.
            var resolvedDir = store.GetProjectDir(info.Id);
            Assert.Equal(renamedDir, resolvedDir);
        }
        finally
        {
            DeleteDir(root);
        }
    }
}
