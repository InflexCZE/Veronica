using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Veronica.Agent;
using Veronica.Agent.Claude;

namespace Veronica.Setup.Core;

public interface IEditorProbe
{
    bool IsRunning();
}

// The Editor locks Runtime's DLLs while it runs, and may write a .vrgproj back over an edit.
public sealed class EditorProbe(InstallLayout layout) : IEditorProbe
{
    private static readonly string[] ProcessNames = ["VRage.Editor"];

    public bool IsRunning()
    {
        foreach (var name in ProcessNames)
        {
            var processes = Process.GetProcessesByName(name);
            bool running = processes.Length > 0;
            foreach (var p in processes)
                p.Dispose();
            if (running)
                return true;
        }
        return IsLocked(layout.PluginDll);
    }

    private static bool IsLocked(string path)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}

// Url: the server address that passed, when the check had to work it out.
public sealed record CheckResult(bool Ok, string Message, string? Url = null);

// Each provider's cheap check: no prompt and no model load (Docs/Tech/Distribution.md, Providers).
public static class ProviderCheck
{
    public static CheckResult WebQwen(IFiles files, InstallLayout layout, InstalledRecord record, IReadOnlyList<ModelRecord> manifest, string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return new CheckResult(false, "No local model is chosen.");
        var model = manifest.FirstOrDefault(m => m.Id == modelId);
        if (model == null)
            return new CheckResult(false, $"The installed WebQwen does not know the model {modelId}.");
        if (!record.Models.TryGetValue(modelId, out var pin) || pin != model.Pin || !files.FileExists(Path.Combine(layout.ModelFolder(modelId), "webgpu.wasm")))
            return new CheckResult(false, $"{modelId} is not downloaded and verified.");
        return new CheckResult(true, $"{modelId} is downloaded and verified.");
    }

    public static async Task<CheckResult> ClaudeAsync(CancellationToken cancellationToken)
    {
        var info = ClaudeCommand.StartInfo(["auth", "status", "--json"]);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string output;
        string error;
        try
        {
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            error = await errorTask.ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CheckResult(false, "'claude auth status' did not answer.");
        }
        return process.ExitCode == ClaudeCommand.NotFoundExitCode
            ? new CheckResult(false, ClaudeCommand.NotFoundMessage)
            : ClaudeStatus(output, error);
    }

