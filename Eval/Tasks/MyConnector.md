# My Connector

## Prompt

Make a docking connector block from my MyThruster models.

## User

- Wants a new connector block that docks the ship to another ship or a station and joins their conveyor networks, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Docking Port" in `Assets/MyDockingPort`;
  - which connectors it should dock with: "the normal large connectors, so I can dock at vanilla stations";
  - which face docks: "the front of the block";
  - throwing items out, cargo, or other settings: "like the normal large connector";
  - conveyor ports: "one, on the back, to join the ship's conveyors";
  - power: "same as the normal connector".
- No icon of their own; fine with none.
- Visual step, positioning the spots: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages; pick each by its Type):

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | docking face (Pivot) | 0, 0, -0.75 | 0, 0, 0 | 1, 1, 0.05 |
  | detection box (ConnectorVolume) | 0, 0, -1.5 | 0, 0, 0 | 3, 3, 1.5 |
  | item ejector (Connector_ItemEjectorComponentDefinition) | 0, 0, -0.9 | 0, 0, 0 | 0.5, 0.5, 0.5 |
  | conveyor port (UseConveyor) | 0, 0, 0.75 | 0, 180, 0 | 1, 1, 0.05 |

  With Euler 0 a dummy's forward (-Z) points out of the front face. Place only the dummies the model added; if it added others, leave them and say so. Then answer as the user, e.g. "Done, all spots are placed".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.75},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":0.05}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the block docks with its front face, items should be thrown out of the front, and the conveyor port is on the back; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, how docking detects a counterpart.

## Pass

1. A new block prefab exists in the dev mod, created from the Connectors template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ConnectorDefinition` `ConnectorSize` is `Assets/System/ConnectorSizes/ConnectorSize150.def` (the vanilla large connector's), and its `ConnectionGroups` hold Physical, Terminal and Ship (`Assets/System/ConnectionGroups/Ship.def`, as the vanilla connectors).
3. Its `ItemEjectorComponentDefinition` has `EjectorDummy` `Assets/System/DummyTypes/Connector_ItemEjectorComponentDefinition.def` and `MaxBatchMass` and `EjectionSpeed` above 0 (vanilla large connector 1000 and 5, reported).
4. Four dummies with these Types: `Pivot.def`, `ConnectorVolume_DummyType.def`, `Connector_ItemEjectorComponentDefinition.def`, and one `UseConveyor.def` with a Name. The model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
5. The block keeps the template's `BlockKind` (vanilla `ConnectorBlockKindDefinition`) and its power (`ConsumedResource` Electricity, Amount 0, as vanilla); no component of the block needs a definition (the final `read` of the block shows no "needs").
6. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: the inventory's `MaxMass` (template 10000, vanilla large 16800) and whether the model matched it, whether it asked which connectors to dock with or picked the size itself, and whether it explained the docking face's direction.

## Fixture

`Eval/Tasks/MyConnector/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
