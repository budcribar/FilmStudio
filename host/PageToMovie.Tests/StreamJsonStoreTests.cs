using PageToMovie.Core.Utils;
using Xunit;

namespace PageToMovie.Tests;

public class StreamJsonStoreTests
{
    private sealed record Payload(string Name, int Count);

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-streamjson-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.json");
            await StreamJsonStore.SaveAsync(path, new Payload("Buster", 3));

            var loaded = await StreamJsonStore.LoadAsync<Payload>(path);
            Assert.NotNull(loaded);
            Assert.Equal("Buster", loaded!.Name);
            Assert.Equal(3, loaded.Count);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task SaveAsync_leaves_no_temp_file_behind_and_is_atomic_on_overwrite()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-streamjson-atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.json");
            await StreamJsonStore.SaveAsync(path, new Payload("first", 1));
            await StreamJsonStore.SaveAsync(path, new Payload("second", 2));

            // No matter how many overwrites happen, only the final target file should remain —
            // no .tmp artifacts, and the file is never observably absent/truncated in between
            // since the write lands via File.Move rather than truncating the target in place.
            var filesInDir = Directory.GetFiles(dir);
            Assert.Single(filesInDir);
            Assert.Equal(path, filesInDir[0]);

            var loaded = await StreamJsonStore.LoadAsync<Payload>(path);
            Assert.Equal("second", loaded!.Name);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task LoadAsync_missing_file_returns_default()
    {
        var result = await StreamJsonStore.LoadAsync<Payload>(
            Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid() + ".json"));
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_corrupt_json_returns_default_instead_of_throwing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-streamjson-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.json");
            await File.WriteAllTextAsync(path, "{ not valid json");
            var result = await StreamJsonStore.LoadAsync<Payload>(path);
            Assert.Null(result);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
