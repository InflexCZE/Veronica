using System.Reflection;
using Avalonia.Threading;
using Keen.VRage.Core;
using Keen.VRage.Editor.Utils;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Render.EngineComponents;
using Veronica.Ui;
using ContinuationQueue = Keen.VRage.Library.Threading.ContinuationQueue;

namespace Veronica.Residency;

// Freezes the Editor and evicts its VRAM for the local model, and brings it back
// (Docs/Tech/VramSharing.md). One freeze at a time: GpuShare runs free and restore in turn.
internal static class EditorResidency
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly TimeSpan ParkTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ResumePoll = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MakeResidentLogInterval = TimeSpan.FromSeconds(5);

    private static ParkedRender? _parked;
    private static DispatcherAppLoop? _stoppedLoop;

    public static Task FreeAsync() => Task.Run(FreeCoreAsync);

    public static Task RestoreAsync() => Task.Run(RestoreCoreAsync);

    // UI thread. Textures the Editor no longer needs are dropped instead of kept up to the
    // streaming budget, so its VRAM does not grow while the local model shares the GPU. Applied
    // again at every freeze: the Editor's own settings changes can turn caching back on.
    public static void DropUnusedTextures()
    {
        if (VRageCore.Instance.Engine?.TryGet<RenderEngineComponent>() is not { } renderEngine)
            return;
        var settings = renderEngine.RenderContracts.GetSettings();
        if (!settings.StreamingSettings.EnableCaching)
            return;
        settings.StreamingSettings = settings.StreamingSettings with { EnableCaching = false };
        Log.Default.WriteLine("[Veronica] Texture caching off while the local model is active.");
    }

    private static async Task FreeCoreAsync()
    {
        var render = new RenderSide();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DropUnusedTextures();
            StopEngineLoop();
            FrozenViewBadge.Show();
        });

        // The update loop is stopped first: it blocks the UI thread once the render thread is
        // 30 frames behind, and the parked render thread never catches up.
        var parked = new ParkedRender(render);
        _parked = parked;
        render.DefinitionsQueue.Enqueue(parked.Run);
        await parked.WaitParkedAsync(ParkTimeout).ConfigureAwait(false);
    }

    private static async Task RestoreCoreAsync()
    {
        try
        {
            if (Interlocked.Exchange(ref _parked, null) is { } parked)
                await parked.ResumeAsync().ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StartEngineLoop();
                FrozenViewBadge.Hide();
            });
        }
    }

    private static void StopEngineLoop()
    {
        var host = VRageCore.Instance;
        var driver = Property(host.GetType(), "Driver").GetValue(host) ?? throw new InvalidOperationException("The Editor has no project driver.");
        if (Property(driver.GetType(), "EngineLoop").GetValue(driver) is not DispatcherAppLoop loop)
            return;
        var timer = Field(typeof(DispatcherAppLoop), "_dispatcher").GetValue(loop) as DispatcherTimer
            ?? throw new EngineChangedException("The Editor's engine loop has no timer.");
        if (!timer.IsEnabled)
            return;
        loop.Stop();
        _stoppedLoop = loop;
    }

    private static void StartEngineLoop()
    {
        _stoppedLoop?.Start();
        _stoppedLoop = null;
    }

    private static RenderEngineComponent RenderEngine =>
        VRageCore.Instance.Engine?.TryGet<RenderEngineComponent>() ?? throw new InvalidOperationException("The Editor has no renderer.");

    private static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name, AnyInstance) ?? throw new EngineChangedException($"The Editor has no {type.FullName}.{name}.");

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, AnyInstance) ?? throw new EngineChangedException($"The Editor has no {type.FullName}.{name}.");

    // Render12's statics the freeze needs, read once per freeze.
    private sealed class RenderSide
    {
        public readonly ContinuationQueue DefinitionsQueue;
        public readonly object FrameDispatcher;
        public readonly MethodInfo FlushAllQueuesAndWaitCpu;
        private readonly object _resourcePool;
        private readonly FieldInfo _parallelWork;
        private readonly PropertyInfo _parallelWorkDone;
        private readonly object _concurrent;
        private readonly FieldInfo _continueLooping;

        public RenderSide()
        {
            var core = Type.GetType("Keen.VRage.Render12.Core.CoreSystems, VRage.Render12")
                ?? throw new EngineChangedException("Render12 has no CoreSystems.");
            if (Static(core, "DirectStorage") != null)
                throw new InvalidOperationException("The Editor streams textures with DirectStorage, which Veronica cannot pause: the local model cannot share the GPU with it.");
            var definitions = Static(core, "DefinitionPostProcesses") ?? throw new InvalidOperationException("Render12 is not initialized.");
            DefinitionsQueue = Field(definitions.GetType(), "OnDefinitionsChangedQueue").GetValue(definitions) as ContinuationQueue
                ?? throw new EngineChangedException("Render12's definitions queue is not a ContinuationQueue.");
            FrameDispatcher = Static(core, "FrameDispatcher") ?? throw new InvalidOperationException("Render12 is not initialized.");
            FlushAllQueuesAndWaitCpu = FrameDispatcher.GetType().GetMethod("FlushAllQueuesAndWaitCpu", AnyInstance, Type.EmptyTypes)
                ?? throw new EngineChangedException("Render12 has no FrameDispatcher.FlushAllQueuesAndWaitCpu().");
            _resourcePool = Static(core, "GPUResourcePool") ?? throw new InvalidOperationException("Render12 is not initialized.");
            _parallelWork = Field(_resourcePool.GetType(), "_parallelWork");
            _parallelWorkDone = Property(_parallelWork.FieldType, "IsCompleted");
            var engine = RenderEngine;
            _concurrent = Field(engine.GetType(), "_concurrent").GetValue(engine) ?? throw new InvalidOperationException("Render12 is not initialized.");
            _continueLooping = Field(_concurrent.GetType(), "ContinueLooping");
        }

        // The Editor is closing: it waits for the render thread to finish its loop.
        public bool Stopping => !(bool)_continueLooping.GetValue(_concurrent)!;

        // The worker that disposes and creates resources may still run when the render thread
        // parks; no new run starts while it is parked.
        public void WaitForResourceWorker(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!(bool)_parallelWorkDone.GetValue(_parallelWork.GetValue(_resourcePool))!)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Render12's resource worker did not finish.");
                Thread.Sleep(1);
            }
        }

        private static object? Static(Type type, string name) =>
            (type.GetField(name, AnyStatic) ?? throw new EngineChangedException($"Render12 has no CoreSystems.{name}.")).GetValue(null);
    }

    // The job that holds the render thread for a turn. It runs between frames, after Present,
    // where the engine's own shader recompile also waits for the GPU.
    private sealed class ParkedRender(RenderSide render)
    {
        private const int Queued = 0, Running = 1, Abandoned = 2;

        private readonly TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _resume = new();
        private int _state;

        public void Run()
        {
            if (Interlocked.CompareExchange(ref _state, Running, Queued) != Queued)
                return;

            EngineAllocations.Snapshot? snapshot = null;
            try
            {
                render.WaitForResourceWorker(WorkerTimeout);
                render.FlushAllQueuesAndWaitCpu.Invoke(render.FrameDispatcher, null);
                snapshot = EngineAllocations.TakeSnapshot();
                if (snapshot.Pageables.Count > 0)
                    EngineAllocations.Device.Evict(snapshot.Pageables.ToArray());
            }
            catch (Exception e)
            {
                snapshot?.Dispose();
                _parked.TrySetException(e is TargetInvocationException { InnerException: { } inner } ? inner : e);
                _resumed.TrySetResult();
                return;
            }
            Log.Default.WriteLine($"[Veronica] Editor evicted {snapshot.Pageables.Count} allocations, {snapshot.Bytes / (1024 * 1024)} MB.");
            _parked.TrySetResult();

            // The Editor closing waits for this thread, so closing ends the turn's freeze too.
            while (!_resume.Wait(ResumePoll) && !render.Stopping)
            {
            }

            // Rendering with any of these evicted faults the GPU, so the thread stays here until
            // they are back.
            var lastLog = DateTime.MinValue;
            while (snapshot.Pageables.Count > 0 && !TryMakeResident(snapshot, ref lastLog) && !render.Stopping)
                Thread.Sleep(ResumePoll);
            snapshot.Dispose();
            _resumed.TrySetResult();
        }

        private static bool TryMakeResident(EngineAllocations.Snapshot snapshot, ref DateTime lastLog)
        {
            try
            {
                EngineAllocations.Device.MakeResident(snapshot.Pageables.ToArray());
                return true;
            }
            catch (Exception e)
            {
                if (DateTime.UtcNow - lastLog > MakeResidentLogInterval)
                {
                    Log.Default.Error($"[Veronica] Making the Editor's VRAM resident again failed, retrying: {e.Message}");
                    lastLog = DateTime.UtcNow;
                }
                return false;
            }
        }

        public async Task WaitParkedAsync(TimeSpan timeout)
        {
            var finished = await Task.WhenAny(_parked.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (finished != _parked.Task && Interlocked.CompareExchange(ref _state, Abandoned, Queued) == Queued)
                throw new TimeoutException("The Editor's render thread did not pick up the freeze.");
            await _parked.Task.ConfigureAwait(false);
        }

        public Task ResumeAsync()
        {
            _resume.Set();
            return Volatile.Read(ref _state) == Running ? _resumed.Task : Task.CompletedTask;
        }
    }
}
