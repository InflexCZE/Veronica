using Veronica.Agent;
using Xunit;

namespace Veronica.Tests;

public sealed class GpuShareTests
{
    private readonly List<string> _calls = [];
    private readonly List<string> _log = [];

    private GpuShare Share(Func<Task>? free = null, Func<Task>? restore = null) => new(
        free ?? (() => { _calls.Add("free"); return Task.CompletedTask; }),
        restore ?? (() => { _calls.Add("restore"); return Task.CompletedTask; }),
        _log.Add);

    [Fact]
    public async Task One_hold_frees_then_restores()
    {
        var share = Share();
        var hold = await share.HoldAsync();
        Assert.Equal(["free"], _calls);
        await hold.DisposeAsync();
        Assert.Equal(["free", "restore"], _calls);
    }

    [Fact]
    public async Task Nested_holds_free_once_and_restore_after_the_last()
    {
        var share = Share();
        var turn = await share.HoldAsync();
        var load = await share.HoldAsync();
        await turn.DisposeAsync();
        Assert.Equal(["free"], _calls);
        await load.DisposeAsync();
        Assert.Equal(["free", "restore"], _calls);
    }

    [Fact]
    public async Task A_hold_waits_for_the_free()
    {
        var freeing = new TaskCompletionSource();
        var share = Share(free: () => freeing.Task);
        var first = share.HoldAsync();
        var second = share.HoldAsync();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        freeing.SetResult();
        await (await first).DisposeAsync();
        await (await second).DisposeAsync();
    }

    [Fact]
    public async Task Disposing_twice_releases_once()
    {
        var share = Share();
        var turn = await share.HoldAsync();
        var load = await share.HoldAsync();
        await turn.DisposeAsync();
        await turn.DisposeAsync();
        Assert.Equal(["free"], _calls);
        await load.DisposeAsync();
    }

    [Fact]
    public async Task A_failed_free_fails_the_hold_and_still_restores()
    {
        var share = Share(free: () => { _calls.Add("free"); throw new InvalidOperationException("engine changed"); });
        var e = await Assert.ThrowsAsync<InvalidOperationException>(share.HoldAsync);
        Assert.Equal("engine changed", e.Message);
        Assert.Equal(["free", "restore"], _calls);
    }

    [Fact]
    public async Task A_hold_after_a_failure_frees_again()
    {
        int frees = 0;
        var share = Share(free: () =>
        {
            _calls.Add("free");
            return ++frees == 1 ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(share.HoldAsync);
        await (await share.HoldAsync()).DisposeAsync();
        Assert.Equal(["free", "restore", "free", "restore"], _calls);
    }

    [Fact]
    public async Task A_failed_restore_is_logged_not_thrown()
    {
        var share = Share(restore: () => Task.FromException(new InvalidOperationException("stuck")));
        await (await share.HoldAsync()).DisposeAsync();
        Assert.Contains(_log, line => line.Contains("stuck"));
    }

    [Fact]
    public async Task A_hold_during_a_restore_frees_after_it()
    {
        var restoring = new TaskCompletionSource();
        var share = Share(restore: () => { _calls.Add("restore"); return restoring.Task; });
        var first = await share.HoldAsync();
        var released = first.DisposeAsync().AsTask();
        var second = share.HoldAsync();
        Assert.False(second.IsCompleted);
        restoring.SetResult();
        await released;
        await (await second).DisposeAsync();
        Assert.Equal(["free", "restore", "free", "restore"], _calls);
    }
}
