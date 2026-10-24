# GPU calls use the current device

**Exists:** in `Source/WebQwen.Native/src/Gpu.cpp` every `create*` call and the queue calls use `Gpu::device` / `Gpu::queue`, the most recently created device, not the `GPUDevice` the script calls them on. Device loss, errors and `destroy()` are already routed by device handle. `api.js` serializes operations, so one model load never overlaps another and the old device is unloaded first.

**Missing:** pass the device handle from `webgpu.js` into each native call (and use that device's queue) so a stale `GPUDevice` can never act on a newer one. Matters once more than one device can be live (two models, or a reload while an old pipeline still disposes). The same applies to the buffer list `wq.dll` evicts and restores (`Docs/Tech/VramSharing.md`): it belongs to a device, and is dropped when that device is lost or destroyed.
