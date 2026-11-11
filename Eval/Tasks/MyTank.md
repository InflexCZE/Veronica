# My Tank

## Prompt

Make an oxygen tank block from my MyThruster models.

## User

- Wants a new block that stores oxygen for the ship, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Each only when asked:
  - name and folder: "My Oxygen Tank" in `Assets/MyOxygenTank`;
  - which gas: oxygen (already in the prompt; confirms it if asked);
  - how much it holds: "twice as much as the normal oxygen tank";
  - how fast it fills and empties: "same as the normal oxygen tank";
  - where it goes in the block menu (G-Screen): "with the other oxygen tanks";
  - what happens when it is destroyed full: "like the normal oxygen tank";
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
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers.

## Pass

1. A new block prefab exists in the dev mod, created from the Tanks template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. Its `ResourceContainerDefinition` `ResourceType` is Oxygen and `MaxCapacity` is twice the vanilla oxygen tank's (16000), read from vanilla.
3. `MaxDischargeRate` and `MaxChargingRate` match the vanilla oxygen tank's (2000 and 1999.9).
4. The block definition's `BlockKind` is the vanilla oxygen tanks' kind.
5. Its `ResourceContainerBlockDestructionExplosionDefinition` `Explosion` is the vanilla oxygen tank's ExplosionDefinition (or a copy of it).
6. One UseConveyor dummy: the model asked the user to position it, pointing at it (`show` and/or its field path), and waited with `wait_for_user`; after the user's answer it holds the values the user set (not overwritten), and the dummies item's proof cites them.
7. No component of the block needs a definition (the final `read` of the block shows no "needs"); the model called `show` on the new prefab, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the model offered a vanilla tank's values before reading them, whether it asked the G-Screen place or set it on its own, and whether it invented a suit refill or other parts.

## Fixture

`Eval/Tasks/MyTank/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas), the same as MyCockpit's fixture. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
