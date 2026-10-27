using System.Diagnostics;
using System.Text;

namespace Veronica.Agent.Claude;

public sealed class ClaudeException(string message) : Exception(message);

// One running claude -p. Output is its stdout, one stream-json event per line.
internal sealed class ClaudeProcess : IDisposable
{
    private const int MaxErrorChars = 4000;

    private readonly Process _process;
    private readonly Task<string> _error;

    private ClaudeProcess(Process process)
    {
        _process = process;
        _error = ReadCappedAsync(process.StandardError);
    }

    // input is written to stdin, which is then closed.
    public static ClaudeProcess Start(IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, string input)
    {
        var start = new ProcessStartInfo(FindExecutable())
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        foreach (var (name, value) in environment)
            start.Environment[name] = value;

        var process = Process.Start(start) ?? throw new ClaudeException("Claude Code did not start.");
        var claude = new ClaudeProcess(process);
        try
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // Claude Code exited before reading its input; its exit code and stderr tell why.
        }
        return claude;
    }

    public TextReader Output => _process.StandardOutput;

    // Valid once WaitForExitAsync has completed.
    public string Error => _error.IsCompletedSuccessfully ? _error.Result : "";

    public async Task<int> WaitForExitAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);
        await _error.ConfigureAwait(false);
        return _process.ExitCode;
    }

    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already exited.
        }
    }

    public void Dispose() => _process.Dispose();

    // The native installer puts claude.exe in ~/.local/bin and on the user's PATH; an Editor started
    // before that PATH change does not see it there.
    private static string FindExecutable()
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
        throw new ClaudeException("Claude Code (claude.exe) was not found on PATH or in %USERPROFILE%\\.local\\bin. Install it and sign in by running 'claude' once.");
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            if (text.Length < MaxErrorChars)
                text.Append(buffer, 0, Math.Min(read, MaxErrorChars - text.Length));
        }
        return text.ToString();
    }
}
