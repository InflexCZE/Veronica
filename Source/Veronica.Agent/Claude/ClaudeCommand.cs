using System.Diagnostics;
using System.Text;

namespace Veronica.Agent.Claude;

// Runs whatever 'claude' the user's terminal runs: cmd.exe resolves it through PATH and PATHEXT, so a native
// claude.exe and npm's claude.cmd both work, and an install Veronica does not know about wins over none.
public static class ClaudeCommand
{
    // cmd.exe's errorlevel for a command it cannot find. 'cmd /c' alone exits with 1 for it; the trailing
    // 'exit /b' hands the errorlevel out as the exit code.
    public const int NotFoundExitCode = 9009;

    public const string NotFoundMessage = "Claude Code ('claude') was not found on PATH. Install it and sign in by running 'claude' once in a terminal; if you just installed it, restart this program.";

    public static ProcessStartInfo StartInfo(IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"/d /s /c \"{CommandLine(arguments)} & exit /b\"",
        };
        return info;
    }

    // Every argument is quoted, so cmd.exe treats & | < > ^ in it as text. A quote inside an argument would end
    // that protection, and npm's claude.cmd passes the line through cmd.exe a second time, so none is allowed.
    public static string CommandLine(IEnumerable<string> arguments)
    {
        var line = new StringBuilder("claude");
        foreach (var argument in arguments)
        {
            if (argument.Contains('"'))
                throw new ArgumentException($"A Claude Code argument cannot contain a quote: {argument}", nameof(arguments));
            int trailing = argument.Length - argument.TrimEnd('\\').Length;
            line.Append(" \"").Append(argument).Append('\\', trailing).Append('"');
        }
        return line.ToString();
    }
}
