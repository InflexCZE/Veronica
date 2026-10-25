# VRAM sharing between the Editor and WebQwen

The Editor (D3D12) and `WebQwen.exe` (Dawn on D3D12) run on one GPU. On the 8 GB target they do not fit together: Bonsai needs about 6.2 GB (5.2 GB weights, about 1 GB KV cache and scratch), the Editor 1-3 GB, and the desktop and other applications hold about 1 GB.

## How Windows behaves

- D3D12 residency is per allocation, not per access. Every allocation a process has not evicted is part of its working set, touched or not.
- When the working sets exceed VRAM, Windows does not swap an idle process out. It demotes the allocations that do not fit into system memory and they stay there. Lowering residency priority or stopping all submits does not change this.
- A partly demoted model decodes 7-20x slower (5.4 and 1.9 tok/s measured, against 39.7) while the Copy engine pages continuously.
- Dawn's residency management does not prevent it, enabled or disabled. Its external memory reservation is capped at half the budget and evicts nothing by itself.
- `IDXGIAdapter3::QueryVideoMemoryInfo` does not show demotion (`CurrentUsage` counts demoted allocations as local). The `GPU Process Memory` performance counters (`Dedicated Usage`, `Shared Usage`) do.
- `Evict` is lazy: nothing moves until another allocation needs the room. When everything fits, `Evict` followed by `MakeResident` costs about 0.2 ms.
- Budgets are not a 50:50 split. The foreground process keeps a budget of about 81% of the available VRAM, whatever the others use. A windowless background process gets roughly the rest. The budgets add up to more than physical VRAM.
- No hint makes Windows prefer one process over another:
  - `IDXGIAdapter3::SetVideoMemoryReservation` is accepted but capped at about half the budget, and demotion is unchanged.
  - `D3DKMTSetProcessSchedulingPriorityClass` (WebQwen HIGH, Editor IDLE) changes nothing.
  - D3D12 residency priorities act only within one process. With both devices in one process (model MAXIMUM, Editor MINIMUM during a turn), the running model slowly pushes the idle Editor out, over about 17 s of inference. Swapping the priorities back never returns the Editor's memory while the model sits idle: Editor frames stayed about 25x slower for 20 s and more.
- Explicit `Evict` / `MakeResident` is the only mechanism found that moves memory between the two sides on demand.

## Strategy: ping-pong residency

Exactly one side keeps its working set resident at a time. The same path runs on every card.

- **Turn start:** the Editor stops submitting work on its engine device and evicts most of its VRAM (see Editor residency). WebQwen makes its buffers resident, then runs inference.
- **During the turn:** tool calls touch scene data only; the Editor stays frozen for the whole agent turn. The Editor UI renders on its own stack and stays live, so the chat streams.
- **Turn end:** WebQwen evicts its buffers. The Editor makes all its engine resources resident and resumes rendering.
- Nothing is destroyed or re-created; every handle stays valid on both sides.
- While evicted, the Editor must not submit any engine-device work: GPU access to an evicted resource faults.
- WebQwen evicts and restores its own large buffers explicitly: every storage buffer of 1 MB and more that is not mappable (6166 of about 6200 MB for Bonsai).
- The evicted side lives in system memory: up to about 6.2 GB for the model plus 1-3 GB for the Editor.

## WebQwen residency

- `webgpu_dawn.dll` owns the large buffers: it creates them as its own D3D12 committed resources imported into Dawn, and evicts or restores a device's set on request. It is the only binary that sees D3D12. Dawn keeps imported memory until the device is released, even after the buffer is released, so a device's set only grows.
- `wq.dll` decides when: every engine operation (load, chat, reset, score) ends by waiting for the GPU queue to go idle and evicting its buffers, before the operation's done callback. A done callback therefore means WebQwen's VRAM is released, and Veronica needs no extra message for it.
- Every engine operation starts with a synchronous `MakeResident` of its buffers.
- Between the steps of one agent turn (tool calls), WebQwen evicts and makes resident again while the Editor stays frozen. This costs nothing because `Evict` is lazy.
- GPU work queued while the buffers are released (a submit or a buffer write) is a bug. `wq.dll` asserts in debug builds and logs an error in every build, then makes the buffers resident so the work cannot fault the GPU.
- The eviction runs in the queue-idle callback itself, and waits again if work was queued since the release started. Destroying a device evicts its buffers at once, since Dawn keeps their memory until the device is released.
- Dawn's own residency management is disabled (`use_d3d12_residency_management` off).
- Lazy `Evict` is measured behaviour, not documented. The documentation says only that `Evict` "enables page-out", that `MakeResident` and `Evict` are reference-counted, and that `Evict` must follow the GPU's last use of the object.

## Editor residency

`Veronica.dll` freezes and evicts the Editor without engine changes and without D3D12 hooks. It finds the Editor's allocations through Render12's own managed objects.

