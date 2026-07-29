using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PageToMovie.Core.Utils;

/// <summary>
/// Thread-safe async key-based concurrency lock template.
/// Replaces repeated ConcurrentDictionary&lt;TKey, SemaphoreSlim&gt; lookup, wait, and try/finally release boilerplate.
/// </summary>
public sealed class KeyedAsyncLock<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _gates;

    public KeyedAsyncLock(IEqualityComparer<TKey>? comparer = null)
    {
        _gates = new ConcurrentDictionary<TKey, SemaphoreSlim>(comparer ?? EqualityComparer<TKey>.Default);
    }

    public async ValueTask<IDisposable> LockAsync(TKey key, CancellationToken ct = default)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        return new LockReleaser(gate);
    }

    private sealed class LockReleaser : IDisposable
    {
        private SemaphoreSlim? _gate;
        public LockReleaser(SemaphoreSlim gate) => _gate = gate;
        public void Dispose()
        {
            var gate = Interlocked.Exchange(ref _gate, null);
            gate?.Release();
        }
    }
}