    public static CheckResult ClaudeStatus(string json, string error = "")
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("loggedIn", out var v) && v.ValueKind == JsonValueKind.True
                ? new CheckResult(true, "Claude Code is installed and signed in.")
                : new CheckResult(false, "Claude Code is not signed in. Run 'claude' once and sign in.");
        }
        catch (JsonException)
        {
            string said = error.Trim();
            if (said.Length > 300)
                said = said[..300] + "...";
            return new CheckResult(false, said.Length == 0
                ? "'claude auth status' gave no status; update Claude Code."
                : $"'claude auth status' gave no status; update Claude Code. It said: {said}");
        }
    }

    // llama.cpp, LM Studio, Ollama, vLLM.
    private static readonly int[] CommonPorts = [8080, 1234, 11434, 8000];

    private static readonly string[] CommonPrefixes = ["", "/v1", "/api/v1"];

    // The base URLs a typed server address may mean, most preferred first: https before http, the port as typed
    // or the scheme's default before the common LLM ports, the path as typed before the common API prefixes.
    // An empty address means a server on this machine.
    public static IReadOnlyList<string> OpenAiCandidates(string? typed)
    {
        string text = (typed ?? "").Trim().TrimEnd('/');
        bool empty = text.Length == 0;
        bool hasScheme = text.Contains("://", StringComparison.Ordinal);
        string[] schemes = hasScheme ? [""] : ["https://", "http://"];
        var candidates = new List<string>();
        foreach (var scheme in schemes)
        {
            if (!Uri.TryCreate(scheme + (empty ? "localhost" : text), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || uri.Host.Length == 0)
                continue;
            int[] ports = !uri.IsDefaultPort ? [uri.Port] : empty ? CommonPorts : [uri.Port, .. CommonPorts];
            string path = uri.AbsolutePath.TrimEnd('/');
            string[] paths = path.Length > 0 ? [path] : CommonPrefixes;
            foreach (int port in ports)
                foreach (var prefix in paths)
                {
                    var builder = new UriBuilder(uri.Scheme, uri.Host, port, prefix);
                    candidates.Add(builder.Uri.AbsoluteUri.TrimEnd('/'));
                }
        }
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Every candidate is asked at once, so hosts that drop packets cost one short timeout, not one each. The search
    // carries no API key, so the key never goes to a guessed address or over a guessed plain http: a server that
    // wants one answers 401 or 403, and only that server is then asked again with the key.
    public static async Task<CheckResult> OpenAiAsync(HttpClient http, string? typed, string? model, string? apiKey, CancellationToken cancellationToken)
    {
        var candidates = OpenAiCandidates(typed);
        string shown = string.IsNullOrWhiteSpace(typed) ? "localhost" : typed.Trim();
        if (candidates.Count == 0)
            return new CheckResult(false, $"'{shown}' is not a server address.");
        var timeout = candidates.Count == 1 ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(5);
        var probes = await Task.WhenAll(candidates.Select(c => ProbeOpenAiAsync(http, c, model, null, timeout, cancellationToken))).ConfigureAwait(false);
        if (probes.FirstOrDefault(p => p.Answer >= OpenAiAnswer.Server) is not { } found)
        {
            var other = probes.FirstOrDefault(p => p.Answer == OpenAiAnswer.Other);
            return candidates.Count == 1 || other != null ? (other ?? probes[0]).Result : new CheckResult(false, $"No OpenAI-compatible server answered at {shown}.");
        }
        if (found.Answer != OpenAiAnswer.NeedsKey)
            return found.Result;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new CheckResult(false, $"The server at {found.BaseUrl} needs an API key.");
        var keyed = await ProbeOpenAiAsync(http, found.BaseUrl, model, apiKey, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
        return keyed.Answer == OpenAiAnswer.NeedsKey
            ? new CheckResult(false, $"The server at {found.BaseUrl} refused the API key.")
            : keyed.Result;
    }

    // Ordered: anything from Server up is an OpenAI-compatible server at that address.
    private enum OpenAiAnswer
    {
        None,
        Other,
        Server,
        NeedsKey,
    }

    private sealed record Probe(string BaseUrl, OpenAiAnswer Answer, CheckResult Result);

    private static async Task<Probe> ProbeOpenAiAsync(HttpClient http, string baseUrl, string? model, string? apiKey, TimeSpan limit, CancellationToken cancellationToken)
    {
        var uri = new Uri(baseUrl + "/models");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(limit);
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new Probe(baseUrl, OpenAiAnswer.NeedsKey, new CheckResult(false, $"The server at {baseUrl} answered {(int)response.StatusCode} {response.ReasonPhrase}."));
            if (!response.IsSuccessStatusCode)
                return new Probe(baseUrl, (int)response.StatusCode >= 500 ? OpenAiAnswer.Other : OpenAiAnswer.None,
                    new CheckResult(false, $"The server answered {(int)response.StatusCode} {response.ReasonPhrase} to {uri}."));
            string body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!IsModelList(body))
                return new Probe(baseUrl, OpenAiAnswer.None, new CheckResult(false, $"{uri} is not an OpenAI-compatible model list."));
            var listed = ModelListed(body, model);
            return new Probe(baseUrl, OpenAiAnswer.Server, listed.Ok ? listed with { Url = baseUrl } : listed);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new Probe(baseUrl, OpenAiAnswer.None, new CheckResult(false, $"The server at {baseUrl} did not answer: {e.GetBaseException().Message}"));
        }
    }

    private static bool IsModelList(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static CheckResult ModelListed(string json, string? model)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var ids = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray()
                    .Select(m => m.ValueKind == JsonValueKind.Object && m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                    .Where(i => i != null).ToList()
                : [];
            if (ids.Count == 0)
                return new CheckResult(false, "The server lists no models.");
            if (string.IsNullOrWhiteSpace(model))
                return new CheckResult(true, $"The server lists {ids[0]}, which Veronica will use.");
            return ids.Contains(model.Trim())
                ? new CheckResult(true, $"The server lists {model.Trim()}.")
                : new CheckResult(false, $"The server does not list {model.Trim()}; it lists {string.Join(", ", ids)}.");
        }
        catch (JsonException)
        {
            return new CheckResult(false, "The server's model list is not JSON.");
        }
    }
}
