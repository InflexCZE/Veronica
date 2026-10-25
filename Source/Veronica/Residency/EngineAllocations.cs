using System.Collections;
using System.Reflection;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Memory;
using Keen.VRage.Library.Memory.Pools;
using Vortice.Direct3D12;

namespace Veronica.Residency;

public sealed class EngineChangedException(string message) : Exception(message);

// The Editor's big VRAM allocations, found through Render12's own objects (Docs/Tech/VramSharing.md).
internal static class EngineAllocations
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static Roots? _roots;
    private static string? _error;

    // Must run before Render12 creates its pools: a pool reads its tracking flag once, when it is created.
    public static void TrackBorrows()
    {
        try
        {
            _roots = new Roots();
            PoolManager.Instance.TrackBorrowsForPoolType(_roots.TokenType);
            bool late = false;
            foreach (var (type, _) in PoolManager.Instance.ReadPools())
                late |= type == _roots.TokenType;
            Log.Default.WriteLine(late
                ? "[Veronica] Render12 created its GPU resource pools before Veronica loaded: the local model cannot free the Editor's VRAM."
                : "[Veronica] GPU resource pool tracking is on.");
        }
        catch (Exception e)
        {
            _roots = null;
            _error = e.Message;
            Log.Default.WriteLine($"[Veronica] Render12 is not the one Veronica knows: {e.Message}");
        }
    }

    // The engine's own wrapper: never disposed here.
    public static ID3D12Device Device =>
        (Get(Engine.CoreDeviceWrap, null) is { } deviceWrap ? Get(Engine.DeviceWrapD3DDevice, deviceWrap) as ID3D12Device : null)
        ?? throw new InvalidOperationException("Render12 has no device yet.");

    private static Roots Engine => _roots ?? throw new EngineChangedException($"Render12 is not the one Veronica knows: {_error}");

    // Render thread, with the engine idle. Each pageable owns a reference of its own: the engine
    // may dispose its object meanwhile, and these stay valid until disposed.
    public static Snapshot TakeSnapshot()
    {
        var roots = Engine;
        var device = Device;
        var snapshot = new Snapshot();

        try
        {
            foreach (var (type, pool) in PoolManager.Instance.ReadPools())
            {
                if (type != roots.TokenType)
                    continue;
                foreach (var token in roots.Borrowed(pool))
                    if (Get(roots.TokenResource, token) is { } resource)
                        roots.CollectFields(resource, snapshot, device);
            }

            if (Get(roots.CoreRasBuffers, null) is { } rasBuffers && Get(roots.RasPools, rasBuffers) is IEnumerable rasPools)
                foreach (var rasPool in rasPools.Cast<object>().ToList())
                    roots.CollectFields(rasPool, snapshot, device);

            if (Get(roots.CoreD3DHeap, null) is { } heapManager)
            {
                roots.CollectHeaps(Get(roots.DefaultBufferHeap, heapManager), snapshot, device);
                roots.CollectHeaps(Get(roots.DefaultTextureHeap, heapManager), snapshot, device);
            }

            if (Get(roots.CoreLocalLights, null) is { } localLights && Get(roots.ShadowTexturePool, localLights) is { } shadowPool)
                roots.CollectHeaps(Get(roots.ShadowMasksHeap, shadowPool), snapshot, device);

            return snapshot;
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
    }

    private static object? Get(FieldInfo field, object? owner) => field.GetValue(owner);

    private static object? Get(PropertyInfo property, object? owner) => property.GetValue(owner);

    internal sealed class Snapshot : IDisposable
    {
        private readonly HashSet<IntPtr> _seen = [];

        public List<ID3D12Pageable> Pageables { get; } = [];

        public long Bytes { get; private set; }

        public void Add(ID3D12Pageable engineObject, long bytes)
        {
            if (engineObject.NativePointer == IntPtr.Zero || !_seen.Add(engineObject.NativePointer))
                return;
            System.Runtime.InteropServices.Marshal.AddRef(engineObject.NativePointer);
            Pageables.Add(new ID3D12Pageable(engineObject.NativePointer));
            Bytes += bytes;
        }

        public void Dispose()
        {
            foreach (var pageable in Pageables)
                pageable.Dispose();
            Pageables.Clear();
        }
    }

    private sealed class Roots
    {
        private const string Render12 = "VRage.Render12";

        public readonly Type TokenType = RenderType("Keen.VRage.Render12.Resources.Utilities.GPUResourcePool+Token");
        public readonly PropertyInfo TokenResource;
        public readonly FieldInfo CoreDeviceWrap;
        public readonly FieldInfo CoreRasBuffers;
        public readonly FieldInfo CoreD3DHeap;
        public readonly FieldInfo CoreLocalLights;
        public readonly PropertyInfo DeviceWrapD3DDevice;
        public readonly FieldInfo RasPools;
        public readonly FieldInfo DefaultBufferHeap;
        public readonly FieldInfo DefaultTextureHeap;
        public readonly FieldInfo HeapContainerHeaps;
        public readonly FieldInfo ShadowTexturePool;
        public readonly FieldInfo ShadowMasksHeap;
        public readonly Type CommittedWrapType = RenderType("Keen.VRage.Render12.Core.D3DWraps.D3DCommittedResourceWrap");
        public readonly FieldInfo CommittedWrapResource;
        private readonly Dictionary<Type, FieldInfo[]> _pageableFields = [];

        public Roots()
        {
            TokenResource = TokenType.GetProperty("Resource", AnyInstance) ?? throw Missing(TokenType, "Resource");
            var core = RenderType("Keen.VRage.Render12.Core.CoreSystems");
            CoreDeviceWrap = StaticField(core, "DeviceWrap");
            CoreRasBuffers = StaticField(core, "RASBuffers");
            CoreD3DHeap = StaticField(core, "D3DHeap");
            CoreLocalLights = StaticField(core, "LocalLights");
            var deviceWrap = RenderType("Keen.VRage.Render12.Core.Device.DeviceWrap");
            DeviceWrapD3DDevice = deviceWrap.GetProperty("D3DDevice", AnyInstance) ?? throw Missing(deviceWrap, "D3DDevice");
            RasPools = Field(RenderType("Keen.VRage.Render12.Resources.BindableBuffers.RASBufferManager"), "_rasPools");
            var heapManager = RenderType("Keen.VRage.Render12.Resources.D3DHeapManager");
            DefaultBufferHeap = Field(heapManager, "_defaultBufferHeap");
            DefaultTextureHeap = Field(heapManager, "_defaultTextureHeap");
            HeapContainerHeaps = Field(RenderType("Keen.VRage.Render12.Resources.D3DHeapManager+HeapContainer"), "_heaps");
            ShadowTexturePool = Field(RenderType("Keen.VRage.Render12.LightingStage.LocalLightsManager"), "_texturePool");
            ShadowMasksHeap = Field(RenderType("Keen.VRage.Render12.LightingStage.LocalShadowTexturePool"), "_shadowMasksHeap");
            CommittedWrapResource = Field(CommittedWrapType, "_d3dResource");
            Field(typeof(ObjectPool<,>), "_trackedBorrowedObjects");
            Field(typeof(ObjectPool<,>), "_lock");
        }

        // A copy taken under the pool's own lock: workers borrow and return meanwhile.
        public List<object> Borrowed(IPoolBase pool)
        {
            var type = pool.GetType();
            var gate = Field(type, "_lock").GetValue(pool) ?? throw Missing(type, "_lock");
            lock (gate)
            {
                if (Field(type, "_trackedBorrowedObjects").GetValue(pool) is not IEnumerable borrowed)
                    throw new EngineChangedException("Render12 created its GPU resource pools before Veronica switched their tracking on.");
                return borrowed.Cast<object>().ToList();
            }
        }

        public void CollectHeaps(object? heapContainer, Snapshot snapshot, ID3D12Device device)
        {
            if (heapContainer != null && HeapContainerHeaps.GetValue(heapContainer) is IEnumerable heaps)
                foreach (var heap in heaps.Cast<object>().ToList())
                    CollectFields(heap, snapshot, device);
        }

        // Heaps, and committed resources through their wraps. A bare ID3D12Resource field is a
        // placed resource, which lives in a heap, or a swap chain buffer, which stays.
        public void CollectFields(object owner, Snapshot snapshot, ID3D12Device device)
        {
            foreach (var field in PageableFields(owner.GetType()))
            {
                switch (field.GetValue(owner))
                {
                    case ID3D12Heap heap when heap.NativePointer != IntPtr.Zero && InVideoMemory(heap.Description.Properties):
                        snapshot.Add(heap, (long)heap.Description.SizeInBytes);
                        break;
                    case { } wrap when wrap.GetType() == CommittedWrapType:
                        if (CommittedWrapResource.GetValue(wrap) is ID3D12Resource resource && InVideoMemory(resource))
                            snapshot.Add(resource, (long)device.GetResourceAllocationInfo(0, resource.Description).SizeInBytes);
                        break;
                }
            }
        }

        private FieldInfo[] PageableFields(Type type)
        {
            lock (_pageableFields)
            {
                if (_pageableFields.TryGetValue(type, out var fields))
                    return fields;
                var found = new List<FieldInfo>();
                for (var t = type; t != null; t = t.BaseType)
                {
                    foreach (var field in t.GetFields(AnyInstance | BindingFlags.DeclaredOnly))
                    {
                        var fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                        if (typeof(ID3D12Heap).IsAssignableFrom(fieldType) || fieldType == CommittedWrapType)
                            found.Add(field);
                    }
                }
                return _pageableFields[type] = found.ToArray();
            }
        }

        private static bool InVideoMemory(ID3D12Resource resource)
        {
            if (resource.NativePointer == IntPtr.Zero || resource.GetHeapProperties(out var properties, out _).Failure)
                return false;
            return InVideoMemory(properties);
        }

        private static bool InVideoMemory(HeapProperties properties) =>
            properties.Type == HeapType.Default
            || (properties.Type == HeapType.Custom && properties.MemoryPoolPreference == MemoryPool.L1);

        private static Type RenderType(string name) =>
            Type.GetType($"{name}, {Render12}") ?? throw new EngineChangedException($"Render12 has no type {name}.");

        private static FieldInfo Field(Type type, string name) =>
            type.GetField(name, AnyInstance) ?? throw Missing(type, name);

        private static FieldInfo StaticField(Type type, string name) =>
            type.GetField(name, AnyStatic) ?? throw Missing(type, name);

        private static EngineChangedException Missing(Type type, string member) =>
            new($"Render12 has no {type.FullName}.{member}.");
    }
}
