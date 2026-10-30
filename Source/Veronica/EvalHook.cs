using System.Globalization;
using System.Net.Sockets;
using Avalonia.Threading;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;
using Veronica.Agent.Eval;
using Veronica.Ui;

namespace Veronica;

// Eval runs (Docs/Tech/Eval.md): VERONICA_RUN_DIR records every session, VERONICA_CONTROL_PORT lets a harness drive the chat.
internal static class EvalHook
{
    private static ControlServer? _server;
    private static RecordingProvider? _recording;

    private static string RunDir => Environment.GetEnvironmentVariable("VERONICA_RUN_DIR")?.Trim() ?? "";

    private static string Port => Environment.GetEnvironmentVariable("VERONICA_CONTROL_PORT")?.Trim() ?? "";

    public static IProvider Record(IProvider provider)
    {
        if (RunDir.Length == 0)
            return provider;
        Log.Default.WriteLine($"[Veronica] Eval transcripts in '{LocalModel.AsciiOnly(RunDir)}'.");
        return _recording = new RecordingProvider(provider, RunDir, ModelProvider.Describe());
    }

    public static void Attach(ChatPanelViewModel panel)
    {
        if (RunDir.Length == 0 && Port.Length == 0)
            return;

        var recording = _recording;
        var run = new RunControl(new PanelControl(panel), recording == null ? null : () => recording.WriteError);
        panel.Run = run;

        string port = Port;
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
