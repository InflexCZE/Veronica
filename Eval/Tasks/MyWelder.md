# My Welder

## Prompt

Make a ship welder block from my MyThruster models.

## User

- Wants a new block that welds and repairs blocks in front of it, like the vanilla ship welder, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Repair Welder" in `Assets/MyRepairWelder`;
  - how fast it welds: "twice as fast as the normal welder";
  - how far it reaches: "same as the normal welder";
  - where the components come from: "from the ship's cargo, like the normal welder";
  - how many conveyor ports: "one, on the back";
  - where it goes in the block menu: "with the other welders".
- No icon of their own; fine with none.
- Visual step, positioning the tool spot and the port: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with these values:

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | tool spot (ToolDetection) | 0, 0, -0.9 | 0, 0, 0 | 1, 1, 1 |
  | port (UseConveyor) | 0, 0, 0.75 | 0, 180, 0 | 1, 1, 0.05 |

  Then it answers as the user, e.g. "Done, the tool spot is in front and the port on the back".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.9},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the welding happens just in front of the front face and the port is in the middle of the back face; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, tags, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Welder template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `IntegrityToolComponent` uses a copy of a vanilla welder's IntegrityToolDefinition with `IntegrityChange` twice the vanilla value (2), read from vanilla.
3. `DummyInteractedEntityProviderAdapterComponent`, `SphereShapeEntityDetectorComponent`, `AreaIntegrityToolEntityDetectorComponent` and `InventoryComponent` use a vanilla welder's definitions (or copies with the same values); no component of the block needs a definition (the final `read` of the block shows no "needs").
4. An `ItemRequesterComponent` on the server carries the tag `IItemRequester`, and the `InventoryComponent` carries `InventoryRequester`.
5. One ToolDetection dummy and one UseConveyor dummy: the model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
6. The block definition's `BlockKind` is the vanilla welders' kind and power stays Electricity at Amount 0; the model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model offered vanilla values before reading them, whether it added the tool damage, effects or stockpile parts, and whether it told the user players switch the welder on in its terminal.

## Fixture

`Eval/Tasks/MyWelder/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
