# My Landing Gear

## Prompt

Make a landing gear block from my MyThruster models.

## User

- Wants a new landing gear block that locks the ship to the ground or another ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Landing Gear" in `Assets/MyLandingGear`;
  - which face is the lock pad: "the bottom of the block";
  - how flat the surface must be, or other lock settings: "like the normal landing gear";
  - power: "same as the normal landing gear".
- No icon of their own; fine with none.
- Visual step, positioning the two boxes: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages; pick each by its Type):

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | lock box (LockArea) | 0, -0.55, 0 | -90, 0, 0 | 0.8, 0.8, 0.1 |
  | release box (CooldownRange) | 0, -0.65, 0 | -90, 0, 0 | 1.0, 1.0, 0.3 |

  Euler X -90 turns the dummy's forward (-Z) to point down, out of the pad; the thin Z side of each box is then vertical. Then answer as the user, e.g. "Done, both boxes are under the pad".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":-0.55,"Z":0},"Euler":{"X":-90,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.8,"Y":0.8,"Z":0.1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the boxes instead, the user says the pad is the bottom of the block and the boxes go just under it; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, how the lock detects a surface.

## Pass

1. A new block prefab exists in the dev mod, created from the Landing Gear template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Lock box: a dummy with `Type` `Assets/System/DummyTypes/LockArea.def`.
3. Release box: a dummy with `Type` `Assets/System/DummyTypes/CooldownRange.def`.
4. The model asked the user to position both, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
5. The block keeps the template's `BlockKind` (vanilla `LandingGearBlockKindDefinition`) and its power (`ConsumedResource` Electricity, Amount 0, as vanilla); no component of the block needs a definition (the final `read` of the block shows no "needs").
6. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether it asked which face is the pad, whether it explained what each box does in its own words, and any lock settings it changed (MaxAlignmentAngle, detection frequency) and why.

## Fixture

`Eval/Tasks/MyLandingGear/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
