# My Light

## Prompt

Make a light block from my MyThruster models.

## User

- Wants a new lamp block for ship interiors, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Lamp" in `Assets/MyLamp`;
  - colour: a warm orange, like an old bulb; exact values only when asked for them: "RGB 255, 170, 80";
  - brightness: "about as bright as the normal interior light"; the brightness limit players can set, if asked: "same as the interior light too";
  - reach or range, if asked: "whatever the interior light does";
  - where it goes in the block menu (G-Screen): "with the other interior lights";
  - power, if asked: "like the normal interior light".
- No icon of their own; fine with none.
- Visual step, positioning the light dummy: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the Light-type dummy the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with position X 0, Y 0.3, Z 0.2, Euler 0 on each axis, scale 0.1 on each axis. If the model also added a Flare-type dummy and asks for it, put it at the same values. Then answer as the user, e.g. "Done, the light spot is in place".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0.3,"Z":0.2},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.1,"Y":0.1,"Z":0.1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spot instead, the user says the light should come out of the top front of the block; exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, linear vs sRGB colour.

## Pass

1. A new block prefab exists in the dev mod, created from the Lights (Point) template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `Components[PointLightComponent].LightColor` is a warm orange: R the largest channel and at least 0.9, G below R, B below G and at most 0.35 (sRGB 255, 170, 80 is about 1, 0.4, 0.08 linear; 1, 0.67, 0.31 taken as plain 0-1 also passes and is reported); A is 1.
3. Brightness as the vanilla Interior Light: `Components[PointLightComponent].LightBrightness` is 60 and the block's `PointLightDefinition.MaxLightBrightness` is 100 (vanilla `InteriorLight50`); `FalloffRangeMultiplier` stays 1. The values came from a read of the vanilla light, not memory.
4. The block's definition `BlockKind` is vanilla `InteriorLightsBlockKindDefinition` (a new kind joining the interior lights tile through add-to-gscreen also passes).
5. A Light-type dummy (`Type` `Assets/System/DummyTypes/Light.def`): the model added it, asked the user to position it, pointing at it (`show` and/or its field paths), and waited with `wait_for_user`; after the user's answer it holds the values the user set (not overwritten), and the dummies item's proof cites them.
6. Power left as vanilla: the block component is the template's `PowerableBlockComponent`, `ConsumedResource` Electricity with Amount 0; no component of the block needs a definition (the final `read` of the block shows no "needs").
7. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether it added a Flare-type dummy, how it converted the colour (asked the wiki, sRGB to linear or not), and whether it asked the user for values readable in vanilla.

## Fixture

`Eval/Tasks/MyLight/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
