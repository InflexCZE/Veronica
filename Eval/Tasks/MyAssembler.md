# My Assembler

## Prompt

Make an assembler block from my MyThruster models that crafts everything the vanilla assembler does, but twice as fast. It should also craft a new component of mine, a Plasma Coil; its icon is in my MyComponent folder, and when dropped it can use the MyThruster model too.

## User

- Wants a new block players craft items in, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Wants a new component item, "Plasma Coil", that only this block crafts. Its icon is the png they imported into `Assets/MyComponent` (they do not remember the file name; they confirm it when the model names it). Dropped, it uses the plain MyThruster model.
- "Twice as fast": every recipe takes half the vanilla assembler's time.
- Each only when asked:
  - block name and folder: "My Assembler" in `Assets/MyAssembler`;
  - item folder: "put it in `Assets/MyComponent` with the icon";
  - what it crafts: "everything the vanilla assembler makes, plus the Plasma Coil";
  - the coil's recipe: "4 steel plates and 2 wires make one coil";
  - how long a coil takes: "3 seconds in a normal assembler", so 1.5 s in this one; if the model asks whether the 3 s are before or after the speed-up, "before";
  - the coil's mass: "120 kg";
  - what kind of item it is: "a component, like a motor";
  - how many orders it queues: "same as the assembler";
  - power: "like the vanilla assembler";
  - where it goes in the block menu: "next to the vanilla assembler";
  - a spot that opens the production screen at the block, if offered: "yes, a screen spot on the front";
  - conveyor ports: "one, on the back".
- No block icon of their own; fine with none.
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
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers, recipe or category names, item types, the configuration that maps dropped items.

## Pass

Vanilla values are vanilla Assembler500's (`Assets/Blocks/Assembler/Assembler/500`): its `ItemConverterDefinition` lists the Components tab (`Assets/System/ItemConverters/Components.def`) with `Assembler_ItemRecipesDefinition`, `MaxQueueSize` 20, `ConsumedResource` 1600, `SpeedMultiplier` 1; its `PowerableBlockDefinition` draws 0 idle and has `BlockKind` `AssemblerBlockKindDefinition`. Masses: steel plate 35 kg, wire 20 kg, so the inputs weigh 180 kg.

1. A new block prefab exists in the dev mod, created from the Production (Manual) template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ItemConverterDefinition` `RecipeDefinitions` holds every recipe vanilla Assembler500's does: the Components tab with `Assembler_ItemRecipesDefinition` (a reference to the vanilla set), or a copy of that set with all its recipes. `MaxQueueSize` 20 and `ConsumedResource` 1600, read from vanilla.
3. Its `SpeedMultiplier` is 2. Halving each vanilla recipe's `TimeToConvert` instead fails (it changes the vanilla assembler too).
4. A new `ItemDefinition` in the dev mod for the Plasma Coil: `Type` Component, `MassPerUnit` 120, `Icon` the fixture png (`Assets/MyComponent/MyComponent_Icon.png`).
5. A dropped-item prefab in the dev mod created from the Floating Objects/Floating Object template; the `ModelComponentDefinition` its compositions use has `Model` `Assets/MyThruster/NonFractured/MyThruster.model` and keeps the template's pickup dummy.
6. `FloatingObjectPrefabsConfiguration` is changed by one override in the dev mod whose `ItemPrefabs` maps the new item to the new prefab's `_Server` definition (not the binding) and keeps vanilla's entries.
7. The block can craft the coil: an `ItemRecipeDefinition` in the dev mod with inputs 4 steel plate (`ItemSteelPlate`) and 2 wire (`ItemWire`), output 1 of the new item, `TimeToConvert` 3 s, the user's time before the speed-up (1.5 s halves it twice with `SpeedMultiplier` 2 and fails); it is in a recipe set the block's `RecipeDefinitions` lists (a new set of the mod's own under the Components tab, or a copy of the vanilla set with it added); and the block's inventories accept the coil and both inputs (`WhitelistedTypes` with Component, `WhitelistedItems` empty or listing them). A copy of the vanilla assembler's output inventory without the coil in `WhitelistedItems` fails: the order never completes, though validation passes. No vanilla recipe or recipe set is overridden.
8. The block definition's `ConsumedResource.Amount` is 0, `ConsumedResource` is still Electricity, and `BlockKind` is the vanilla assembler's (`AssemblerBlockKindDefinition`).
9. One UseConveyor dummy, and one UseProduction dummy when the model offered the screen spot (the user always accepts; not offering it is reported, not failed): the model asked the user to position them, pointing at them (`show` and/or their field paths), and waited with `wait_for_user`; after the user's answer they hold the values the user set (not overwritten), and the dummies item's proof cites them.
10. No component of the block needs a definition (the final `read` shows no "needs"); the model called `show` on the new block; no vanilla file changed and nothing outside the dev mod was changed; no new validation error.

Not required, reported: how many compactions the run went through and whether open plan items survived them; whether the model offered vanilla values before reading them; whether it asked the coil's mass or invented one; whether it read the input masses before accepting 120 kg; which recipe-set route it took; whether it changed the inventories; whether it showed the item; whether it invented parts (subparts, LCD panels).

## Fixture

`Eval/Tasks/MyAssembler/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas, as in MyCockpit's fixture) and the imported icon `Assets/MyComponent/MyComponent_Icon.png` (a small generated png, committed with its folder and file metas, captured as the Editor writes them at load). Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
