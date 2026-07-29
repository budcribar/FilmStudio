using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class MtimeValidatedFileCacheTests
{
    [Fact]
    public async Task NoOp_cache_hit_by_mtime_and_reparses_on_change()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-mtimecache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.txt");
            await File.WriteAllTextAsync(path, "one");
            var cache = new MtimeValidatedFileCache<string, NoOpSemaphore>();
            var parses = 0;
            Task<string> Parse(byte[] bytes, CancellationToken _)
            {
                parses++;
                return Task.FromResult(System.Text.Encoding.UTF8.GetString(bytes));
            }

            var a = await cache.GetOrLoadAsync(path, Parse);
            var b = await cache.GetOrLoadAsync(path, Parse);
            Assert.Equal("one", a);
            Assert.Same(a, b); // same cached instance — no reparse on hit
            Assert.Equal(1, parses);

            await Task.Delay(20);
            await File.WriteAllTextAsync(path, "two");
            var c = await cache.GetOrLoadAsync(path, Parse);
            Assert.Equal("two", c);
            Assert.Equal(2, parses);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Missing_file_returns_null_without_throwing()
    {
        var cache = new MtimeValidatedFileCache<string, NoOpSemaphore>();
        var result = await cache.GetOrLoadAsync(
            Path.Combine(Path.GetTempPath(), "nope_" + Guid.NewGuid() + ".txt"),
            (bytes, _) => Task.FromResult(System.Text.Encoding.UTF8.GetString(bytes)));
        Assert.Null(result);
    }

    [Fact]
    public async Task Invalidate_forces_reparse_even_without_mtime_change()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-mtimecache-inv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.txt");
            await File.WriteAllTextAsync(path, "one");
            var cache = new MtimeValidatedFileCache<string, NoOpSemaphore>();
            var parses = 0;
            Task<string> Parse(byte[] bytes, CancellationToken _)
            {
                parses++;
                return Task.FromResult(System.Text.Encoding.UTF8.GetString(bytes));
            }

            _ = await cache.GetOrLoadAsync(path, Parse);
            cache.Invalidate(path);
            _ = await cache.GetOrLoadAsync(path, Parse);
            Assert.Equal(2, parses);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RealSemaphore_read_waits_for_the_writer_holding_the_same_gate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fs-mtimecache-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "value.txt");
            await File.WriteAllTextAsync(path, "one");

            // Simulates a writer's own per-path gate — the cache must resolve to this exact
            // instance so a read can never land while the "writer" holds it.
            var writerGate = new SemaphoreSlim(1, 1);
            var cache = new MtimeValidatedFileCache<string, RealSemaphore>(
                resolveReadGate: _ => new RealSemaphore(writerGate));

            await writerGate.WaitAsync(); // hold the gate as if a write is in progress
            var readTask = cache.GetOrLoadAsync(path,
                (bytes, _) => Task.FromResult(System.Text.Encoding.UTF8.GetString(bytes)));

            var completedEarly = await Task.WhenAny(readTask, Task.Delay(200)) == readTask;
            Assert.False(completedEarly, "read must block while the writer's gate is held");

            writerGate.Release();
            var result = await readTask;
            Assert.Equal("one", result);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
