using PageToMovie.Engine.Collaboration;

namespace PageToMovie.Tests;

public class SceneVersionStoreTests : IDisposable
{
    private readonly string _root;
    private readonly SceneVersionStore _sut;

    public SceneVersionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm-scene-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sut = new SceneVersionStore(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    [Fact]
    public async Task Snapshot_then_list_returns_version()
    {
        var info = await _sut.SnapshotAsync("p1", "scene-1", """{"key":"scene-1","title":"A"}""", note: "first");
        Assert.False(string.IsNullOrWhiteSpace(info.VersionId));
        Assert.Equal("scene-1", info.SceneKey);
        Assert.Contains("scene-state.json", info.Files);

        var list = await _sut.ListHistoryAsync("p1", "scene-1");
        Assert.Single(list);
        Assert.Equal(info.VersionId, list[0].VersionId);
        Assert.Equal("first", list[0].Note);
    }

    [Fact]
    public async Task Restore_returns_scene_state()
    {
        var state = """{"key":"s2","title":"Before"}""";
        var info = await _sut.SnapshotAsync("p1", "s2", state);
        var result = await _sut.RestoreAsync("p1", "s2", info.VersionId);
        Assert.True(result.Ok);
        Assert.Equal(state, result.SceneStateJson);
    }

    [Fact]
    public async Task Snapshot_with_media_file_copies_it()
    {
        var media = Path.Combine(_root, "src-audio.wav");
        await File.WriteAllTextAsync(media, "fake-audio");
        var info = await _sut.SnapshotAsync(
            "p1", "s3", """{"key":"s3"}""",
            localMediaPaths: new Dictionary<string, string> { ["audio.wav"] = media });
        Assert.Contains("audio.wav", info.Files);

        var dest = Path.Combine(_root, "restored-audio.wav");
        var result = await _sut.RestoreAsync(
            "p1", "s3", info.VersionId,
            mediaDestinations: new Dictionary<string, string> { ["audio.wav"] = dest });
        Assert.True(result.Ok);
        Assert.True(File.Exists(dest));
        Assert.Equal("fake-audio", await File.ReadAllTextAsync(dest));
    }

    [Fact]
    public async Task List_orders_newest_first()
    {
        await _sut.SnapshotAsync("p1", "s4", """{"n":1}""", note: "old");
        await Task.Delay(20);
        await _sut.SnapshotAsync("p1", "s4", """{"n":2}""", note: "new");
        var list = await _sut.ListHistoryAsync("p1", "s4");
        Assert.Equal(2, list.Count);
        Assert.Equal("new", list[0].Note);
        Assert.Equal("old", list[1].Note);
    }

    [Fact]
    public async Task Restore_missing_version_fails()
    {
        var result = await _sut.RestoreAsync("p1", "s5", "no-such-version");
        Assert.False(result.Ok);
        Assert.Contains("not found", result.Error ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Independent_scenes_have_separate_histories()
    {
        await _sut.SnapshotAsync("p1", "a", """{"k":"a"}""");
        await _sut.SnapshotAsync("p1", "b", """{"k":"b"}""");
        Assert.Single(await _sut.ListHistoryAsync("p1", "a"));
        Assert.Single(await _sut.ListHistoryAsync("p1", "b"));
    }
}
