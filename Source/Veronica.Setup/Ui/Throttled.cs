using System.Diagnostics;
using Veronica.Setup.Core;

namespace Veronica.Setup.Ui;

// A download reports every buffer; the window only needs a few updates a second.
public static class Throttled
{
    private static readonly long Interval = Stopwatch.Frequency / 10;

    public static IProgress<DownloadProgress> Progress(Action<DownloadProgress> report)
    {
        var ui = new Progress<DownloadProgress>(report);
        long last = 0;
        string file = "";
        return new Forward(p =>
        {
            long now = Stopwatch.GetTimestamp();
            if (now - Interlocked.Read(ref last) < Interval && p.Done < p.Total && p.File == file)
                return;
            Interlocked.Exchange(ref last, now);
            file = p.File;
            ((IProgress<DownloadProgress>)ui).Report(p);
        });
    }

    private sealed class Forward(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
