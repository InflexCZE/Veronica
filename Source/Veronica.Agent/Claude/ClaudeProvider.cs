using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Veronica.Agent.Tools;

namespace Veronica.Agent.Claude;

public sealed record ClaudeSettings(string Model, string? Effort, int McpPort, string WorkingDirectory)
{
    public const string DefaultModel = "sonnet";
    public const int DefaultMcpPort = 47812;
    public const int MaxTurns = 100;

    public static string DefaultWorkingDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veronica", "Claude");
}

// Each turn runs one claude -p, which runs its own tool loop. Veronica's tools reach it through the
// MCP endpoint, which also serves any other local MCP client.
public sealed class ClaudeProvider(ClaudeSettings settings, ToolSet tools) : IProvider, IDisposable
{
    // How long a tool call waits for its tool_use block to show up on Claude Code's stdout.
    private static readonly TimeSpan MatchWait = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlyDictionary<string, string> ChildEnvironment = new Dictionary<string, string>
    {
        // The user's Stop is the timeout; some Editor tools take minutes.
        ["MCP_TOOL_TIMEOUT"] = "86400000",
        // Tools hidden behind a search would need instructions the replaced system prompt no longer has.
        ["ENABLE_TOOL_SEARCH"] = "false",
    };

    private readonly object _gate = new();
    private readonly SemaphoreSlim _toolGate = new(1, 1);
    private McpServer? _server;
    private Turn? _turn;
    private int _externalCalls;

    public event Action<string>? StatusChanged;

    public IProviderSession OpenSession() => new Session(this);

    public void Dispose()
    {
        lock (_gate)
        {
            _server?.Dispose();
            _server = null;
        }
    }

    private McpServer Server()
    {
        lock (_gate)
        {
            if (_server != null)
                return _server;
            try
            {
                _server = new McpServer(settings.McpPort, tools.Definitions, CallToolAsync);
            }
            catch (SocketException e)
            {
                throw new ClaudeException($"Veronica's tool endpoint cannot listen on 127.0.0.1:{settings.McpPort} ({e.Message}). Set VERONICA_MCP_PORT to a free port.");
            }
        }
        StatusChanged?.Invoke($"Claude Code ({settings.Model}), tools at {_server.Url}");
        return _server;
    }

    // One tool at a time, from any client. A call that arrived during a turn runs in that turn, or not
    // at all when the turn ended while it waited.
    private async Task<ToolResult?> CallToolAsync(string name, string arguments, CancellationToken stop)
    {
        Turn? arrivedIn = CurrentTurn;
        await _toolGate.WaitAsync(stop).ConfigureAwait(false);
        try
        {
            var turn = CurrentTurn;
            if (arrivedIn != null && arrivedIn != turn)
                return null;
            if (turn == null)
                return await tools.ExecuteAsync(new ToolCall(NextId(), name, arguments), stop).ConfigureAwait(false);
            string id = await turn.MatchAsync(name, arguments, MatchWait).ConfigureAwait(false) ?? NextId();
            return await turn.Host.OnToolCallAsync(new ToolCall(id, name, arguments), new ToolCallContext(false), turn.Cancellation).ConfigureAwait(false);
        }
        finally
        {
            _toolGate.Release();
        }
    }

    private Turn? CurrentTurn
    {
        get
        {
            lock (_gate)
                return _turn;
        }
    }

    private ClaudeSettings Settings => settings;

    private string NextId() => $"mcp-{Interlocked.Increment(ref _externalCalls)}";

    private void Begin(Turn turn)
    {
        lock (_gate)
        {
            if (_turn != null)
                throw new InvalidOperationException("A Claude turn is already running.");
            _turn = turn;
        }
    }

    private void End(Turn turn)
    {
        lock (_gate)
        {
            if (_turn == turn)
                _turn = null;
        }
    }

    private List<string> Arguments(string systemPromptFile, string mcpConfigFile, string sessionFlag, string sessionId)
    {
        var arguments = new List<string>
        {
            "-p",
            "--output-format", "stream-json",
            "--verbose",
            "--include-partial-messages",
            "--model", settings.Model,
            "--system-prompt-file", systemPromptFile,
            "--tools", "",
            "--strict-mcp-config",
            "--mcp-config", mcpConfigFile,
            "--setting-sources", "",
            "--settings", "{\"autoMemoryEnabled\":false}",
            "--permission-mode", "dontAsk",
            "--allowedTools", $"{McpServer.ToolNamePrefix}*",
            "--max-turns", ClaudeSettings.MaxTurns.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sessionFlag, sessionId,
        };
        if (!string.IsNullOrWhiteSpace(settings.Effort))
        {
            arguments.Add("--effort");
            arguments.Add(settings.Effort.Trim());
        }
        return arguments;
    }

