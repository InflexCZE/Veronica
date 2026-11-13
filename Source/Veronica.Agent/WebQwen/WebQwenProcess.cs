using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Veronica.Agent.WebQwen;

public sealed record WebQwenLaunch(string ExePath, string ModelId, string? ModelsDir, int ContextWindowSize)
{
    public const int DefaultContextWindowSize = 50_000;

    public IReadOnlyList<string> Arguments()
    {
        List<string> arguments = ["serve", "--model", ModelId];
        if (ModelsDir != null)
            arguments.AddRange(["--models", ModelsDir]);
        arguments.AddRange(["--context", ContextWindowSize.ToString(CultureInfo.InvariantCulture)]);
        return arguments;
    }

    // An unusable value falls back to the default rather than failing: the chat still works, the log says why.
    public static int ContextWindowSizeFrom(string? setting, out string? problem)
    {
        problem = null;
        string value = setting?.Trim() ?? "";
        if (value.Length == 0)
            return DefaultContextWindowSize;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size > 0)
            return size;
        problem = $"{Settings.VERONICA_CONTEXT_SIZE} '{value}' is not a positive number of tokens, using {DefaultContextWindowSize}.";
        return DefaultContextWindowSize;
    }
}

// A running WebQwen.exe. It dies with this process: the job kills it if this process is
// killed, and it exits by itself when its stdin closes.
public sealed class WebQwenProcess
{
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(5);

    private readonly Process _process;

    private WebQwenProcess(Process process, WebQwenConnection connection)
    {
        _process = process;
        Connection = connection;
    }

    public WebQwenConnection Connection { get; }

    public static WebQwenProcess Start(WebQwenLaunch launch, Action<string> log)
    {
        var info = new ProcessStartInfo(launch.ExePath)
        {
            WorkingDirectory = Path.GetDirectoryName(launch.ExePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in launch.Arguments())
            info.ArgumentList.Add(argument);

        var process = Process.Start(info) ?? throw new WebQwenClosedException($"{launch.ExePath} did not start.");
        try
        {
            JobObject.Assign(process.Handle);
        }
        catch
        {
            process.Kill();
            process.Dispose();
            throw;
        }
        log($"Started {launch.ExePath} (pid {process.Id})");

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;
            // Runs on a pool thread, where an exception would take the Editor down.
            try
            {
                log(e.Data);
            }
            catch
            {
            }
        };
        process.BeginErrorReadLine();

        var connection = new WebQwenConnection(process.StandardOutput, process.StandardInput, log);
        return new WebQwenProcess(process, connection);
    }

    // Closes stdin so WebQwen exits by itself, and kills it if it does not.
    public void Dispose(Action<string> log)
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch
        {
        }
        _ = Task.Run(() =>
        {
            try
            {
                if (_process.WaitForExit(ExitGrace))
                {
                    log($"WebQwen exited with code {_process.ExitCode}");
                }
                else
                {
                    log("WebQwen did not exit, killing it");
                    _process.Kill();
                }
            }
            catch
            {
            }
            finally
            {
                _process.Dispose();
            }
        });
    }
}
