# VRAM sharing between the Editor and WebQwen

The Editor (D3D12) and `WebQwen.exe` (Dawn on D3D12) run on one GPU. On the 8 GB target they do not fit together: Bonsai needs about 6.2 GB (5.2 GB weights, about 1 GB KV cache and scratch), the Editor 1-3 GB, and the desktop and other applications hold about 1 GB.

## How Windows behaves

- D3D12 residency is per allocation, not per access. Every allocation a process has not evicted is part of its working set, touched or not.
- When the working sets exceed VRAM, Windows does not swap an idle process out. It demotes the allocations that do not fit into system memory and they stay there. Lowering residency priority or stopping all submits does not change this.
- A partly demoted model decodes 7-20x slower (5.4 and 1.9 tok/s measured, against 39.7) while the Copy engine pages continuously.
- Dawn's residency management does not prevent it, enabled or disabled. Its external memory reservation is capped at half the budget and evicts nothing by itself.
- `IDXGIAdapter3::QueryVideoMemoryInfo` does not show demotion (`CurrentUsage` counts demoted allocations as local). The `GPU Process Memory` performance counters (`Dedicated Usage`, `Shared Usage`) do.
- `Evict` is lazy: nothing moves until another allocation needs the room. When everything fits, `Evict` followed by `MakeResident` costs about 0.2 ms.

## Strategy: ping-pong residency

Exactly one side keeps its working set resident at a time. The same path runs on every card.

- **Turn start:** the Editor stops submitting work on its engine device and evicts every engine heap and committed resource. WebQwen makes its buffers resident, then runs inference.
- **During the turn:** tool calls touch scene data only; the Editor stays frozen for the whole agent turn. The Editor UI renders on its own stack and stays live, so the chat streams.
- **Turn end:** WebQwen evicts its buffers. The Editor makes all its engine resources resident and resumes rendering.
- Nothing is destroyed or re-created; every handle stays valid on both sides.
- While evicted, the Editor must not submit any engine-device work: GPU access to an evicted resource faults.
- WebQwen evicts and restores its own allocations explicitly, covering every storage buffer of 1 MB and more (6166 of about 6200 MB for Bonsai).
- The evicted side lives in system memory: up to about 6.2 GB for the model plus 1-3 GB for the Editor.

## Model load

The load is split:

1. **Weight upload** (about 10 s for 5.2 GB from the local cache): the Editor is frozen and evicted, as in a turn.
2. **When the weights are done** (the engine starts compiling shader modules and the GPU is idle): WebQwen evicts its buffers and the Editor resumes.
3. **Shader compile** (about 27 s) runs with the Editor live. Allocations made from here on (about 0.85 GB) do not demote while the weights are evicted; WebQwen evicts again when the load ends. The rest of the KV cache and scratch (about 0.15 GB) is allocated in the first turn.

## Cost

RTX 3070 8 GB, about 1 GB held by the desktop, Bonsai:

| Editor VRAM | decode | turn start (WebQwen resident) | turn end (Editor's first frame) |
|---|---|---|---|
| 2 GB | 40.0 tok/s | 0.49 s | 0.54 s |
| 3 GB | 39.9 tok/s | 0.75 s | 0.83 s |

- Decode and prefill match WebQwen running alone. Editor frame times after resume match those before the turn.
- Each direction costs about 0.25 s per GB of overlap (Editor + model + others - VRAM). It is close to zero when everything fits.
- Split load: the Editor is frozen for the weight upload only (10 s); it is back about 0.9 s after that.
