# Veronica: TODOs

Work known to be missing or unfinished. One file per item: what exists, what is missing, where to start. An item that needs an owner decision says so. Delete the file when the item is done.

| item | area |
|---|---|
| [ShaderCache.md](ShaderCache.md) | WebQwen: persist Dawn's compiled pipelines |
| [EngineLoopPolling.md](EngineLoopPolling.md) | WebQwen: engine thread busy-polls while the GPU works (owner decision) |
| [CodeSigning.md](CodeSigning.md) | Distribution: releases are unsigned |
| [TestDiskWrites.md](TestDiskWrites.md) | Tests: one skill test still writes files |
| [ModelDownloads.md](ModelDownloads.md) | WebQwen: a model load cannot be cancelled |
| [RequirementsCheck.md](RequirementsCheck.md) | WebQwen: can this machine run a model, including free VRAM; `Veronica.exe` shows no fit until then |
| [EngineHang.md](EngineHang.md) | Chat: no way out of a hung engine (owner decision) |
| [ToolCallHang.md](ToolCallHang.md) | Chat: no way out of a tool call that never returns (owner decision) |
| [ChatPanelGaps.md](ChatPanelGaps.md) | Chat: per-token streaming cost, one status at a time, timers outside turns |
| [StalledStream.md](StalledStream.md) | Chat: a reply stream that stops without closing waits until Stop (owner decision) |
| [QuestionCutBeforeWait.md](QuestionCutBeforeWait.md) | Chat: a question cut to its lead-in before wait_for_user (owner decision) |
| [McpAskDisconnect.md](McpAskDisconnect.md) | Chat: ask_user from an MCP client that disconnected stays on the card |
| [DescribeGaps.md](DescribeGaps.md) | Tools: describe's component summaries need doc files; prefab rule below a list; power-drawing components not marked (owner decision) |
| [InterfaceImplementers.md](InterfaceImplementers.md) | Editor tools: no way to list the components implementing an interface, on a prefab or game-wide (owner decision) |
| [PowerConfiguration.md](PowerConfiguration.md) | Skills: no skill configures idle and consumer power on any block (owner decision) |
| [ValueUnits.md](ValueUnits.md) | Chat: units invented for values the model read without one (owner decision) |
| [BoardStaleAfterReload.md](BoardStaleAfterReload.md) | Board: tiles of the previous project after a reload (owner decision) |
| [BoardLayoutGaps.md](BoardLayoutGaps.md) | Board: back edges, late size changes, unconnected nodes, cancelled drags |
| [PresentGraphUse.md](PresentGraphUse.md) | Board: a graph in place of show on a changed item (owner decision) |
| [SubpartToggleMotion.md](SubpartToggleMotion.md) | Skills: subparts that move to a target and back on a toggle |
| [SkippedBlockKinds.md](SkippedBlockKinds.md) | Skills: block kinds and parts left out of the wide block run (owner decision) |
| [Grinder.md](Grinder.md) | Skills: no grinder block, deferred out of the items and recipes round (owner decision) |
| [O2H2Generator.md](O2H2Generator.md) | Skills: no ice-to-gas generator block, deferred out of the items and recipes round (owner decision) |
| [RespawnPreviewCrash.md](RespawnPreviewCrash.md) | Editor: show of a block with a respawn point crashes the Editor; medical room kind held (owner decision) |
| [SmallModelEval.md](SmallModelEval.md) | Eval: the 9B does not finish the eval tasks reliably (owner decision: parked) |
| [NativeStdout.md](NativeStdout.md) | WebQwen: native stdout writes would corrupt the protocol |
| [ModelRecordFields.md](ModelRecordFields.md) | WebQwen: model record fields beyond Bonsai's |
| [DeviceScopedCalls.md](DeviceScopedCalls.md) | WebQwen: GPU calls use the current device, not their own |
| [ReleaseLayout.md](ReleaseLayout.md) | Distribution: no GitHub repo or release yet |
| [SplitLoad.md](SplitLoad.md) | Split model load: Editor live during shader compile (owner decision) |
| [EditorResidency.md](EditorResidency.md) | Editor: freeze and evict around a turn, edge cases not yet tested live |
| [ReloadDuringTurn.md](ReloadDuringTurn.md) | Editor: a project reload during a turn, untested with the commit job disabled |
| [VramSharingHardware.md](VramSharingHardware.md) | VRAM sharing measured on one NVIDIA card only |
| [VramSharingBigCards.md](VramSharingBigCards.md) | VRAM sharing: keep the Editor live when everything fits (owner decision) |
| [EditorVersionFloor.md](EditorVersionFloor.md) | Editor: oldest Editor build a release supports (owner decision) |
| [EditorTextOutside.md](EditorTextOutside.md) | Editor: font-fallback crash for text written outside our views |
| [EditorProjectUnload.md](EditorProjectUnload.md) | Editor: panel and menu stay live after Unload Project |
| [EditorUndo.md](EditorUndo.md) | Editor tools: agent edits are not Ctrl-Z steps, and the model cannot take back its own (owner decision) |
| [WriteValueChecks.md](WriteValueChecks.md) | Editor tools: write hints only on the written field's own validation (owner decision) |
| [SkillSources.md](SkillSources.md) | Skills: a community skill repository (owner decision) |
| [Wiki.md](Wiki.md) | Wiki: content beyond the skills, eval evidence on the research |
| [ContentQuery.md](ContentQuery.md) | Editor tools: no way to query a field across many items (owner decision) |
| [ClaudeWiki.md](ClaudeWiki.md) | Wiki: not available with the Claude provider (owner decision) |
| [ClaudeWaitForUser.md](ClaudeWaitForUser.md) | Chat: Claude's prompt and skills name wait_for_user, which it does not have (owner decision) |
| [ContextWindow.md](ContextWindow.md) | Chat: providers assume a 45K context window (owner decision) |
| [ResearcherContext.md](ResearcherContext.md) | Wiki: the researcher inherits the chat's whole transcript and does not compact (owner decision) |
| [PlanPanel.md](PlanPanel.md) | Skills: the active plan is not shown in the panel |
| [PlanChecks.md](PlanChecks.md) | Skills: plan items are completed on the model's word (owner decision) |
| [PlanChainFollowUps.md](PlanChainFollowUps.md) | Skills: optional follow-up skills (add-to-gscreen) are lost in a skill chain |
| [PausedPlans.md](PausedPlans.md) | Skills: paused plans outlive a change of task; no drop tool, no depth cap (owner decision) |
| [DummyTypeChoice.md](DummyTypeChoice.md) | Skills: dummy types chosen by name; no shared place names the configuration with the valid types (owner decision) |
| [PartialPrefabs.md](PartialPrefabs.md) | Editor tools: no partial copies or overrides of prefabs |
| [ToolRewards.md](ToolRewards.md) | Chat: no tool earns a reward; round budget raised to 50 meanwhile (owner decision) |
| [FieldPathKeys.md](FieldPathKeys.md) | Editor tools: some dictionary keys print in a form paths cannot address |
| [ConditionalWritability.md](ConditionalWritability.md) | Editor tools: conditional read-only rules of the inspector are ignored |
| [ProviderSelection.md](ProviderSelection.md) | Chat: OpenAI-compatible provider untested in a live Editor chat |
| [ClaudeProviderGaps.md](ClaudeProviderGaps.md) | Chat: Claude provider review findings not yet fixed (one owner decision) |
| [OpenAiServerQuirks.md](OpenAiServerQuirks.md) | Chat: OpenAI-compatible server behaviours not handled (one owner decision) |
| [PrefabChildEntities.md](PrefabChildEntities.md) | Editor tools: prefab component tools see the root entity only (owner decision) |
| [PrefabComponentEdits.md](PrefabComponentEdits.md) | Editor tools: prefab component edits refused per block, not per side; no rollback of a failed add |
| [TimeSpanWrites.md](TimeSpanWrites.md) | Editor tools: a TimeSpan write error does not show the accepted form |
| [ReadBoundPartHeader.md](ReadBoundPartHeader.md) | Editor tools: a read of a block's part shows the whole block without saying so |
| [ListHeaderTargets.md](ListHeaderTargets.md) | Editor tools: listing headers get copied into read targets |
| [PrefabOutsidePlan.md](PrefabOutsidePlan.md) | Editor tools: create_prefab outside create-block leaves name and models unset with no hint |
| [EditorStartupHang.md](EditorStartupHang.md) | Eval: an Editor start sometimes hangs after load (startup push never finishes) |
| [SyntheticTemplates.md](SyntheticTemplates.md) | Editor tools: no template for a bound child model such as a door leaf (owner decision) |
| [PrefabTagMove.md](PrefabTagMove.md) | Editor tools: a tag that dependents rely on cannot move to another component (owner decision) |
