using Avalonia;
using Veronica.Setup.Core;
using Veronica.Setup.Ui;

namespace Veronica.Setup;

public static class Program
{
    public const string UpdateArgument = "--update";

    public const string SelfUpdatedArgument = "--self-updated";

    // The first run's copy in the chosen install folder: start installing and open past Welcome.
    public const string InstallArgument = "--install";

    [STAThread]
    public static int Main(string[] args)
    {
        bool update = args.Contains(UpdateArgument, StringComparer.OrdinalIgnoreCase);
        bool selfUpdated = args.Contains(SelfUpdatedArgument, StringComparer.OrdinalIgnoreCase);
        bool install = args.Contains(InstallArgument, StringComparer.OrdinalIgnoreCase);
        string root = Path.GetDirectoryName(Environment.ProcessPath)!;
        var layout = new InstallLayout(root);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => SetupHost.Log(layout, $"Crashed: {e.ExceptionObject}");
        // The instance that started this one is still closing; give it time to let go.
        using var instance = SingleInstance.TryAcquire(root, selfUpdated || install ? TimeSpan.FromSeconds(30) : TimeSpan.Zero);
        if (instance == null)
        {
            SingleInstance.Notify(root, update);
            return 0;
        }
        App.StartUpdate = update;
        App.SelfUpdated = selfUpdated;
        App.StartInstall = install;
        App.Instance = instance;
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
    }
}
