using System.Diagnostics;
using System.Net.Http.Headers;
using Keen.VRage.Library.Diagnostics;
using Veronica.Agent;

namespace Veronica;

// Once per Editor start, an installed Veronica looks for a newer release and offers Veronica.exe to update.
internal static class UpdateCheck
{
    private static bool _started;

    // show: any thread.
    public static void Start(Action<string, Action> show)
    {
        if (_started || !Install.IsInstalled || Install.Version is not { } current || Install.SetupExe is not { } setup)
            return;
        _started = true;
        string url = Install.Settings.GetTrimmed(Settings.VERONICA_RELEASES_URL) ?? Settings.DefaultReleasesUrl;
        _ = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Veronica", current.ToString()));
                var release = Release.Parse(await http.GetStringAsync(url).ConfigureAwait(false));
                if (!(release.Version > current))
                    return;
                Log.Default.WriteLine($"[Veronica] Veronica {release.Version} is available (running {current}).");
                show($"Veronica {release.Version} is available. Update starts Veronica.exe, which updates once you close the Editor.", () => RunSetup(setup));
            }
            catch (Exception e)
            {
                Log.Default.WriteLine($"[Veronica] Update check failed: {LocalModel.AsciiOnly(e.Message)}");
            }
        });
    }

    private static void RunSetup(string setup)
    {
        try
        {
            Process.Start(new ProcessStartInfo(setup) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(setup)!, ArgumentList = { "--update" } });
        }
        catch (Exception e)
        {
            Log.Default.WriteLine($"[Veronica] Starting {LocalModel.AsciiOnly(setup)} failed: {LocalModel.AsciiOnly(e.Message)}");
        }
    }
}
