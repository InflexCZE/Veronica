using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Veronica.Agent;

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

public sealed record CheckResult(bool Ok, string Message);

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
        string? exe = FindClaude();
        if (exe == null)
            return new CheckResult(false, "Claude Code (claude.exe) was not found on PATH or in %USERPROFILE%\\.local\\bin. Install it and sign in by running 'claude' once.");
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        info.ArgumentList.Add("auth");
        info.ArgumentList.Add("status");
        info.ArgumentList.Add("--json");
        using var process = Process.Start(info)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        string output;
        try
        {
            output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CheckResult(false, "'claude auth status' did not answer.");
        }
        return ClaudeStatus(output);
    }

    public static CheckResult ClaudeStatus(string json)
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
            return new CheckResult(false, "'claude auth status' gave no status; update Claude Code.");
        }
    }

    public static async Task<CheckResult> OpenAiAsync(HttpClient http, string? baseUrl, string? model, string? apiKey, CancellationToken cancellationToken)
    {
        string url = (baseUrl ?? "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(url + "/models", UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return new CheckResult(false, url.Length == 0 ? "No server URL is set." : $"'{url}' is not an http or https URL.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new CheckResult(false, $"The server answered {(int)response.StatusCode} {response.ReasonPhrase} to {uri}.");
            return ModelListed(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false), model);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return new CheckResult(false, $"The server at {url} did not answer: {e.GetBaseException().Message}");
        }
    }

    public static CheckResult ModelListed(string json, string? model)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var ids = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null).Where(i => i != null).ToList()
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

    // Like the Claude provider: the native installer puts claude.exe in ~/.local/bin, maybe not on this PATH yet.
    private static string? FindClaude()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"));
        foreach (var directory in directories)
        {
            try
            {
                string candidate = Path.Combine(directory.Trim().Trim('"'), "claude.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (ArgumentException)
            {
            }
        }
        return null;
    }
}