    private static string McpConfig(string url) => new JsonObject
    {
        ["mcpServers"] = new JsonObject
        {
            [McpServer.ServerName] = new JsonObject { ["type"] = "http", ["url"] = url },
        },
    }.ToJsonString();

    // Matches an MCP call to the tool_use block Claude Code streamed for it, so the chat and the
    // transcript share the call's id.
    private sealed class Turn(ITurnHost host, CancellationToken cancellation)
    {
        private readonly List<ToolCall> _unmatched = [];
        private TaskCompletionSource _added = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITurnHost Host { get; } = host;

        public CancellationToken Cancellation { get; } = cancellation;

        public void Add(ToolCall call)
        {
            TaskCompletionSource added;
            lock (_unmatched)
            {
                _unmatched.Add(call);
                added = _added;
                _added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            added.SetResult();
        }

        public async Task<string?> MatchAsync(string name, string arguments, TimeSpan wait)
        {
            long start = Stopwatch.GetTimestamp();
            while (true)
            {
                Task added;
                lock (_unmatched)
                {
                    int index = _unmatched.FindIndex(c => c.Name == name && SameJson(c.Arguments, arguments));
                    if (index >= 0)
                    {
                        string id = _unmatched[index].Id;
                        _unmatched.RemoveAt(index);
                        return id;
                    }
                    added = _added.Task;
                }
                var remaining = wait - Stopwatch.GetElapsedTime(start);
                if (remaining <= TimeSpan.Zero || Cancellation.IsCancellationRequested)
                    return null;
                await Task.WhenAny(added, Task.Delay(remaining, Cancellation)).ConfigureAwait(false);
            }
        }

        private static bool SameJson(string a, string b)
        {
            try
            {
                return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
            }
            catch (JsonException)
            {
                return a == b;
            }
        }
    }

    private sealed class Session(ClaudeProvider provider) : IProviderSession
    {
        private const int MaxErrorChars = 1000;

        private string _sessionId = Guid.NewGuid().ToString();
        private bool _started;

        public Task ResetAsync()
        {
            _sessionId = Guid.NewGuid().ToString();
            _started = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public async Task RunTurnAsync(TurnRequest request, ITurnHost host, CancellationToken cancellationToken)
        {
            string message = request.Transcript.LastOrDefault(m => m.Role == ChatRole.User)?.Content
                ?? throw new ArgumentException("The turn has no user message.", nameof(request));
            var server = provider.Server();

            var turn = new Turn(host, cancellationToken);
            provider.Begin(turn);
            try
            {
                string directory = provider.Settings.WorkingDirectory;
                Directory.CreateDirectory(directory);
                string promptFile = Path.Combine(directory, "system-prompt.md");
                string mcpFile = Path.Combine(directory, "mcp.json");
                File.WriteAllText(promptFile, request.SystemPrompt);
                File.WriteAllText(mcpFile, McpConfig(server.Url));
                var arguments = provider.Arguments(promptFile, mcpFile, _started ? "--resume" : "--session-id", _sessionId);

                using var process = ClaudeProcess.Start(arguments, directory, ChildEnvironment, message);
                var stream = new ClaudeStream(host, turn.Add);
                int exitCode;
                try
                {
                    using (cancellationToken.Register(process.Kill))
                    {
                        try
                        {
                            while (await process.Output.ReadLineAsync().ConfigureAwait(false) is { } line)
                                stream.Read(line);
                        }
                        catch (IOException) when (cancellationToken.IsCancellationRequested)
                        {
                        }
                        exitCode = await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    // A failed read must not leave Claude Code running, calling tools outside any turn.
                    process.Kill();
                    stream.Finish();
                    // Claude Code stored nothing under an unused id; a fresh one avoids colliding with
                    // whatever it may have half-created.
                    if (stream.SawMessages)
                        _started = true;
                    else if (!_started)
                        _sessionId = Guid.NewGuid().ToString();
                }

                if (cancellationToken.IsCancellationRequested)
                    return;
                if (stream.Result is not { } result)
                    throw new ClaudeException(Failed(exitCode, process.Error));
                if (result.Subtype == "error_max_turns")
                {
                    host.OnNotice(ClientToolLoop.RoundCapNotice(ClaudeSettings.MaxTurns));
                    return;
                }
                if (result.IsError || result.Subtype != "success")
                    throw new ClaudeException(result.Text is { Length: > 0 } text ? Excerpt(text) : $"Claude Code ended the turn with '{result.Subtype}'.");
            }
            finally
            {
                provider.End(turn);
            }
        }

        private static string Failed(int exitCode, string error)
        {
            error = error.Trim();
            return error.Length == 0
                ? $"Claude Code exited with code {exitCode} without an answer."
                : $"Claude Code exited with code {exitCode}: {Excerpt(error)}";
        }

        private static string Excerpt(string text) => text.Length > MaxErrorChars ? text[..MaxErrorChars] + "..." : text;
    }
}
