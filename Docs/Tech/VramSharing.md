# VRAM sharing

The Editor (D3D12) and the local model runtime (Dawn on D3D12) run on one GPU. On an 8 GB card they do not fit together: a model of about 6 GB (weights, KV cache, scratch), the Editor's 1-3 GB, and about 1 GB held by the desktop and other applications.

## Windows constraints

These measured behaviours rule out every softer approach:

- D3D12 residency is per allocation, not per access: every allocation a process has not evicted is in its working set, touched or not.
- When the working sets exceed VRAM, Windows does not swap an idle process out. It demotes the allocations that do not fit into system memory, and they stay there. A partly demoted model decodes 7-20x slower while the copy engine pages continuously.
- The foreground process keeps most of the budget, whatever the others use.
- No hint makes Windows prefer one process: video memory reservations, process scheduling priority and Dawn's own residency management all leave demotion unchanged. D3D12 residency priorities act only within one process, and even there a running model slowly pushes an idle Editor out and never gives the memory back.
- Demotion is invisible to `QueryVideoMemoryInfo`; the `GPU Process Memory` performance counters show it.
- Explicit `Evict` / `MakeResident` is the only mechanism that moves memory between the two sides on demand. `Evict` is lazy (measured, not documented): nothing moves until another allocation needs the room, so an evict and restore that is not needed costs almost nothing.

## Ping-pong residency

Exactly one side keeps its working set resident at a time. The same path runs on every card.

- **Turn start:** the Editor stops submitting work on its engine device and evicts its VRAM. The runtime makes its buffers resident and runs inference.
- **During the turn:** the Editor's engine stays frozen for the whole turn. Tool calls change scene data only; their effects render on the first frame after the turn. The Editor UI renders on its own stack and stays live, so the chat streams.
- **Turn end:** the runtime evicts its buffers; the Editor makes its resources resident and resumes.
- Nothing is destroyed or re-created; every handle stays valid on both sides. The evicted side lives in system memory.
- While evicted, the Editor must not submit engine-device work: GPU access to an evicted resource faults.
- The freeze applies only to the local model. Other providers run elsewhere and the Editor keeps its VRAM.
- The provider holds the Editor's VRAM for each whole turn and for each model load until it completes, even when the user stops the turn that started the load. Holds are counted: the first frees the Editor's VRAM, the last gives it back.

## Runtime residency

- `webgpu_dawn.dll` owns the large buffers as its own D3D12 resources imported into Dawn, and evicts or restores a device's set on request. It is the only binary that sees D3D12. Dawn keeps imported memory until the device is released, so a device's set only grows and destroying a device evicts its set at once.
- `wq.dll` decides when: every engine operation starts by making its buffers resident and ends by waiting for the GPU queue to go idle and evicting them, before its done callback. A done callback therefore means the runtime's VRAM is released, and the plugin needs no extra message for it.
- Dawn's own D3D12 residency management is off, so it never makes resources resident or evicts them behind the runtime's explicit residency.
- Between the rounds of one turn the runtime evicts and restores while the Editor stays frozen; lazy `Evict` makes that free.
- GPU work queued while the buffers are released is a bug: it is logged in every build and asserted in debug, and the buffers are made resident so the work cannot fault.

## Editor residency

The plugin freezes and evicts the Editor without engine changes and without D3D12 hooks.

- **Finding the allocations:** D3D12 cannot list a device's allocations, so the plugin walks Render12's own owners: borrow tracking on its resource pools, plus the raytracing structure pools and the placed-resource heaps. Borrow tracking is fixed when each pool is created, so it is switched on in the plugin's constructor, before Render12 creates its device. The walk covers most of the Editor's VRAM; the rest is driver, shader and swap-chain memory.
- Only default-heap memory is evicted; upload and readback memory is already in system memory. Placed resources go through their heaps.
- A missing engine type, field or lock means the engine changed: the turn fails rather than guess.
- **Freezing the render thread:** stopping the engine update loop alone does not stop frames, since the Editor also ticks the scene outside the loop. The job that commits each update's frame to the render thread is disabled too.
- The eviction runs as a job on the render thread, at the point in the frame where the engine's own shader recompile waits for the GPU. It waits for Render12's resource worker and for the engine queues to go idle, evicts, and blocks the render thread until the turn ends. A job that never gets to run fails the turn.
- **DirectStorage** writes straight into GPU textures outside the engine's fences and cannot be paused, so a turn fails when the Editor uses it. The Editor has it off today.
- **The snapshot** holds a reference to every object it evicts until the restore, so an object the engine disposes during the turn stays valid. Objects created during the turn stay resident.
- **Texture growth:** while the local model is the provider, texture caching is turned off at every freeze, so streaming drops textures it no longer needs instead of growing toward its budget.

## Model load

The load is split: the Editor is frozen and evicted for the weight upload and live for the shader compile that follows, which needs no VRAM and is most of the load. How the runtime and the Editor coordinate the split is not designed yet (`Docs/TODOs/SplitLoad.md`); until it is, a load is one engine operation and the Editor stays frozen for all of it.

## Cost

Each direction costs about 0.25 s per GB of overlap (Editor + model + others - VRAM), close to zero when everything fits. Decode speed during a turn matches the runtime running alone, and the Editor's frame times after a turn match those before it.
