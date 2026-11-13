using System.Collections.Concurrent;

namespace Veronica.Agent.Mcp;

// Bare MCP: each client session works on a SessionState of its own. A client that sends no session id
// shares the scope's chat state. Sessions end on DELETE or with the Editor.
public sealed class McpClientSessions(SessionScope scope, Func<SessionState, string?> instructions) : IMcpSessions
{
    private readonly ConcurrentDictionary<string, SessionState> _open = new();

    // Any thread.
    public event Action? Changed;

    public int OpenCount => _open.Count;

    public McpSessionStart Open()
    {
        var state = scope.Create();
        string id = Guid.NewGuid().ToString("N");
        _open[id] = state;
        Changed?.Invoke();
        return new McpSessionStart(id, instructions(state));
    }

    public bool IsOpen(string id) => _open.ContainsKey(id);

    public void Close(string id)
    {
        if (_open.TryRemove(id, out _))
            Changed?.Invoke();
    }

    public SessionState For(string? id) => id != null && _open.TryGetValue(id, out var state) ? state : scope.Chat;

    public Task<T> RunAsync<T>(string? id, Func<Task<T>> run) => scope.RunInAsync(For(id), run);
}
