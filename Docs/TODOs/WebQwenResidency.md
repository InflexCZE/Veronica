# WebQwen evicts and restores its own buffers

**Needs an owner decision.**

**Exists:** `Docs/Tech/VramSharing.md` decides when `wq.dll` evicts and restores its buffers, the submit assert, and that Dawn's residency management is off. Dawn has no public call for evicting buffers. A spike proved one way: `wq.dll` creates every storage buffer of 1 MB and more (not mappable, not mapped at creation) as its own D3D12 committed resource and imports it with the `SharedBufferMemoryD3D12Resource` feature (`wgpuDeviceImportSharedBufferMemory`, `wgpuSharedBufferMemoryCreateBuffer`, one `BeginAccess` that stays open). `webgpu_dawn.dll` exported the D3D12 device (`dawn::native::d3d12::GetD3D12Device`). Bonsai ran at full speed on the imported buffers; its output was not checked and the logit gate was not run.

**Missing:**
- Pick the mechanism that gives `wq.dll` its buffers' D3D12 resources: the import path above, or another.
- Release at the end of every engine operation and `MakeResident` at its start (`Source/WebQwen.Native/src/Engine.cpp`), on the engine thread after `onSubmittedWorkDone`.
- A load-phase hook at the end of the weight upload; the spike detected it from the engine's progress text (the first "shader" progress message).
- The submit check in `Submit` (`Source/WebQwen.Native/src/Gpu.cpp`): debug assert, error log, make resident.
- Disable the `use_d3d12_residency_management` toggle in `RequestDevice`.
- Release of the imported memory when a buffer is destroyed.
- Verify: the logit gate passes on imported buffers; decode stays at about 40 tok/s.
