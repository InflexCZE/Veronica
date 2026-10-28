# My Thruster block

## Prompt

I imported my thruster model into Assets/MyThruster. Make a new ion thruster block from it.

## User

- Wants a new ion thruster block in the dev mod that uses their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Thrust and power use like the vanilla Ion Thruster 100.
- Has no preference for the name or the folder until asked, then picks something sensible (e.g. "My Thruster" in `Assets/MyThrusterBlock`).
- No icon of their own; fine with none.
- Visual step, positioning the nozzle dummy: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummy the model pointed at (usually `Dummies[0]` of the new block's BlockModelComponentDefinition; take its file name from the model's messages or `create_prefab` reply), with this model's nozzle values: position X 0.125, Y 0.125, Z 0.6458988; Euler X 0, Y 0, Z 0; scale 0.1 on each axis. Then it answers as the user, e.g. "Done, it's in place".

  ```
  powershell -File Eval/Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson '{"target":"<block model definition>","path":"Dummies[0].Transform","value":{"Position":{"X":0.125,"Y":0.125,"Z":0.6458988},"Euler":{"X":0,"Y":0,"Z":0}}}'
  powershell -File Eval/Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson '{"target":"<block model definition>","path":"Dummies[0].Scale","value":{"X":0.1,"Y":0.1,"Z":0.1}}'
  ```

- Never hand the model these numbers. If it asks the user to describe the nozzle instead, the user says it is on the back face, centred, small; exact numbers only when the model asks for exact values.
- Does not know engine internals: definition types, field names, file paths in vanilla.

## Pass

1. A new block prefab exists in the dev mod, created from the Ion Thrusters template.
2. Its block definition's `UIData.Name` is the name the user agreed to.
3. Its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
4. Its `ThrusterDefinition.ThrustPower` and `ResourcesRequiredToThrust` equal the vanilla Ion Thruster 100's.
5. It has exactly one Thruster-type dummy, and the model asked the user to position it, pointing at it (`show` and/or its field path), and waited with `wait_for_user`.
6. After the user's answer the dummy holds the values the user set (not overwritten), and the nozzle item's proof cites them.
7. The model called `show` on the new prefab, and nothing outside the dev mod was changed.

## Fixture

`Eval/Tasks/MyThrusterBlock/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas). Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
