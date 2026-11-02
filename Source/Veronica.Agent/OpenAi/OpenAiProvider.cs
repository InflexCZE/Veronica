using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Veronica.Agent.OpenAi;

// BaseUrl includes the API prefix, e.g. http://localhost:8080/v1. A null Model takes the
// first one the server lists. ReasoningEffort goes to the chat template; null leaves the template's default.
public sealed record OpenAiSettings(string BaseUrl, string? Model, string? ApiKey, string? ReasoningEffort = null);

public sealed class OpenAiException(string message) : Exception(message);

// Any server speaking the OpenAI chat-completions protocol, one streamed request per round.
public sealed class OpenAiProvider : IProvider, IDisposable
{
    private readonly OpenAiSettings _settings;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _modelGate = new(1, 1);
    private string? _model;

    public OpenAiProvider(OpenAiSettings settings, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _model = string.IsNullOrWhiteSpace(settings.Model) ? null : settings.Model.Trim();
        // A round can generate for minutes; the user's Stop is the timeout.
        _http = new HttpClient(handler ?? AnyCertificateHandler()) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public event Action<string>? StatusChanged;

    public IProviderSession OpenSession(string name) => ClientToolLoop.CreateSession(StreamRoundAsync);

    public int ContextWindow => IProvider.AssumedContextWindow;

    public bool RunsSubagents => true;

    public void Dispose() => _http.Dispose();

    // Locally hosted servers typically use self-signed certificates.
    private static SocketsHttpHandler AnyCertificateHandler() => new()
    {
        SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
    };

    public async Task StreamRoundAsync(RoundRequest request, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
    {
        string model = await ModelAsync(cancellationToken).ConfigureAwait(false);
        using var post = NewRequest(HttpMethod.Post, "chat/completions");
        post.Content = new StringContent(Body(model, request, _settings.ReasoningEffort), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(post, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new OpenAiException(await ErrorMessageAsync(response, cancellationToken).ConfigureAwait(false));
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await ReadStreamAsync(stream, onDelta, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (cancellationToken.IsCancellationRequested && e is OperationCanceledException or IOException)
        {
            // Stopped mid-reply: the round keeps what arrived.
        }
    }

    private async Task<string> ModelAsync(CancellationToken cancellationToken)
    {
        if (_model != null)
            return _model;
        await _modelGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_model != null)
                return _model;
            using var get = NewRequest(HttpMethod.Get, "models");
            using var response = await _http.SendAsync(get, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new OpenAiException($"Listing the server's models failed: {await ErrorMessageAsync(response, cancellationToken).ConfigureAwait(false)}");
            using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            string? id = list.RootElement.ValueKind == JsonValueKind.Object
                && list.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array
                && data.GetArrayLength() > 0
                && data[0].ValueKind == JsonValueKind.Object
                ? String(data[0], "id")
                : null;
            _model = id ?? throw new OpenAiException("The server lists no models.");
            StatusChanged?.Invoke($"Model {_model}");
            return _model;
        }
        finally
        {
            _modelGate.Release();
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        string baseUrl = _settings.BaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate($"{baseUrl}/{path}", UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new OpenAiException(baseUrl.Length == 0 ? "No server URL is configured." : $"Server URL '{baseUrl}' is not an http or https URL.");
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        return request;
    }

    private static string Body(string model, RoundRequest request, string? reasoningEffort)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray(request.Messages.Select(m => (JsonNode?)ToWire(m)).ToArray()),
            ["stream"] = true,
        };
        if (request.Thinking != false && !string.IsNullOrWhiteSpace(reasoningEffort))
            body["chat_template_kwargs"] = new JsonObject { ["reasoning_effort"] = reasoningEffort.Trim() };
        int budget = request.Thinking != false && request.ThinkingBudget is > 0 and var b ? b : 0;
        if (budget > 0)
            body["thinking_budget_tokens"] = budget;
        if (request.MaxTokens is > 0 and var maxTokens)
            body["max_tokens"] = maxTokens + budget;
        if (request.Tools.Count > 0)
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.Parameters),
                },
            }).ToArray());
        return body.ToJsonString();
    }

    private static JsonObject ToWire(ChatMessage message)
    {
        string role = message.Role switch
        {
            ChatRole.System => "system",
            ChatRole.User => "user",
            ChatRole.Assistant => "assistant",
            ChatRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(message)),
        };
        var wire = new JsonObject { ["role"] = role, ["content"] = message.Content };
        if (message.Reasoning != null)
            wire["reasoning_content"] = message.Reasoning;
        if (message.ToolCalls is { Count: > 0 } calls)
            wire["tool_calls"] = new JsonArray(calls.Select(c => (JsonNode?)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.Arguments },
            }).ToArray());
        if (message.ToolCallId != null)
            wire["tool_call_id"] = message.ToolCallId;
        return wire;
    }

    private static async Task ReadStreamAsync(Stream stream, Action<StreamDelta> onDelta, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        RoundStats? stats = null;
        string? finishReason = null;
        bool sawData = false;
        var skipped = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (!sawData && skipped.Length < 300)
                    skipped.AppendLine(line);
                continue;
            }
            sawData = true;
            string data = line["data:".Length..].Trim();
            if (data.Length == 0)
                continue;
            if (data == "[DONE]")
                break;
            JsonDocument chunk;
            try
            {
                chunk = JsonDocument.Parse(data);
            }
            catch (JsonException)
            {
                throw new OpenAiException($"The server sent an unreadable stream line: {Excerpt(data)}");
            }
            using var _ = chunk;
            var root = chunk.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                continue;
            // Some servers report a failure mid-stream as an error member on a 200 response.
            if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                throw new OpenAiException(ErrorText(error) ?? "The server reported an error.");
            // llama.cpp's running totals; only the last chunk's count.
            if (root.TryGetProperty("timings", out var timings) && Stats(timings) is { } s)
                stats = s;
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                continue;
            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object)
                continue;
            if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
            {
                if (String(delta, "content") is { Length: > 0 } text)
                    onDelta(new TextDelta(text));
                // llama.cpp, vLLM and DeepSeek name it reasoning_content, OpenRouter reasoning;
                // a server sending both carries the same text twice.
                if ((NonEmpty(String(delta, "reasoning_content")) ?? NonEmpty(String(delta, "reasoning"))) is { } reasoning)
                    onDelta(new ReasoningDelta(reasoning));
                if (delta.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
                {
                    int position = 0;
                    foreach (var call in calls.EnumerateArray())
                    {
                        if (call.ValueKind != JsonValueKind.Object)
                            continue;
                        int index = call.TryGetProperty("index", out var i) && i.ValueKind == JsonValueKind.Number && i.TryGetInt32(out int n) ? n : position;
                        position++;
                        bool hasFunction = call.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.Object;
                        onDelta(new ToolCallDelta(index, String(call, "id"), hasFunction ? String(function, "name") : null, hasFunction ? String(function, "arguments") : null));
                    }
                }
            }
            if (String(choice, "finish_reason") is { } finish)
                finishReason = finish;
        }
        // A 200 without an event stream: a URL missing its /v1, a proxy page, or a server ignoring stream.
        if (!sawData)
        {
            string body = skipped.ToString().Trim();
            throw new OpenAiException(body.Length == 0 ? "The server sent an empty reply." : $"The server did not send an event stream: {Excerpt(body)}");
        }
        if (stats != null)
            onDelta(new StatsDelta(stats));
        onDelta(new DoneDelta(finishReason));
    }

    private static RoundStats? Stats(JsonElement timings)
    {
        if (timings.ValueKind != JsonValueKind.Object
            || Number(timings, "prompt_n") is not { } promptTokens
            || Number(timings, "prompt_ms") is not { } promptMs
            || Number(timings, "predicted_n") is not { } completionTokens
            || Number(timings, "predicted_ms") is not { } completionMs)
            return null;
        return new RoundStats(
            (int)promptTokens,
            (int)completionTokens,
            promptMs > 0 ? promptTokens * 1000 / promptMs : 0,
            completionMs > 0 ? completionTokens * 1000 / completionMs : 0)
        {
            CachedTokens = Number(timings, "cache_n") is { } cached ? (int)cached : null,
        };
    }

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("error", out var error)
                && ErrorText(error) is { } text)
                return $"{status}: {text}";
        }
        catch (JsonException)
        {
        }
        body = body.Trim();
        return body.Length == 0 ? status : $"{status}: {Excerpt(body)}";
    }

    private static string Excerpt(string text) => text.Length > 300 ? text[..300] + "..." : text;

    private static string? ErrorText(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
            return NonEmpty(error.GetString());
        if (error.ValueKind != JsonValueKind.Object)
            return null;
        // OpenRouter puts the upstream explanation here; message is only "Provider returned error".
        string? raw = error.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object ? String(metadata, "raw") : null;
        return NonEmpty(raw) ?? NonEmpty(String(error, "message"));
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static string? NonEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
