# My Container thrust

## Prompt

Make my container able to thrust, like a small ion thruster.

## User

- Has a finished cargo container block, "My Container", in `Assets/MyContainerBlock`, made from the Cargo Containers template with their own imported models (the MyThruster model set). It already stores items (2000 kg) and has one conveyor port.
- Wants it to also push like the vanilla Ion Thruster 100: same thrust and power use. If asked which size: the small one, 100 cm.
- No preference for names or the folder until asked, then picks something sensible (e.g. `Assets/MyContainerBlock`).
- No flame or sound wishes; if asked, "whatever a normal ion thruster has".
- Visual step, positioning the nozzle dummy: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummy the model pointed at (in `MyContainer_BlockModelDefinition.partialdef`), with position X 0, Y 0, Z 0.6458988; Euler X 0, Y 0, Z 0; scale 0.1 on each axis. Then it answers as the user, e.g. "Done, the nozzle is in place".

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes):

  ```
  $t = 'MyContainer_BlockModelDefinition.partialdef'
  $d = 'Dummies[<index the model named>]'
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":0.6458988},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & C:\Codes\Veronica\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.1,"Y":0.1,"Z":0.1}}')
  ```

- Never hand the model these numbers. If it asks the user to describe the nozzle instead, the user says it is on the back face, centred, small; exact numbers only when the model asks for exact values.
- If the model says the block will thrust without drawing power, the user accepts it for now ("fine, leave the power as it is").
- Second turn: once the model has finished (shown the block, nozzle placed), the user sends "Nice. Can you make it twice as strong?". Answer follow-up questions in the same spirit (power use: "whatever fits", either is fine).
- Does not know engine internals: component and definition types, tags, field names, file paths in vanilla.

## Pass

1. The block has a `ThrusterComponent`.
2. After the first turn, its `Definition` is an ion `ThrusterDefinition` whose `ThrustPower` and `ResourcesRequiredToThrust` equal the vanilla Ion Thruster 100's: vanilla's own, or a new one or a partial copy in the dev mod.
3. Every component the model added that takes a definition has one (the final `read` of the block shows no "needs").
4. Exactly one new Thruster-type dummy on the block's model definition; the model asked the user to position it, pointing at it (`show` and/or its field path), and waited with `wait_for_user`.
5. After the user's answer the nozzle dummy holds the values the user set (not overwritten), and the nozzle item's proof cites them.
6. The storage is untouched: `InventoryComponent` (server, its definition with MaxMass 2000), `ConveyorComponent` and the `ConveyorBack` port dummy as in the fixture.
7. After the second turn, the block's `ThrustPower` is twice the vanilla Ion Thruster 100's, in a definition in the dev mod; no vanilla file changed. `ResourcesRequiredToThrust` as vanilla or scaled, either accepted.
8. The model called `show` on the block, no other block was created, and nothing outside the dev mod was changed.

Reported, not judged (owner decision pending): whether the block draws power when thrusting. In the game a thruster draws power only when the block's main component is a `PowerableBlockComponent` (`ThrusterComponent.Init`); the container's is a `FunctionalBlockComponent`, so thrust there is free. Note whether the model noticed and what it did or told the user.

Not required, a plus: a `ThrusterEffectsComponent` on the client with a definition (the flame).

## Fixture

`Eval/Tasks/MyContainerThrust/`: the imported MyThruster models and the finished "My Container" block in `Assets/MyContainerBlock`, made from the Cargo Containers template (one UseConveyor dummy `ConveyorBack`, InventoryDefinition MaxMass 2000).
