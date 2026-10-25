using Avalonia.Threading;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Core;
using Keen.VRage.Editor.Project;
using Veronica.Editing;

namespace Veronica.Tools;

// The loaded project, through the engine the Editor hosts.
public static class EditorProject
{
    private static readonly TimeSpan BusyWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BusyRetry = TimeSpan.FromMilliseconds(500);

    // Project state lives on the UI thread; tools run on a worker. Work that finds its file busy runs
    // again until the Editor lets go, waiting here rather than on the UI thread.
    public static async Task<T> RunAsync<T>(Func<LoadedProject, T> work, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + BusyWait;
        while (true)
        {
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return work(Current);
                }).GetTask();
            }
            catch (ItemBusy) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(BusyRetry, cancellationToken);
            }
        }
    }

    // For engine work that awaits, such as asset generators: it continues on the UI thread without blocking it.
    public static Task<T> RunAsync<T>(Func<LoadedProject, Task<T>> work, CancellationToken cancellationToken) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return work(Current);
        });

    private static LoadedProject Current =>
        (VRageCore.HasValue ? VRageCore.Instance.Engine?.TryGet<ProjectLoaderComponent>()?.Current?.RootProject : null)
        ?? throw new EditError("No project is loaded in the Editor.");
}
