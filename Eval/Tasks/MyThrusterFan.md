# My thruster fan

## Prompt

Give my thruster a fan that spins faster the harder it thrusts. The fan uses my thruster model.

## User

- Has a finished ion thruster block, "My Thruster", in `Assets/MyThrusterBlock`, made from the Ion Thrusters template with their own imported models. The fan's model is `Assets/MyThruster/NonFractured/MyThruster.model`; they confirm the file when the model names it.
- Wants one fan that turns slowly when the thruster idles and fast at full thrust.
- Each only when asked:
  - axis: "around the thrust direction, front to back";
  - speeds: "slow, like 30 degrees a second idle, and 600 at full thrust"; if asked about spin-up: "quick, but not instant";
  - names and folder: no preference, then something sensible (e.g. `Assets/MyThrusterBlock`).
- Visual step, positioning the fan's spot: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummy the model pointed at in `MyThruster_ThrusterBlockModelDefinition.partialdef`, with position X 0, Y 0, Z 0.5; Euler 0 on each axis; scale 1 on each axis:

  ```
  $t = 'MyThruster_ThrusterBlockModelDefinition.partialdef'
  $d = 'Dummies[<index the model named>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":0.5},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":1}}')
  ```

  Then answer as the user, e.g. "Done, the fan is at the back". Never hand the model these numbers; if it asks to describe the spot, the user says "at the back, in the middle".
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla.

## Pass

1. A subpart prefab in the dev mod (Subpart template or its components) whose ModelComponent's definition is a ModelComponentDefinition in the dev mod with Model the fan model.
2. The subpart has an InfiniteRotationAnimationComponent (`Keen.VRage.Core.Game.GameSystems.RigidAnimations`) with an InfiniteRotationAnimationDefinition in the dev mod: RotationAxis along Z (0, 0, 1 or -1), AngularSpeed 30, VariableAngularInterpolatedSpeed 600, AngularSpeedup and AngularSlowdown above 0 (0 never spins).
3. My Thruster has a ConditionalSubPartsComponent (client) whose ConditionalSubPartsDefinition's SubParts maps a DummyTypeDefinition in the dev mod to the subpart prefab, and exactly one dummy of that type in its BlockModelComponentDefinition, positioned by the user; the model did not overwrite the user's values. The existing Nozzle and Particles dummies are unchanged.
4. My Thruster has a ThrusterAnimationOverrideComponent, which feeds the thrust to the fan. A FunctionalBlockAnimationControllerComponent as well is not required; note it.
5. The thruster's own values are unchanged, no vanilla file changed, the model called `show`, and no validation error the run added (Get-Validation end vs start).

## Fixture

`Eval/Tasks/MyThrusterFan/` is MyThrusterCargo's fixture: the imported MyThruster models (`Assets/MyThruster`) and the finished ion thruster My Thruster (`Assets/MyThrusterBlock`). The fan's model is the MyThruster model.
