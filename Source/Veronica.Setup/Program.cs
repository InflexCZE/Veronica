using Avalonia;

namespace Veronica.Setup;

public static class Program
{
    public const string UpdateArgument = "--update";

    public const string SelfUpdatedArgument = "--self-updated";

    // Followed by the path of the exe this one was copied from, which it deletes once that has exited.
    public const string ReplacesArgument = "--replaces";

    [STAThread]
    public static int Main(string[] args)
    {
        bool update = args.Contains(UpdateArgument, StringComparer.OrdinalIgnoreCase);
        bool selfUpdated = args.Contains(SelfUpdatedArgument, StringComparer.OrdinalIgnoreCase);
        int replaces = Array.FindIndex(args, a => string.Equals(a, ReplacesArgument, StringComparison.OrdinalIgnoreCase));
        string? original = replaces >= 0 && replaces + 1 < args.Length ? args[replaces + 1] : null;
        string root = Path.GetDirectoryName(Environment.ProcessPath)!;
        // The instance that started this one is still closing; give it time to let go.
        using var instance = SingleInstance.TryAcquire(root, selfUpdated || original != null ? TimeSpan.FromSeconds(30) : TimeSpan.Zero);
        if (instance == null)
        {
            SingleInstance.Notify(root, update);
            return 0;
        }
        if (original != null && string.Equals(Path.GetDirectoryName(Path.GetFullPath(original)), root, StringComparison.OrdinalIgnoreCase))
            DeleteSoon(original);
        App.StartUpdate = update;
        App.SelfUpdated = selfUpdated;
        App.Instance = instance;
        return AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }

    private static void DeleteSoon(string path)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(250);
            }
        }
    }
}
