# My Cockpit

## Prompt

Make a cockpit block from my MyThruster models.

## User

- Wants a new block the player sits in and flies the ship from, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - which kind of seat, or whether it stores items: it should hold some cargo, like the normal cockpit;
  - the sitting pose: cockpit;
  - name and folder: "My Cockpit" in `Assets/MyCockpit`;
  - capacity: "like the normal cockpit";
  - conveyors: "whatever the normal cockpit has".
- No icon of their own; fine with none.
- Visual step, positioning the seat dummies: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages; the template names the dummies Head, Seat and Enter), with these values (Euler 0 on each axis for all three):

  | dummy (Type) | position X, Y, Z | scale X, Y, Z |
  |---|---|---|
  | Seat (Seat) | 0, -0.2, 0 | 0.4, 0.4, 0.4 |
  | Head (Head) | 0, 0.25, 0.1 | 0.1, 0.1, 0.1 |
  | Enter (UsePilotable) | 0, 0, -0.4 | 0.8, 0.8, 0.3 |

  Then it answers as the user, e.g. "Done, all three are in place".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":-0.2,"Z":0},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.4,"Y":0.4,"Z":0.4}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the seat is in the middle of the block, the eyes a bit above and forward of it, and the player gets in from the front; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, tags, field names, file paths in vanilla.

## Pass

1. A new block prefab exists in the dev mod, created from the Cockpit template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `SeatComponentDefinition.SeatType.Id` is `Cockpit`.
3. Its `ModelChangingSeatComponentDefinition.Inventory` is `Inventory`, the tag of its server `InventoryComponent`.
4. No component of the block needs a definition (the final `read` of the block shows no "needs"; the template leaves `BlockToolsComponent` without one).
5. The Seat, Head and Enter (UsePilotable) dummies: the model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
6. An Override of vanilla's `CameraSystem` in the dev mod gives the block's client composition the vanilla cockpit's cameras: `CompositionToCameraPrefabs` with the cockpit third-person and first-person camera prefabs, and `CompositionToNamedCameraPrefabs` with `GridNamedCameraModesDefinition`.
7. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model asked which seat template to use or picked Cockpit from the user's wishes, and whether it took the storage through `block-inventory`.

## Fixture

`Eval/Tasks/MyCockpit/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyThrusterBlock's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
