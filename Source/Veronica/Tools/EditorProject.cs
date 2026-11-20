using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia.Threading;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Core;
using Keen.VRage.Editor.Project;
using Keen.VRage.Library.Diagnostics;
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
                    return Unwrapped(() => work(Current));
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
            return Unwrapped(() => work(Current));
        });

    // Editor entry points reached by reflection wrap their failure; the model and the log need the real one.
    private static T Unwrapped<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (TargetInvocationException e) when (e.InnerException is { } inner)
        {
            Log.Default.Error($"[Veronica] Editor call failed: {inner}");
            ExceptionDispatchInfo.Throw(inner);
            throw;
        }
    }

    private static LoadedProject Current =>
        (VRageCore.HasValue ? VRageCore.Instance.Engine?.TryGet<ProjectLoaderComponent>()?.Current?.RootProject : null)
        ?? throw new EditError("No project is loaded in the Editor.");
}
