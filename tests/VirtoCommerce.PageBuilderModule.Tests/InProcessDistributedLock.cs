using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.DistributedLock;

namespace VirtoCommerce.PageBuilderModule.Tests;

// One semaphore per resource: callers of the same resource run one at a time, like the platform lock with one instance.
internal sealed class InProcessDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);
    private CancellationTokenSource _lost = new();
    private TaskCompletionSource _gate;

    /// <summary>Makes callers wait as if another holder had the lock, until <see cref="Release"/>.</summary>
    public void Hold()
    {
        _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Release()
    {
        _gate?.TrySetResult();
    }

    /// <summary>Completes when a caller is waiting at the gate.</summary>
    public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool LoseLocks { get; set; }

    // Only the handles held now are lost: the platform lock hands a later caller a live handle.
    public void LoseHeldLocks() => Interlocked.Exchange(ref _lost, new CancellationTokenSource()).Cancel();

    public TaskCompletionSource<string> Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IDistributedLockHandle> AcquireAsync(string resource, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_gate != null)
        {
            Waiting.TrySetResult();
            await _gate.Task.WaitAsync(cancellationToken);
        }

        var semaphore = _semaphores.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        if (semaphore.CurrentCount == 0)
        {
            Blocked.TrySetResult(resource);
        }
        await semaphore.WaitAsync(cancellationToken);
        return new Handle(resource, semaphore, LostToken);
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(string resource, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var semaphore = _semaphores.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        return await semaphore.WaitAsync(timeout, cancellationToken) ? new Handle(resource, semaphore, LostToken) : null;
    }

    private CancellationToken LostToken => LoseLocks ? new CancellationToken(true) : Volatile.Read(ref _lost).Token;

    private sealed class Handle(string resource, SemaphoreSlim semaphore, CancellationToken lost) : IDistributedLockHandle
    {
        private int _released;

        public string Resource { get; } = resource;

        public CancellationToken HandleLostToken { get; } = lost;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
