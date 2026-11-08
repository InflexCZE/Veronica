namespace Veronica.Agent.WebQwen;

// The local model. WebQwen.exe starts with the first round and stays up; when it dies the
// round fails and the next one starts a new process.
public sealed class WebQwenProvider(WebQwenLaunch launch, Action<string> log, IGpuShare? gpu = null) : IProvider, IDisposable
{
    private readonly Lock _gate = new();
    private WebQwenProcess? _process;
    private bool _disposed;

    private const string Loading = "The local model is loading...";

    public event Action<ProviderStatus>? StatusChanged;

    public int ContextWindow => IProvider.AssumedContextWindow;

    public bool RunsSubagents => true;

    public IProviderSession OpenSession(string name)
    {
        var session = ClientToolLoop.CreateSession(StreamRoundAsync);
        return gpu == null ? session : new GpuHeldSession(session, gpu);
    }

    public void Dispose()
    {
        WebQwenProcess? process;
        lock (_gate)
        {
            _disposed = true;
            process = _process;
            _process = null;
        }
        process?.Dispose(log);
    }

    private async Task StreamRoundAsync(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
    {
        // Off the caller's thread: starting a process can stall, and the caller may be the UI.
        var process = await Task.Run(EnsureStarted, CancellationToken.None).ConfigureAwait(false);
        try
        {
            await process.Connection.ChatAsync(request.Messages, request.Tools, request.MaxTokens ?? 0, request.Thinking ?? true, onDelta, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WebQwenClosedException)
        {
            Drop(process);
            throw;
        }
    }

    private WebQwenProcess EnsureStarted()
    {
        WebQwenProcess process;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_process != null)
                return _process;
            StatusChanged?.Invoke(new ProviderStatus(Loading, "Starting WebQwen"));
            _process = process = WebQwenProcess.Start(launch, line => log(line));
        }
        var connection = process.Connection;
        connection.Progress += (progress, text) => StatusChanged?.Invoke(new ProviderStatus(Loading, text, double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : null));
        connection.Start();
        connection.Ready.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                StatusChanged?.Invoke(new ProviderStatus("The local model is ready", Ready: true));
        }, TaskScheduler.Default);
        connection.Closed.ContinueWith(t =>
        {
            Drop(process);
            StatusChanged?.Invoke(new ProviderStatus("The local model stopped", t.Result));
        }, TaskScheduler.Default);
        // Taken after the process starts: rounds run inside a turn's hold, so the VRAM is already free.
        if (gpu != null)
            _ = HoldThroughLoadAsync(gpu.HoldAsync(), connection);
        return process;
    }

    // The load is one engine operation: the Editor stays out of the way until it ends, even when
    // the turn that started it is stopped first.
    private async Task HoldThroughLoadAsync(Task<IAsyncDisposable> holding, WebQwenConnection connection)
    {
        try
        {
            await using var hold = await holding.ConfigureAwait(false);
            await Task.WhenAny(connection.Ready, connection.Closed).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            log($"Freeing VRAM for the model load failed: {e.Message}");
        }
    }

    private void Drop(WebQwenProcess process)
    {
        lock (_gate)
        {
            if (_process != process)
                return;
            _process = null;
        }
        process.Dispose(log);
    }

    // The Editor stays out of the way for the whole turn, tool calls included: each round ends
    // with WebQwen releasing its VRAM, but the Editor does not come back between rounds.
    private sealed class GpuHeldSession(IProviderSession inner, IGpuShare gpu) : IProviderSession
    {
        public async Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
        {
            await using var hold = await gpu.HoldAsync().ConfigureAwait(false);
            await inner.RunTurnAsync(request, host, cancellationToken).ConfigureAwait(false);
        }

        public Task ResetAsync() => inner.ResetAsync();

        public void Dispose() => inner.Dispose();
    }
}
