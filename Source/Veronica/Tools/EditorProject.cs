using Avalonia.Threading;
using Keen.VRage.ContentPipeline.Project.Structure;
using Keen.VRage.Core;
using Keen.VRage.Editor.Project;
using Veronica.Editing;

namespace Veronica.Tools;

// The loaded project, through the engine the Editor hosts.
public static class EditorProject
{
    // Project state lives on the UI thread; tools run on a worker.
    public static Task<T> RunAsync<T>(Func<LoadedProject, T> work, CancellationToken cancellationToken) =>
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return work(Current);
        }).GetTask();

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
