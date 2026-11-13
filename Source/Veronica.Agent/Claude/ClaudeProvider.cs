using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Veronica.Agent.Mcp;

namespace Veronica.Agent.Claude;

public sealed record ClaudeSettings(string Model, string? Effort, string WorkingDirectory)
{
    public const string DefaultModel = "sonnet";
    public const int MaxTurns = 100;

    public static string DefaultWorkingDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veronica", "Claude");
}

// Each turn runs one claude -p, which runs its own tool loop. Veronica's tools reach it through the
// MCP endpoint.
public sealed class ClaudeProvider(ClaudeSettings settings, McpEndpoint endpoint) : IProvider
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
    private readonly SemaphoreSlim _matchGate = new(1, 1);
    private Turn? _turn;
    private IDisposable? _routing;
    private bool _announced;
    private int _externalCalls;

    public event Action<ProviderStatus>? StatusChanged;

    public IProviderSession OpenSession(string name) => new Session(this);

    public int ContextWindow => IProvider.AssumedContextWindow;

    // Its turn runs in claude -p, which only reaches Veronica through the MCP endpoint; it reads wiki pages itself.
    public bool RunsSubagents => false;

    private string StartEndpoint()
    {
        string url = endpoint.Start();
        bool announce;
        lock (_gate)
        {
            announce = !_announced;
            _announced = true;
        }
        if (announce)
            StatusChanged?.Invoke(new ProviderStatus($"Claude Code ({settings.Model})", $"tools at {url}", Ready: true));
        return url;
    }

    // Every call during a turn runs in the turn, whichever client sent it, or not at all when the
    // turn ended while it waited. One at a time, so each call takes the oldest matching tool_use.
    private async Task<ToolResult?> RouteAsync(Turn turn, string name, string arguments, CancellationToken stop)
    {
        string id;
        await _matchGate.WaitAsync(stop).ConfigureAwait(false);
        bool held = true;
        try
        {
            if (CurrentTurn != turn)
                return null;
            id = await turn.MatchAsync(name, arguments, MatchWait).ConfigureAwait(false) ?? NextId();
            if (name != AskUserTool.ToolName)
                return await turn.Host.OnToolCallAsync(new ToolCall(id, name, arguments), new ToolCallContext(false), turn.Cancellation).ConfigureAwait(false);
            // A question waits for the user; holding the gate would stall every other call meanwhile.
            _matchGate.Release();
            held = false;
        }
        finally
        {
            if (held)
                _matchGate.Release();
        }
        return await turn.Host.OnToolCallAsync(new ToolCall(id, name, arguments), new ToolCallContext(false), turn.Cancellation).ConfigureAwait(false);
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
            _routing = endpoint.Route((name, arguments, stop) => RouteAsync(turn, name, arguments, stop));
            _turn = turn;
        }
    }

    private void End(Turn turn)
    {
        lock (_gate)
        {
            if (_turn != turn)
                return;
            _turn = null;
            _routing?.Dispose();
            _routing = null;
        }
        turn.End();
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
        private readonly CancellationTokenSource _ended = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        private TaskCompletionSource _added = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ITurnHost Host { get; } = host;

        // Cancelled by Stop or by the turn's end: a call still waiting for the tool lock then must not
        // run in a turn that is over.
        public CancellationToken Cancellation => _ended.Token;

        public void End() => _ended.Cancel();

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
            string url = provider.StartEndpoint();

            var turn = new Turn(host, cancellationToken);
            provider.Begin(turn);
            try
            {
                string directory = provider.Settings.WorkingDirectory;
                Directory.CreateDirectory(directory);
                string promptFile = Path.Combine(directory, "system-prompt.md");
                string mcpFile = Path.Combine(directory, "mcp.json");
                File.WriteAllText(promptFile, request.SystemPrompt);
                File.WriteAllText(mcpFile, McpConfig(url));
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
