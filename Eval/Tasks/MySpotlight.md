# My Spotlight

## Prompt

I want a spotlight block made from my MyThruster models.

## User

- Wants a new searchlight block for the outside of ships, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Searchlight" in `Assets/MySearchlight`;
  - colour: a cool, slightly blue white; exact values only when asked for them: "RGB 200, 220, 255";
  - brightness: "as bright as the big vanilla spotlight"; the brightness limit players can set, if asked: "same as that one";
  - reach or range, if asked: "it should reach as far as the big vanilla spotlight";
  - beam width: "narrow-ish, about 40 degrees wide";
  - anything else of the vanilla spotlight's look (glow around the lamp, cookie, flare), if asked: "whatever the vanilla one does is fine";
  - power, if asked: "like the normal spotlight".
- No icon of their own; fine with none.
- Visual step, positioning the light dummy: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the Light-type dummy the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages), with position X 0, Y 0, Z -0.45, Euler 0 on each axis, scale 0.2 on each axis. If the model also added a Flare-type dummy and asks for it, put it at the same values. Then answer as the user, e.g. "Done, the lamp spot is set".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index of the dummy>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.45},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.2,"Y":0.2,"Z":0.2}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the spot instead, the user says the light comes out of the front face of the block; exact numbers only when the model asks for exact values. If the model asks which way the beam points: "out of the front".
- Does not know engine internals: component and definition types, dummy types, field names, file paths in vanilla, radians, linear vs sRGB colour.

## Pass

1. A new block prefab exists in the dev mod, created from the Lights (Spot) template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. The block's `Components[SpotLightComponent].LightColor` is a cool white: B the largest channel and at least 0.9, R below G below B, R at least 0.4 (sRGB 200, 220, 255 is about 0.58, 0.72, 1 linear; 0.78, 0.86, 1 taken as plain 0-1 also passes and is reported); A is 1.
3. Brightness and reach as the vanilla large spotlight (`Spotlight250`): `Components[SpotLightComponent].LightBrightness` is 300, the block's `SpotLightDefinition.MaxLightBrightness` is 700 and `FalloffRangeMultiplier` is 8. The values came from a read of the vanilla spotlight, not memory.
4. The block's `SpotLightDefinition.OuterAngle` is 40 degrees in radians: 0.69 to 0.71.
5. The block's definition `BlockKind` is still vanilla `SpotlightBlockKindDefinition`.
6. A Light-type dummy (`Type` `Assets/System/DummyTypes/Light.def`): the model added it, asked the user to position it, pointing at it (`show` and/or its field paths), and waited with `wait_for_user`; after the user's answer it holds the values the user set (not overwritten), and the dummies item's proof cites them.
7. Power left as vanilla: the block component is the template's `PowerableBlockComponent`, `ConsumedResource` Electricity with Amount 0; no component of the block needs a definition (the final `read` of the block shows no "needs").
8. The model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether it set the ambient glow, cone start radius, cookie texture or a Flare-type dummy like the vanilla spotlight; whether it told the user the beam shines along the block's forward (the model's -Z); how it converted the colour and the angle; and whether it asked the user for values readable in vanilla.

## Fixture

`Eval/Tasks/MySpotlight/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