### What the Editor holds

A freshly loaded Editor holds about 2 GB, measured with `MemoryHierarchy.DumpGPUResources`:

- Most of it is fixed render-pipeline memory: raytracing, light clustering, per-frame render targets, shadow maps, and hologram and probe targets.
- The only eager asset loads are all the particle atlases (about 300 MB) and the skybox (128 MB).
- Texture streaming may grow to about 5 GB. Its budget follows the DXGI budget, has a 2 GB floor, and keeps textures it no longer needs (caching).

### Finding the allocations

- D3D12 cannot list a device's allocations, so Veronica walks the engine's owners instead.
- **Pool tracking:** almost every big allocation is borrowed from Render12's `GPUResourcePool`, whose pools can keep a set of borrowed items.
  - The plugin's constructor calls `PoolManager.Instance.TrackBorrowsForPoolType(GPUResourcePool+Token)`.
  - It runs about 4.7 s before Render12 creates its device. Tracking is fixed when each pool is created, so a later call would track nothing.
- **Other roots:** the raytracing structure pools (`CoreSystems.RASBuffers`) and the placed-resource heap containers (`CoreSystems.D3DHeap`'s buffer and texture heaps, and the local-light shadow-mask heaps).
- **What is evicted:**
  - every `ID3D12Heap`, and every committed resource held in a `D3DCommittedResourceWrap`, found in those objects;
  - default-heap memory only, so upload and readback memory, already in system memory, is skipped;
  - no size threshold;
  - placed resources are evicted through their heaps.
- The walk covers about 88% of the Editor's VRAM. The rest is driver and shader memory, the swap chain, and descriptor and query heaps.
- A missing type, field or lock means the engine changed: the turn fails with an error. Every turn logs the MB evicted.

### Freeze

- The Editor freezes only for WebQwen work: from the start to the end of each agent turn, and for a model load until it completes, even when the user stops the turn that started it.
- **Turn start:**
  1. On the UI thread, stop updates from reaching the render thread. An update blocks the UI thread once the render thread is 30 frames behind.
     - Stop the Editor's engine update loop. The render thread keeps looping without updates.
     - Disable the job that commits each update's frame to the render thread. The Editor also ticks the scene outside the loop, for example when a content push puts an open asset preview on hold, so stopping the loop alone does not stop the frames. The ticks still run; their render work reaches the render thread after the turn.
  2. Queue a job on the render thread through `CoreSystems.DefinitionPostProcesses.OnDefinitionsChangedQueue`. Render12 runs that queue every frame, after Present and before message processing and Draw. The engine's own shader recompile waits for the GPU at the same point.
  3. That job:
     - waits for Render12's resource worker (`GPUResourcePool._parallelWork`), which disposes and creates resources and may still be running;
     - waits for the engine queues to go idle (`FrameDispatcher.FlushAllQueuesAndWaitCpu`);
     - takes the snapshot and evicts it;
     - blocks until the turn ends.
  4. If the job has not run within 10 s, the turn fails.
- **DirectStorage:** when the Editor uses it, the turn fails. Its workers write straight into GPU textures outside the engine's fences, and nothing can pause them. It is off in the Editor today.
- **The snapshot** AddRefs every object it evicts and releases them after the restore. An object the engine disposes during the turn therefore stays valid until then.
- **Turn end:** after WebQwen's last engine operation has released its VRAM, make the snapshot resident, release it, unblock the render thread, enable the commit job, and restart the update loop.
- Tool calls change scene data on the UI thread during the turn. Their effects render on the first frame after the turn.
- Objects created during the turn are not in the snapshot and stay resident.

### Texture growth

While WebQwen is the active provider, texture caching is off: textures that are no longer needed are dropped instead of kept up to the streaming budget.

## Model load

The load is split: the Editor is frozen and evicted for the weight upload (about 10 s for 5.2 GB from the local cache) and live for the shader compile that follows (about 27 s). How WebQwen and the Editor coordinate the split is not designed yet. Until it is, a load is one engine operation: the Editor stays frozen for all of it, and WebQwen releases its VRAM when the load completes.

## Cost

RTX 3070 8 GB, about 1 GB held by the desktop, Bonsai:

| Editor VRAM | decode | turn start (WebQwen resident) | turn end (Editor's first frame) |
|---|---|---|---|
| 2 GB | 40.0 tok/s | 0.49 s | 0.54 s |
| 3 GB | 39.9 tok/s | 0.75 s | 0.83 s |

- Decode and prefill match WebQwen running alone. Editor frame times after resume match those before the turn.
- Each direction costs about 0.25 s per GB of overlap (Editor + model + others - VRAM). It is close to zero when everything fits.
- Measured with the release at the end of every engine operation, as implemented, and with the spike's split load. In the split load the Editor was back about 0.9 s after the weight upload.
