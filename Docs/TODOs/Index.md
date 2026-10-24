# Veronica: TODOs

Work known to be missing or unfinished. One file per item: what exists, what is missing, where to start. An item that needs an owner decision says so. Delete the file when the item is done.

| item | area |
|---|---|
| [ShaderCache.md](ShaderCache.md) | WebQwen: persist Dawn's compiled pipelines |
| [EngineLoopPolling.md](EngineLoopPolling.md) | WebQwen: engine thread busy-polls while the GPU works (owner decision) |
| [ModelDownloads.md](ModelDownloads.md) | WebQwen: C# downloads of manifest, library and weights |
| [RequirementsCheck.md](RequirementsCheck.md) | WebQwen: can this machine run the model, including free VRAM |
| [InstallStrategy.md](InstallStrategy.md) | Where Veronica finds WebQwen.exe and the model (owner decision) |
| [AddTool.md](AddTool.md) | Remove the `add` test tool when the first Editor tool lands |
| [ContextFull.md](ContextFull.md) | Chat: full context window (owner decision) |
| [EngineHang.md](EngineHang.md) | Chat: no way out of a hung engine (owner decision) |
| [NativeStdout.md](NativeStdout.md) | WebQwen: native stdout writes would corrupt the protocol |
| [ModelRecordFields.md](ModelRecordFields.md) | WebQwen: model record fields beyond Bonsai's |
| [DeviceScopedCalls.md](DeviceScopedCalls.md) | WebQwen: GPU calls use the current device, not their own |
| [ReleaseLayout.md](ReleaseLayout.md) | WebQwen: release folder and publish |
| [SplitLoad.md](SplitLoad.md) | Split model load: Editor live during shader compile (owner decision) |
| [EditorResidency.md](EditorResidency.md) | Editor: freeze and evict the engine device around a turn |
| [VramSharingHardware.md](VramSharingHardware.md) | VRAM sharing measured on one NVIDIA card only |
| [VramSharingBigCards.md](VramSharingBigCards.md) | VRAM sharing: keep the Editor live when everything fits (owner decision) |
| [EditorVersionFloor.md](EditorVersionFloor.md) | Editor: oldest Editor build a release supports (owner decision) |
| [EditorTextOutside.md](EditorTextOutside.md) | Editor: font-fallback crash for text written outside our views |
| [EditorProjectUnload.md](EditorProjectUnload.md) | Editor: panel and menu stay live after Unload Project |
