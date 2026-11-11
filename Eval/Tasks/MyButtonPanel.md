# My Button Panel

## Prompt

Make a button panel block from my MyThruster models.

## User

- Wants a new panel block with buttons players press to trigger things on the ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Control Panel" in `Assets/MyControlPanel`;
  - how many buttons: "three";
  - where it goes in the block menu: "with the other button panels";
  - power: "same as the normal button panel".
- No icon of their own; fine with none.
- Visual step, positioning the buttons: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the UseToolbarSlot dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages; pick each by the number at the end of its Name), with these values (Euler 0 on each axis for all three):

  | button (number at the end of its Name) | position X, Y, Z | scale X, Y, Z |
  |---|---|---|
  | 0 | -0.3, 0, -0.5 | 0.2, 0.2, 0.1 |
  | 1 | 0, 0, -0.5 | 0.2, 0.2, 0.1 |
  | 2 | 0.3, 0, -0.5 | 0.2, 0.2, 0.1 |

  Then answer as the user, e.g. "Done, the three buttons are on the front".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":-0.3,"Y":0,"Z":-0.5},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.2,"Y":0.2,"Z":0.1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the three buttons sit in a row on the front face, left to right; exact numbers only when the model asks for exact values.
- If asked what the buttons should do: "I'll set that up in game".
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, how a button picks its slot.

## Pass

1. A new block prefab exists in the dev mod, created from the Buttons template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `BlockToolbarServerComponent` has a `BlockToolbarServerDefinition` with `ToolbarsCount` 1 and `TilesCount` 3 (no vanilla one has these counts, so a copy in the dev mod); no vanilla file changed.
3. Three dummies of `Type` `Assets/System/DummyTypes/UseToolbarSlot.def`, whose Names end in a space and a number: 0, 1 and 2, each once.
4. The model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
5. Its block definition's `BlockKind` is the vanilla button panels' (`ButtonPanelBlockKindDefinition`); power stays `ConsumedResource` Electricity, Amount 0, as vanilla; no component of the block needs a definition (the final `read` of the block shows no "needs").
6. The model called `show` on the new prefab, and nothing outside the dev mod was changed.

Not required, reported: whether it asked the number of buttons or took it from vanilla, whether it added a UseTerminal spot (optional), and whether it told the user the buttons' actions are set in game.

## Fixture

`Eval/Tasks/MyButtonPanel/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
