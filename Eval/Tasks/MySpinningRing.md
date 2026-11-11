# My spinning ring

## Prompt

My gyroscope block should have a ring that spins while it works. For the ring, reuse my MyThruster model.

## User

- Has a finished gyroscope block, "My Gyro", in `Assets/MyGyroBlock`, made from the Gyroscopes template with their own models. The ring's model is `Assets/MyThruster/NonFractured/MyThruster.model`; they know they imported "the ring model", not its path, and confirm the file when the model names it.
- Wants one ring that spins while the gyroscope works and stops when it is off.
- Each only when asked:
  - axis: "around its up axis";
  - speed: "about as fast as the vanilla gyroscope's rings"; if asked for a number instead: "whatever the vanilla one does";
  - names and folder: no preference, then something sensible (e.g. `Assets/MyGyroBlock`).
- Visual step, positioning the ring's spot: when the model asks the user to position it in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummy the model pointed at in `MyGyro_BlockModelDefinition.partialdef`, with position X 0, Y 0.25, Z 0; Euler 0 on each axis; scale 1 on each axis:

  ```
  $t = 'MyGyro_BlockModelDefinition.partialdef'
  $d = 'Dummies[<index the model named>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0.25,"Z":0},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":1,"Y":1,"Z":1}}')
  ```

  Then answer as the user, e.g. "Done, the ring sits in the middle". Never hand the model these numbers; if it asks to describe the spot, the user says "in the middle of the block, a bit above the centre".
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla.

## Pass

Vanilla gyroscope rings (`Assets/Blocks/Gyroscopes/Gyroscopes/<size>/Subparts/*RotationAnimation.def`) spin at AngularSpeed 100 with AngularSpeedup and AngularSlowdown 50.

1. A subpart prefab in the dev mod, made from the Subpart template (or with its components: HierarchyComponent, ChildTransformComponent, ModelComponent, ModelRenderComponent, ParentRenderAdapterComponent), whose ModelComponent's definition is a ModelComponentDefinition in the dev mod with Model the ring model.
2. The subpart has an InfiniteRotationAnimationComponent (`Keen.VRage.Core.Game.GameSystems.RigidAnimations`) with an InfiniteRotationAnimationDefinition in the dev mod: RotationAxis the up axis (0, 1, 0; -1 also passes), AngularSpeed above 0 and AngularSpeedup above 0 (0 never spins), AngularSlowdown above 0. Values within the vanilla rings' range pass; others pass when the model told the user where they came from.
3. My Gyro has a ConditionalSubPartsComponent (client) whose ConditionalSubPartsDefinition's SubParts maps a DummyTypeDefinition in the dev mod (not a vanilla one) to the subpart prefab.
4. My Gyro's BlockModelComponentDefinition holds exactly one dummy of that type, positioned by the user; the model did not overwrite the user's values.
5. My Gyro has a FunctionalBlockAnimationControllerComponent, so the ring stops when the gyroscope does not work.
6. The gyroscope is otherwise unchanged (MaxTorque 217980, power 5), no vanilla file changed, the model called `show`, and no validation error the run added (Get-Validation end vs start).

Not required, reported: paint on the ring (BlockSubpartRenderComponent), mesh parts hidden while active, collision of the ring.

## Fixture

`Eval/Tasks/MySpinningRing/` holds the imported MyThruster models (`Assets/MyThruster`, as in MyGatling's fixture) and the finished gyroscope block My Gyro (`Assets/MyGyroBlock`, from the Gyroscopes template with the MyThruster models, MaxTorque and power as vanilla Gyroscope50). The ring's model is the MyThruster model. Start-Run copies it over the clean dev mod.
