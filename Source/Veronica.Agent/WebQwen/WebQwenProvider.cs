namespace Veronica.Agent.WebQwen;

// The local model. WebQwen.exe starts with the first round and stays up; when it dies the
// round fails and the next one starts a new process.
public sealed class WebQwenProvider(WebQwenLaunch launch, Action<string> log) : IProvider, IDisposable
{
    private readonly Lock _gate = new();
    private WebQwenProcess? _process;
    private bool _disposed;

    public event Action<string>? StatusChanged;

    public IProviderSession OpenSession() => ClientToolLoop.CreateSession(StreamRoundAsync);

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
        if (request.Tools.Count > 0)
            throw new NotSupportedException("WebQwen does not carry tools yet.");
        // Off the caller's thread: starting a process can stall, and the caller may be the UI.
        var process = await Task.Run(EnsureStarted, CancellationToken.None).ConfigureAwait(false);
        try
        {
            await process.Connection.ChatAsync(request.Messages, request.MaxTokens ?? 0, request.Thinking ?? true, onDelta, cancellationToken)
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
            StatusChanged?.Invoke("Starting WebQwen...");
            _process = process = WebQwenProcess.Start(launch, line => log(line));
        }
        var connection = process.Connection;
        connection.Progress += (_, text) => StatusChanged?.Invoke(text);
        connection.Start();
        connection.Ready.ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                StatusChanged?.Invoke("Ready");
        }, TaskScheduler.Default);
        connection.Closed.ContinueWith(t =>
        {
            Drop(process);
            StatusChanged?.Invoke($"WebQwen stopped: {t.Result}");
        }, TaskScheduler.Default);
        return process;
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
}
