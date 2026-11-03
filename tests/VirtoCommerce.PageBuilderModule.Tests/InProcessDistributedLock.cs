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

    public async Task<IDistributedLockHandle> AcquireAsync(string resource, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_gate != null)
        {
            Waiting.TrySetResult();
            await _gate.Task.WaitAsync(cancellationToken);
        }

        var semaphore = _semaphores.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Handle(resource, semaphore);
    }

    public async Task<IDistributedLockHandle> TryAcquireAsync(string resource, TimeSpan timeout = default, CancellationToken cancellationToken = default)
    {
        var semaphore = _semaphores.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        return await semaphore.WaitAsync(timeout, cancellationToken) ? new Handle(resource, semaphore) : null;
    }

    private sealed class Handle(string resource, SemaphoreSlim semaphore) : IDistributedLockHandle
    {
        private int _released;

        public string Resource { get; } = resource;

        public CancellationToken HandleLostToken => CancellationToken.None;

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
