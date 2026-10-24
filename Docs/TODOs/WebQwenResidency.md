# WebQwen evicts and restores its own buffers

**Needs an owner decision.**

**Exists:** `Docs/Tech/VramSharing.md` decides that WebQwen evicts its buffers at turn end and makes them resident at turn start. Dawn has no public call for this. A spike proved one way: `wq.dll` creates every storage buffer of 1 MB and more (not mappable, not mapped at creation) as its own D3D12 committed resource and imports it with the `SharedBufferMemoryD3D12Resource` feature (`wgpuDeviceImportSharedBufferMemory`, `wgpuSharedBufferMemoryCreateBuffer`, one `BeginAccess` that stays open). `webgpu_dawn.dll` exported the D3D12 device (`dawn::native::d3d12::GetD3D12Device`). Bonsai ran at full speed on the imported buffers; its output was not checked and the logit gate was not run.

**Missing:**
- Pick the mechanism: the import path above, or another.
- C API calls for evict and make-resident, mirrored in `Source/WebQwen/Interop/Wq.cs`, run on the engine thread once the queue is idle (`onSubmittedWorkDone`), including the mid-load evict when the weights are done.
- A load progress event that marks the end of the weight upload.
- Release of the imported memory when a buffer is destroyed.
- Verify: the logit gate passes on imported buffers; decode stays at about 40 tok/s.
