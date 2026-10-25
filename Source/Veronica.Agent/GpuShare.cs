namespace Veronica.Agent;

// The other side of the GPU, the Editor. It keeps its VRAM out of the local model's way while
// any hold is open and takes it back when the last one is released (Docs/Tech/VramSharing.md).
public interface IGpuShare
{
    // The hold counts from the call; the task completes once the VRAM is free. A failed task
    // holds nothing.
    Task<IAsyncDisposable> HoldAsync();
}

// Counts holds over a free/restore pair. Transitions run one after another: a restore always
// follows its free, even a failed one, so the restore must cope with a partial free.
public sealed class GpuShare(Func<Task> free, Func<Task> restore, Action<string> log) : IGpuShare
{
    private readonly Lock _gate = new();
    private int _holds;
    private Task _last = Task.CompletedTask;
    private Task _freed = Task.CompletedTask;

    public Task<IAsyncDisposable> HoldAsync()
    {
        Task freed;
        lock (_gate)
        {
            if (_holds++ == 0)
                _freed = _last = After(_last, free);
            freed = _freed;
        }
        return HoldWhenFreedAsync(freed);
    }

    private async Task<IAsyncDisposable> HoldWhenFreedAsync(Task freed)
    {
        try
        {
            await freed.ConfigureAwait(false);
        }
        catch
        {
            await RestoreLoggedAsync(ReleaseAsync()).ConfigureAwait(false);
            throw;
        }
        return new Hold(this);
    }

    private Task ReleaseAsync()
    {
        lock (_gate)
        {
            if (--_holds > 0)
                return Task.CompletedTask;
            _last = After(_last, restore);
            return _last;
        }
    }

    private async Task After(Task previous, Func<Task> next)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
        }
        await next().ConfigureAwait(false);
    }

    private async Task RestoreLoggedAsync(Task restoring)
    {
        try
        {
            await restoring.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            log($"Giving the Editor its VRAM back failed: {e.Message}");
        }
    }

    private sealed class Hold(GpuShare share) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync() =>
            Interlocked.Exchange(ref _released, 1) == 0 ? new ValueTask(share.RestoreLoggedAsync(share.ReleaseAsync())) : ValueTask.CompletedTask;
    }
}
