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
| [ToolCallHang.md](ToolCallHang.md) | Chat: no way out of a tool call that never returns (owner decision) |
| [QuestionCutBeforeWait.md](QuestionCutBeforeWait.md) | Chat: a question cut to its lead-in before wait_for_user (owner decision) |
| [McpAskDisconnect.md](McpAskDisconnect.md) | Chat: ask_user from an MCP client that disconnected stays on the card |
| [DescribeGaps.md](DescribeGaps.md) | Tools: describe's component summaries need doc files; prefab rule below a list |
| [ValueUnits.md](ValueUnits.md) | Chat: units invented for values the model read without one (owner decision) |
| [BoardStaleAfterReload.md](BoardStaleAfterReload.md) | Board: tiles of the previous project after a reload (owner decision) |
| [BoardLayoutGaps.md](BoardLayoutGaps.md) | Board: back edges, late size changes, unconnected nodes, cancelled drags |
| [PresentGraphUse.md](PresentGraphUse.md) | Board: a graph in place of show on a changed item (owner decision) |
| [ProgressionUnlock.md](ProgressionUnlock.md) | Skills: survival unlock of a new block (owner decision) |
| [SkippedBlockKinds.md](SkippedBlockKinds.md) | Skills: block kinds and parts left out of the wide block run (owner decision) |
| [RespawnPreviewCrash.md](RespawnPreviewCrash.md) | Editor: show of a block with a respawn point crashes the Editor; medical room kind held (owner decision) |
| [SmallModelEval.md](SmallModelEval.md) | Eval: the 9B does not finish the eval tasks reliably (owner decision: parked) |
| [NativeStdout.md](NativeStdout.md) | WebQwen: native stdout writes would corrupt the protocol |
| [ModelRecordFields.md](ModelRecordFields.md) | WebQwen: model record fields beyond Bonsai's |
| [DeviceScopedCalls.md](DeviceScopedCalls.md) | WebQwen: GPU calls use the current device, not their own |
| [ReleaseLayout.md](ReleaseLayout.md) | WebQwen: release folder and publish |
| [SplitLoad.md](SplitLoad.md) | Split model load: Editor live during shader compile (owner decision) |
| [EditorResidency.md](EditorResidency.md) | Editor: freeze and evict around a turn, edge cases not yet tested live |
| [ReloadDuringTurn.md](ReloadDuringTurn.md) | Editor: a project reload during a turn, untested with the commit job disabled |
| [VramSharingHardware.md](VramSharingHardware.md) | VRAM sharing measured on one NVIDIA card only |
| [VramSharingBigCards.md](VramSharingBigCards.md) | VRAM sharing: keep the Editor live when everything fits (owner decision) |
| [EditorVersionFloor.md](EditorVersionFloor.md) | Editor: oldest Editor build a release supports (owner decision) |
| [EditorTextOutside.md](EditorTextOutside.md) | Editor: font-fallback crash for text written outside our views |
| [EditorProjectUnload.md](EditorProjectUnload.md) | Editor: panel and menu stay live after Unload Project |
| [EditorUndo.md](EditorUndo.md) | Editor tools: agent edits are not Ctrl-Z steps (owner decision) |
| [ValidationTool.md](ValidationTool.md) | Editor tools: agent cannot see validation issues |
| [WriteValueChecks.md](WriteValueChecks.md) | Editor tools: write hints only on the written field's own validation (owner decision) |
| [SkillSources.md](SkillSources.md) | Skills: install location and a community repository (owner decision) |
| [Wiki.md](Wiki.md) | Wiki: content beyond the skills, eval evidence on the research |
| [ContentQuery.md](ContentQuery.md) | Editor tools: no way to query a field across many items (owner decision) |
| [ClaudeWiki.md](ClaudeWiki.md) | Wiki: not available with the Claude provider (owner decision) |
| [ContextWindow.md](ContextWindow.md) | Chat: providers assume a 45K context window |
| [PlanPanel.md](PlanPanel.md) | Skills: the active plan is not shown in the panel |
| [PlanChecks.md](PlanChecks.md) | Skills: plan items are completed on the model's word (owner decision) |
| [PlanChainFollowUps.md](PlanChainFollowUps.md) | Skills: optional follow-up skills (add-to-gscreen) are lost in a skill chain |
| [PartialPrefabs.md](PartialPrefabs.md) | Editor tools: no partial copies or overrides of prefabs |
| [ToolRewards.md](ToolRewards.md) | Chat: no tool earns a reward; round budget raised to 50 meanwhile (owner decision) |
| [FieldPathKeys.md](FieldPathKeys.md) | Editor tools: some dictionary keys print in a form paths cannot address |
| [ConditionalWritability.md](ConditionalWritability.md) | Editor tools: conditional read-only rules of the inspector are ignored |
| [ProviderSelection.md](ProviderSelection.md) | Chat: OpenAI-compatible and Claude providers chosen by environment variables, no UI (owner decision) |
| [ClaudeProviderGaps.md](ClaudeProviderGaps.md) | Chat: Claude provider review findings not yet fixed (one owner decision) |
| [OpenAiServerQuirks.md](OpenAiServerQuirks.md) | Chat: OpenAI-compatible server behaviours not handled (one owner decision) |
| [PrefabChildEntities.md](PrefabChildEntities.md) | Editor tools: prefab component tools see the root entity only (owner decision) |
| [PrefabComponentEdits.md](PrefabComponentEdits.md) | Editor tools: prefab component edits refused per block, not per side; no rollback of a failed add |
| [ReplaceBlockComponent.md](ReplaceBlockComponent.md) | Editor tools: a block's component cannot be replaced, e.g. made powerable (owner decision) |
| [PrefabTagMove.md](PrefabTagMove.md) | Editor tools: a tag that dependents rely on cannot move to another component (owner decision) |
