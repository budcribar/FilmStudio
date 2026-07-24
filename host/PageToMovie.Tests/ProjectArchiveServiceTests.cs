using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectArchiveServiceTests
{
    [Fact]
    public async Task Export_then_import_round_trips_project_files()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "ptm-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = tmp });
            var store = new ProjectStore(opts);
            var archives = new ProjectArchiveService(store, NullLogger<ProjectArchiveService>.Instance);

            var created = await store.CreateProjectAsync("DebugRoundTrip");
            var dir = store.GetProjectDir(created.Id!);
            await File.WriteAllTextAsync(Path.Combine(dir, "source", "screenplay.fountain"), "Title: Test\n\nINT. ROOM - DAY\n\nHello.\n");
            await File.WriteAllTextAsync(Path.Combine(dir, "source", "cast_seeds.json"), "{\"schema_version\":\"cast_seeds.v1\",\"character_seed_tokens\":{}}\n");
            Directory.CreateDirectory(Path.Combine(dir, "source", "book_images"));
            await File.WriteAllBytesAsync(Path.Combine(dir, "source", "book_images", "page_001_render.png"), new byte[] { 1, 2, 3, 4 });

            await using var exp = await archives.ExportAsync(created.Id!);
            Assert.True(exp.ByteLength > 0);
            Assert.EndsWith(".zip", exp.FileName, StringComparison.OrdinalIgnoreCase);

            // Import under a new id
            var imported = await archives.ImportAsync(exp.Stream, preferredId: "DebugRoundTrip_Copy", overwrite: false);
            Assert.True(imported.Ok);
            Assert.Equal("DebugRoundTrip_Copy", imported.ProjectId);

            var copyDir = store.GetProjectDir("DebugRoundTrip_Copy");
            Assert.True(File.Exists(Path.Combine(copyDir, "project.json")));
            Assert.True(File.Exists(Path.Combine(copyDir, "source", "screenplay.fountain")));
            Assert.Equal(
                "Title: Test\n\nINT. ROOM - DAY\n\nHello.\n",
                await File.ReadAllTextAsync(Path.Combine(copyDir, "source", "screenplay.fountain")));
            Assert.True(File.Exists(Path.Combine(copyDir, "source", "book_images", "page_001_render.png")));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Import_flat_zip_with_project_json_at_root()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "ptm-archive-flat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var zipPath = Path.Combine(tmp, "flat.zip");
        try
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var e = zip.CreateEntry("project.json");
                await using (var w = new StreamWriter(e.Open()))
                    await w.WriteAsync("{\"id\":\"FlatImport\",\"title\":\"Flat\"}\n");
                var s = zip.CreateEntry("source/book_full.txt");
                await using (var w = new StreamWriter(s.Open()))
                    await w.WriteAsync("Once upon a time.\n");
            }

            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = tmp });
            var store = new ProjectStore(opts);
            var archives = new ProjectArchiveService(store, NullLogger<ProjectArchiveService>.Instance);

            await using var fs = File.OpenRead(zipPath);
            var imported = await archives.ImportAsync(fs, preferredId: null, overwrite: false);
            Assert.True(imported.Ok);
            Assert.Equal("FlatImport", imported.ProjectId);
            Assert.True(File.Exists(Path.Combine(store.GetProjectDir("FlatImport"), "source", "book_full.txt")));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* ignore */ }
        }
    }
}
