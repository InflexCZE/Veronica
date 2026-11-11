# My Reactor

## Prompt

Make a reactor block from my MyThruster models.

## User

- Wants a new block that makes power for the ship by burning fuel, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Reactor" in `Assets/MyReactor`;
  - which fuel: "the same as the smaller vanilla reactor";
  - how much power: "40 MW";
  - the energy a unit of fuel gives, or how long fuel lasts: "a kilo of fuel should be worth as much as in that smaller reactor"; if asked whether that means the same energy per kilo or the same burn time: "the same energy per kilo, so it burns faster at the higher power";
  - how much fuel it holds: "same as that reactor";
  - whether it takes fuel from the conveyors by itself: "yes, like the normal reactor";
  - how many conveyor ports: "one, on the back".
- No icon of their own; fine with none.
- Visual step, positioning the conveyor port: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the UseConveyor dummy the model added in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with Position (0, 0, 0.75), Euler (0, 180, 0) and Scale (1, 1, 0.05). Then it answers as the user, e.g. "Done, the port is on the back".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes):

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the port>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":0.75},"Euler":{"X":0,"Y":180,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":0.05}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spot instead, the user says the port is in the middle of the back face; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers, the fuel's name.

## Pass

1. A new block prefab exists in the dev mod, created from the Reactors template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ItemToResourceConverterDefinition` `ItemConsumed` is the uranium ore item (`UraniumOreItem_FloatingObjectItem`), the fuel of vanilla Reactor250, read from vanilla.
3. `MaxResourceProduction` is 40000 and `ResourcesPerItem` is Reactor250's (14400), read from vanilla.
4. Its fuel `InventoryDefinition` `MaxMass` is Reactor250's (12000); its whitelist still allows uranium ore.
5. Its `InventoryStockpileDefinition` `Items` holds the uranium ore item and `FillRatioPull` is above 0 and at most 1 (vanilla 0.01; another value is reported).
6. One UseConveyor dummy: the model asked the user to position it, pointing at it (`show` and/or its field path), and waited with `wait_for_user`; after the user's answer it holds the values the user set (not overwritten), and the dummies item's proof cites them.
7. The block definition's `BlockKind` is still the vanilla reactors' and `ConsumedResource` is none; no component of the block needs a definition (the final `read` shows no "needs"); the model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model offered vanilla values before reading them, whether it named the fuel to the user or asked them to, whether it converted MW to kW without asking, and whether it invented parts (subparts, explosion changes).

## Fixture

`Eval/Tasks/MyReactor/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
