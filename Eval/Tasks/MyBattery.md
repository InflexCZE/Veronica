# My Battery

## Prompt

Make a battery block from my MyThruster models.

## User

- Wants a new battery block for ships, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Battery" in `Assets/MyBattery`;
  - which template, or whether players swap power cells: "like the normal batteries, with power cells";
  - output: "2 MW at most";
  - charging: "about as fast as it gives power";
  - how many cells: "one is enough";
  - where it goes in the block menu (G-Screen): "with the other batteries";
  - power draw, if asked: "like the normal batteries".
- No icon of their own; fine with none.
- Visual step, positioning the cell slot dummy: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the CellSlot-type dummy the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with position X 0, Y 0.1, Z -0.3, Euler 0 on each axis, scale 0.25 on each axis. Then answer as the user, e.g. "Done, the cell spot is in place".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes):

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0.1,"Z":-0.3},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.25,"Y":0.25,"Z":0.25}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spot instead, the user says the cell sits in the front of the block, a little above the middle; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, kW vs MW.

## Pass

1. A new block prefab exists in the dev mod, created from the Battery template (not the older Batteries); its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ResourceContainerInventoryDefinition.MaxDischargeRate` is 2000 (kW) and `MaxChargingRate` is at least 1990 and below 2000.
3. Its `PowerableBlockDefinition.ConsumedResource` is null, as on the vanilla batteries.
4. Its `PowerableBlockDefinition.BlockKind` is vanilla `BatteriesBlockKindDefinition` (a new kind joining the batteries tile through add-to-gscreen also passes).
5. A CellSlot-type dummy (`Type` `Assets/System/DummyTypes/CellSlot.def`): the model added it, asked the user to position it, pointing at it (`show` and/or its field paths), and waited with `wait_for_user`; after the user's answer it holds the values the user set (not overwritten), and the dummies item's proof cites them.
6. The block keeps the template's one cell slot (`InventoryComponent` tagged `CellSlot0` with its PowerModule); no component of the block needs a definition (the final `read` of the block shows no "needs").
7. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether `MaxCapacity` was set to what the cell holds (3600000), whether it converted MW to kW itself or asked, and whether it asked the user for values readable in vanilla.

## Fixture

`Eval/Tasks/MyBattery/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
