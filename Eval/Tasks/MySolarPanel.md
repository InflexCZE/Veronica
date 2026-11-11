# My Solar Panel

## Prompt

Make a solar panel block from my MyThruster models.

## User

- Wants a new solar panel block that lies flat on a ship's roof, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Solar Panel" in `Assets/MySolarPanel`;
  - how much power: "as much as the big vanilla solar panel";
  - one side or both: "only the top has cells, the bottom is a plain metal plate"; if the model never asks, the user does not volunteer it;
  - how many sun sensor spots: "two, one on each half of the top";
  - where the cells are, if asked to describe it: on the top face of the block.
- No icon of their own; fine with none.
- Visual step, positioning the sun sensor dummies: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the SolarOcclusionDetector-type dummies the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages). Values per sensor, in the order of the dummies (a third or fourth sensor, if the model made them, goes to X -0.25 / 0.25 at Z 0.25 and Z -0.25 instead of 0):

  | sensor | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | first | -0.25, 0.45, 0 | 90, 0, 0 | 0.4, 0.8, 0.05 |
  | second | 0.25, 0.45, 0 | 90, 0, 0 | 0.4, 0.8, 0.05 |

  Euler X 90 turns the dummy's forward (-Z) to face up, out of the top. Then answer as the user, e.g. "Done, both sensors are on the top".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":-0.25,"Y":0.45,"Z":0},"Euler":{"X":90,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.4,"Y":0.8,"Z":0.05}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the cells cover the top of the block; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, how the panel measures sunlight.

## Pass

1. A new block prefab exists in the dev mod, created from the Solar Panels template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `SolarGeneratorDefinition.MaxOutput` is 4000, the vanilla `SolarPanel1000`'s, from a read of the vanilla panel, not memory.
3. The block's `SolarGeneratorDefinition.EfficiencyMultiplier` is 1: the cells face one side only. The template's 2 (two-sided) left in place fails.
4. Sun sensors: at least one dummy with `Type` `Assets/System/DummyTypes/SolarOcclusionDetector.def`; the model added them, asked the user to position them, pointing at them (`show` and/or their field paths) and saying they face out of the cell face, and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
5. The block keeps the template's `BlockKind` (vanilla `SolarPanelBlockKindDefinition`) and its `FunctionalBlockComponent`; no component of the block needs a definition (the final `read` of the block shows no "needs").
6. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether it asked how many sensors or chose a count itself, whether it asked one side or both or assumed, and whether it explained the sensor direction in its own words.

## Fixture

`Eval/Tasks/MySolarPanel/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
