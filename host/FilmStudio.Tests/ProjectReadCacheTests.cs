using FilmStudio.Core.Models;
using FilmStudio.Engine;
using Xunit;

namespace FilmStudio.Tests;

public class ProjectReadCacheTests
{
    [Fact]
    public void Projects_list_cached_until_invalidate()
    {
        var cache = new ProjectReadCache();
        var builds = 0;
        IReadOnlyList<ProjectInfo> Build()
        {
            builds++;
            return new[]
            {
                new ProjectInfo { Id = "A", Label = "A", Path = "/tmp/A" },
            };
        }

        var a = cache.GetOrBuildProjects(Build);
        var b = cache.GetOrBuildProjects(Build);
        Assert.Equal(1, builds);
        Assert.Equal("A", a[0].Id);
        Assert.Equal("A", b[0].Id);

        // Caller mutation must not poison cache
        ((List<ProjectInfo>)a)[0].Id = "mutated";
        var c = cache.GetOrBuildProjects(Build);
        Assert.Equal("A", c[0].Id);
        Assert.Equal(1, builds);

        cache.InvalidateProjectsList();
        _ = cache.GetOrBuildProjects(Build);
        Assert.Equal(2, builds);
    }

    [Fact]
    public void Blueprint_document_cached_by_mtime_shared()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-read-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "blueprint.json");
            File.WriteAllText(path, """{"scenes":[]}""");
            var cache = new ProjectReadCache();
            var a = cache.GetOrLoadBlueprintDocument(path);
            var b = cache.GetOrLoadBlueprintDocument(path);
            Assert.NotNull(a);
            Assert.Same(a, b); // shared instance — do not dispose
            Assert.Same(cache.GetOrLoadBlueprintUtf8(path), cache.GetOrLoadBlueprintUtf8(path));

            Thread.Sleep(20);
            File.WriteAllText(path, """{"scenes":[{"scene_number":1}]}""");
            var c = cache.GetOrLoadBlueprintDocument(path);
            Assert.NotNull(c);
            Assert.NotSame(a, c);
            Assert.True(c!.RootElement.GetProperty("scenes").GetArrayLength() == 1);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Dir_index_cached_until_dir_mtime_or_invalidate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-dir-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.mp4"), new string('x', 2048));
            var cache = new ProjectReadCache();
            var builds = 0;
            Dictionary<string, long> Index(string d)
            {
                builds++;
                var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in Directory.EnumerateFiles(d))
                    map[Path.GetFileName(f)] = new FileInfo(f).Length;
                return map;
            }

            var a = cache.GetOrIndexDir(dir, Index);
            var b = cache.GetOrIndexDir(dir, Index);
            Assert.Equal(1, builds);
            Assert.True(a.ContainsKey("a.mp4"));
            Assert.True(b.ContainsKey("a.mp4"));

            cache.InvalidateProject("P", dir);
            _ = cache.GetOrIndexDir(dir, Index);
            Assert.Equal(2, builds);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Blueprint_path_cached_per_project()
    {
        var cache = new ProjectReadCache();
        var finds = 0;
        string? Find()
        {
            finds++;
            return "/proj/blueprint.json";
        }

        Assert.Equal("/proj/blueprint.json", cache.GetOrFindBlueprintPath("Buster", Find));
        Assert.Equal("/proj/blueprint.json", cache.GetOrFindBlueprintPath("Buster", Find));
        Assert.Equal(1, finds);

        cache.InvalidateProject("Buster", "/proj");
        Assert.Equal("/proj/blueprint.json", cache.GetOrFindBlueprintPath("Buster", Find));
        Assert.Equal(2, finds);
    }

    [Fact]
    public void Disabled_always_rebuilds_projects()
    {
        var cache = new ProjectReadCache { Enabled = false };
        var builds = 0;
        IReadOnlyList<ProjectInfo> Build()
        {
            builds++;
            return new[] { new ProjectInfo { Id = "A", Path = "/a" } };
        }

        _ = cache.GetOrBuildProjects(Build);
        _ = cache.GetOrBuildProjects(Build);
        Assert.Equal(2, builds);
    }
}
