# Veronica: TODOs

Work known to be missing or unfinished. One file per item: what exists, what is missing, where to start. An item that needs an owner decision says so. Delete the file when the item is done.

| item | area |
|---|---|
| [ShaderCache.md](ShaderCache.md) | WebQwen: persist Dawn's compiled pipelines |
| [EngineLoopPolling.md](EngineLoopPolling.md) | WebQwen: engine thread busy-polls while the GPU works (owner decision) |
| [ModelDownloads.md](ModelDownloads.md) | WebQwen: C# downloads of manifest, library and weights |
| [RequirementsCheck.md](RequirementsCheck.md) | WebQwen: can this machine run the model, including free VRAM |
| [InstallStrategy.md](InstallStrategy.md) | Where Veronica finds WebQwen.exe and the model (owner decision) |
| [ContextFull.md](ContextFull.md) | Chat: full context window (owner decision) |
| [EngineHang.md](EngineHang.md) | Chat: no way out of a hung engine (owner decision) |
| [NativeStdout.md](NativeStdout.md) | WebQwen: native stdout writes would corrupt the protocol |
| [ModelRecordFields.md](ModelRecordFields.md) | WebQwen: model record fields beyond Bonsai's |
| [DeviceScopedCalls.md](DeviceScopedCalls.md) | WebQwen: GPU calls use the current device, not their own |
| [ReleaseLayout.md](ReleaseLayout.md) | WebQwen: release folder and publish |
| [SplitLoad.md](SplitLoad.md) | Split model load: Editor live during shader compile (owner decision) |
| [EditorResidency.md](EditorResidency.md) | Editor: freeze and evict around a turn, edge cases not yet tested live |
| [ReloadDuringTurn.md](ReloadDuringTurn.md) | Editor: a project reload during a turn blocks the UI until the turn ends |
| [VramSharingHardware.md](VramSharingHardware.md) | VRAM sharing measured on one NVIDIA card only |
| [VramSharingBigCards.md](VramSharingBigCards.md) | VRAM sharing: keep the Editor live when everything fits (owner decision) |
| [EditorVersionFloor.md](EditorVersionFloor.md) | Editor: oldest Editor build a release supports (owner decision) |
| [EditorTextOutside.md](EditorTextOutside.md) | Editor: font-fallback crash for text written outside our views |
| [EditorProjectUnload.md](EditorProjectUnload.md) | Editor: panel and menu stay live after Unload Project |
| [EditorUndo.md](EditorUndo.md) | Editor tools: agent edits are not Ctrl-Z steps (owner decision) |
| [ValidationTool.md](ValidationTool.md) | Editor tools: agent cannot see validation issues |
| [EditorSkills.md](EditorSkills.md) | Editor tools: no skill checklists for the model (owner decision) |
| [ToolRewards.md](ToolRewards.md) | Chat: no tool earns a reward; round budget raised to 50 meanwhile (owner decision) |
| [FieldPathKeys.md](FieldPathKeys.md) | Editor tools: some dictionary keys print in a form paths cannot address |
| [ConditionalWritability.md](ConditionalWritability.md) | Editor tools: conditional read-only rules of the inspector are ignored |
| [SamePathAcrossProjects.md](SamePathAcrossProjects.md) | Editor tools: one path in the mod and Vanilla cannot be told apart (owner decision) |
| [PrefabComponentVisibility.md](PrefabComponentVisibility.md) | Editor tools: prefab definitions show internal component fields the inspector hides |
