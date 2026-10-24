using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Veronica.Agent.WebQwen;

public sealed record WebQwenLaunch(string ExePath, string ModelId, string? ModelsDir = null, int ContextWindowSize = 0);

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
        info.ArgumentList.Add("serve");
        info.ArgumentList.Add("--model");
        info.ArgumentList.Add(launch.ModelId);
        if (launch.ModelsDir != null)
        {
            info.ArgumentList.Add("--models");
            info.ArgumentList.Add(launch.ModelsDir);
        }
        if (launch.ContextWindowSize > 0)
        {
            info.ArgumentList.Add("--context");
            info.ArgumentList.Add(launch.ContextWindowSize.ToString(CultureInfo.InvariantCulture));
        }

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
