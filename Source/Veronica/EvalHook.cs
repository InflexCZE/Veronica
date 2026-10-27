using System.Globalization;
using System.Net.Sockets;
using Avalonia.Threading;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Eval;
using Veronica.Ui;

namespace Veronica;

// Eval runs (Docs/Tech/Eval.md): VERONICA_RUN_DIR records the chat, VERONICA_CONTROL_PORT lets a harness drive it.
internal static class EvalHook
{
    private static ControlServer? _server;

    public static void Attach(ChatPanelViewModel panel, string systemPrompt, IReadOnlyList<ToolDefinition> tools)
    {
        string dir = Environment.GetEnvironmentVariable("VERONICA_RUN_DIR")?.Trim() ?? "";
        string port = Environment.GetEnvironmentVariable("VERONICA_CONTROL_PORT")?.Trim() ?? "";
        if (dir.Length == 0 && port.Length == 0)
            return;

        RunTranscript? transcript = null;
        if (dir.Length > 0)
        {
            try
            {
                transcript = new RunTranscript(dir, ModelProvider.Describe(), systemPrompt, tools);
                Log.Default.WriteLine($"[Veronica] Eval transcript in '{LocalModel.AsciiOnly(dir)}'.");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log.Default.WriteLine($"[Veronica] Eval transcript not written: {LocalModel.AsciiOnly(e.Message)}");
            }
        }

        var run = new RunControl(new PanelControl(panel), transcript);
        panel.Run = run;

        if (port.Length == 0)
            return;
        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number is < 1 or > 65535)
        {
            Log.Default.WriteLine($"[Veronica] VERONICA_CONTROL_PORT '{LocalModel.AsciiOnly(port)}' is not a port, no control channel.");
            return;
        }
        try
        {
            _server = new ControlServer(number, run);
            Log.Default.WriteLine($"[Veronica] Eval control channel on 127.0.0.1:{number}.");
        }
        catch (SocketException e)
        {
            Log.Default.WriteLine($"[Veronica] Eval control channel not started on port {number}: {LocalModel.AsciiOnly(e.Message)}");
        }
    }

    private sealed class PanelControl(ChatPanelViewModel panel) : IChatControl
    {
        public Task<string?> SendAsync(string text) => Dispatcher.UIThread.InvokeAsync(() => panel.TrySend(text)).GetTask();

        public void Stop() => Dispatcher.UIThread.Post(panel.StopTurn);
    }
}
