# My door

## Prompt

I want a sliding door block with two leaves that slide apart. Use my MyThruster model for the frame and for both leaves.

## User

- Wants a new door block in the dev mod: the frame uses their imported models `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`; each leaf uses `Assets/MyThruster/NonFractured/MyThruster.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Two leaves, a left and a right one, sliding sideways apart to open, like the vanilla narrow sliding door.
- Each only when asked:
  - name and folder: "My Door" in `Assets/MyDoor`;
  - how fast it opens: "about half a second, like the vanilla sliding door";
  - where players open it from: "a panel on each side, front and back";
  - names of the leaves and the other parts: no preference, something sensible.
- No icon of their own; fine with none.
- Visual steps, positioning dummies: when the model asks the user to position one or more in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), matching each dummy by its Type (and Name for the two panels):

  | dummy | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | left leaf, closed spot | 0.3, 0, 0 | 0, 0, 0 | 1, 1, 1 |
  | left leaf, open spot | 0.9, 0, 0 | 0, 0, 0 | 1, 1, 1 |
  | right leaf, closed spot | -0.3, 0, 0 | 0, 0, 0 | 1, 1, 1 |
  | right leaf, open spot | -0.9, 0, 0 | 0, 0, 0 | 1, 1, 1 |
  | open panel, front (UseDoor) | 0.5, 0, -0.6 | 0, 0, 0 | 0.15, 0.3, 0.1 |
  | open panel, back (UseDoor) | -0.5, 0, 0.6 | 0, 180, 0 | 0.15, 0.3, 0.1 |
  | doorway (DoorwayObstacleDetectorDummy) | 0, 0, 0 | 0, 0, 0 | 1.2, 2, 0.25 |

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0.3,"Y":0,"Z":0},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":1}}')
  ```

  Then answer as the user, e.g. "Done, the leaves and panels are in place".
- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the leaves meet in the middle when closed and slide out to the sides, the panels are beside the doorway on the front and the back; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla.

## Pass

Vanilla reference: the narrow sliding door (`Assets/Blocks/Doors/SlideDoorNarrow/250`): OpeningDuration 0.5 s, doorway QueryHalfExtents (0.6, 1, 0.125), each leaf a bound prefab (`Subparts/SlideDoorNarrow_250_LeftDoor`).

1. A new block prefab in the dev mod from the Doors template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three MyThruster .model assets.
2. `DoorServerDefinition.OpeningDuration` above 0 (the template's 0 divides by zero); the client door component has a `DoorClientDefinition` whose `ServerDefinition` is the block's `DoorServerDefinition`.
3. The `DoorBlockColliderDefinition` has `SubPartsProvider` a `ConditionalSubPartsDoorPartsProviderDefinition` whose `ConditionalSubPartsDefinition` is the block's own, and `ModelDefinition` the block's `BlockModelComponentDefinition` (either unset throws when the door loads).
4. The block's `ConditionalSubPartsDefinition.SubParts` has two entries, each keyed by a different `DummyTypeDefinition` in the dev mod (a closed spot), each mapping to the `_Client` prefab of a leaf.
5. Each leaf is a bound prefab in the dev mod (a `PrefabBindingDefinition` with client and server prefabs), with the components of a vanilla leaf: `ChildTransformComponent`, `HierarchyComponent`, `ModelComponent` with a `ModelComponentDefinition` in the dev mod whose `Model` is the MyThruster model, `ModelRenderComponent` and `ParentRenderAdapterComponent` (client), `DoorSegmentTransformSLERPComponent` with a definition whose `ClosedPositionDummy` is that leaf's SubParts key and `OpenPositionDummy` another dummy type of its own, and `DoorSegmentComponent`. The two leaves' four dummy types are all different.
6. The block's `BlockModelComponentDefinition.Dummies` holds one dummy of each of the four leaf dummy types, two `UseDoor` dummies and one `DoorwayObstacleDetectorDummy`, positioned by the user; the model did not overwrite the user's values.
7. The doorway detector's `QueryHalfExtents` is above 0 on every axis.
8. No vanilla file changed, the model called `show` on the door, and no validation error the run added (Get-Validation end vs start).

Not required, reported: door sound, glow (material swapper), highlight sections, `SupportingFracture` of the dummies, the detector on the server only (vanilla) or on both sides (template), a leaf made from the single-sided Subpart template (fails criterion 5; note it).

## Fixture

`Eval/Tasks/MyDoor/` holds the imported MyThruster models only (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas, as in MyThrusterBlock's fixture). The model makes the door block itself from the Doors template. Start-Run copies it over the clean dev mod.
