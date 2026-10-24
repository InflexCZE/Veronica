# Dawn shader cache

**Exists:** `Docs/Tech/WebQwen.md` decides that C# stores Dawn's compiled shader cache so pipelines compile once per machine. Nothing implements it: every start compiles all pipelines again (Bonsai: 217 shader modules, about 30-40 s of a 35-48 s load on an RTX 3070).

**Missing:**
- `wq.dll`: chain a `WGPUDawnCacheDeviceDescriptor` (`dawn/webgpu.h`: `isolationKey`, load and store callback infos) into the device descriptor in `RequestDevice` (`Source/WebQwen.Native/src/Gpu.cpp`).
- C API: load/store callbacks on `wq_host` (key bytes, value bytes), mirrored in `Source/WebQwen/Interop/Wq.cs` and served by C# from a cache folder.
- Isolation key: at least adapter (vendor/device/driver) and the model library hash, so a driver or library change does not hit stale entries.
- Verify: second start loads the model markedly faster; the logit gate still passes.
