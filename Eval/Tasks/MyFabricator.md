# My Fabricator

## Prompt

Make a fabricator block from my MyThruster models.

## User

- Wants a new block players craft items in, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Fabricator" in `Assets/MyFabricator`;
  - what it crafts: "the same things as the vanilla fabricator";
  - how many orders it queues: "same as the fabricator";
  - power: "like the vanilla fabricator";
  - where it goes in the block menu: "next to the vanilla fabricator";
  - a spot that opens the production screen at the block, if offered: "yes, a screen spot on the front";
  - conveyor ports: "one, on the back".
- No icon of their own; fine with none.
- Visual step, positioning the spots: when the model asks the user to position them in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummies the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with these values:

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | screen spot (UseProduction) | 0, 0, -0.75 | 0, 0, 0 | 0.5, 0.5, 0.1 |
  | conveyor port (UseConveyor) | 0, 0, 0.75 | 0, 180, 0 | 1, 1, 0.05 |

  Then it answers as the user, e.g. "Done, the screen spot is on the front and the port on the back".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.75},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.5,"Y":0.5,"Z":0.1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spots instead, the user says the screen is in the middle of the front face and the port in the middle of the back face; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers, recipe or category names.

## Pass

1. A new block prefab exists in the dev mod, created from the Production (Manual) template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ItemConverterDefinition` `RecipeDefinitions` holds what vanilla Fabricator1000's holds: the Components tab (`Assets/System/ItemConverters/Components.def`) with `Fabricator_ItemRecipesDefinition`, read from vanilla (a reference to the vanilla set; no copy needed).
3. `MaxQueueSize` is Fabricator1000's (10) and the converter's `ConsumedResource` is Fabricator1000's (6400), read from vanilla.
4. The block definition's `ConsumedResource.Amount` is 0 (vanilla draws nothing idle; the template's fixed draw is gone), `ConsumedResource` is still Electricity, and `BlockKind` is the vanilla fabricator's (`FabricatorBlockKindDefinition`).
5. One UseConveyor dummy, and one UseProduction dummy when the model offered the screen spot (the user always accepts; not offering it is reported, not failed): the model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
6. No component of the block needs a definition (the final `read` shows no "needs"); the model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model offered vanilla values before reading them, whether it changed the inventories (capacity, whitelist), whether it asked the G-Screen place or the user had to raise it, and whether it invented parts (subparts, LCD panels).

## Fixture

`Eval/Tasks/MyFabricator/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
