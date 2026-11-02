# My Drill

## Prompt

Make a ship drill block from my MyThruster models.

## User

- Wants a new block that digs terrain and collects ore, like the vanilla ship drill, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Mining Drill" in `Assets/MyMiningDrill`;
  - how it digs (speed, size of the cut, reach, damage in front): "same as the normal ship drill";
  - how much ore it holds: "twice as much as the normal drill";
  - where the ore goes: "out to the ship's cargo through the conveyors, like the normal drill";
  - how many conveyor ports: "one, on the back";
  - where it goes in the block menu: "with the other drills".
- No icon of their own; fine with none.
- Visual step, positioning the drill head spots, the damage box and the port: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with these values:

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | primary spot (PrimaryDrill) | 0, 0, -0.9 | 90, 0, 0 | 1, 1, 1 |
  | secondary spot (SecondaryDrill) | 0, 0, -0.9 | 90, 0, 0 | 1, 1, 1 |
  | damage box (DrillDamage) | 0, 0, -0.95 | 0, 0, 0 | 1.25, 1.25, 0.74 |
  | port (UseConveyor) | 0, 0, 0.75 | 0, 180, 0 | 1, 1, 0.05 |

  Then it answers as the user, e.g. "Done, the drill spots and the damage box are at the front, the port on the back".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.9},"Euler":{"X":90,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the drill head is at the front face, the damage happens just in front of it, and the port is in the middle of the back face; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, tags, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Drills template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Digging like the vanilla drill: the DrillDefinition its `DrillComponent` uses has both brush shapes set and the drilling time modifiers of a vanilla drill, read from vanilla.
3. Reach and damage like the vanilla drill: the `SphereShapeEntityDetectorComponent`'s definition has a vanilla drill's `QueryRadius` (above 0), and the `ToolDamageComponent`'s definition has a `DamageAdapterPrefab`, with the `BoxShapeEntityDetectorComponent`'s `QueryHalfExtents` above 0 on every axis; values read from vanilla.
4. Ore: the inventory's `MaxMass` is twice a vanilla drill's, read from vanilla; the `InventoryStockpileComponent`'s definition has `FillRatioPush` set (not none).
5. PrimaryDrill, SecondaryDrill, DrillDamage and one UseConveyor dummy: the model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
6. The block definition's `BlockKind` is the vanilla drills' kind and power stays Electricity at Amount 0; no component needs a definition (the final `read` of the block shows no "needs"); `DrillToolEffectsComponent` uses a vanilla drill's DrillToolEffectDefinition, not the template's abstract base; the model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.
7. The Editor log (copied by Stop-Run) shows no validation error from the block's files after the final edits: errors while a dummy was being added (missing dummy, a null Type before its write) are transient. A component set on a vanilla definition passes 2-4 when the values match, but leaves the block's own template copy empty, which fails here.

Not required, reported: whether the model offered vanilla values before reading them, whether it set `WhitelistedTypes` Ore and `PreventStockpilingFromAddingItems`, and whether it told the user the drill digs only while built, on and powered.

## Fixture

`Eval/Tasks/MyDrill/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
