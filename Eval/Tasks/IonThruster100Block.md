# Ion Thruster 100 block

## Prompt

Pick Ion Thruster 100 model from vanilla project and create new thruster block to the mod

## User

- Wants a new ion thruster block in the dev mod that looks like the vanilla Ion Thruster 100 and behaves like it.
- Has not imported anything: the block should use the vanilla model as it is.
- Has no preference for the name or the folder until asked, then picks something sensible (e.g. "Ion Thruster 100 Mk2" in `Assets/IonThruster100Mk2`).
- No icon of their own; fine with no icon or the vanilla one.
- Same thrust and power use as vanilla; nozzles where the vanilla ones are.
- Does not know engine internals: definition types, field names, file paths in vanilla.

## Pass

1. A new block prefab exists in the dev mod, created from the Ion Thrusters template.
2. Its block definition's `UIData.Name` is the name the user agreed to.
3. Its `BlockModelComponentDefinition.Model` is the vanilla Ion Thruster 100 model; fractured and damaged models are the vanilla ones too when the template expects them.
4. Its `ThrusterDefinition.ThrustPower` and `ResourcesRequiredToThrust` equal the vanilla Ion Thruster 100's.
5. Its Thruster dummies match the vanilla Ion Thruster 100's nozzles in number, position and scale.
6. The model called `show` on the new prefab.
7. Nothing outside the dev mod was changed.
