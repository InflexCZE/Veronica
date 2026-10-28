# Ion thrusters override

## Prompt

Make all ion thrusters twice as strong and give them a red flame.

## User

- Wants every vanilla ion thruster in the game, all sizes, to push twice as hard, and their flame to be red.
- "Twice as strong" is force only. If asked about power use: just more force, same power.
- "Red" is any clearly red flame; no exact colour in mind. If asked about the glow or light, red is nicer but not required.
- Has no preference for the folder until asked, then picks something sensible (e.g. `Assets/IonOverrides`).
- Does not know engine internals: definition types, field names, particle effects, file paths in vanilla.

## Pass

1. The dev mod holds an override of each vanilla ion thruster's `ThrusterDefinition` (Ion 100, 150, 500, 750), with `ThrustPower` twice the vanilla value within float rounding: 17900.612, 164985.76, 1712737.1, 11273974.
2. `ResourcesRequiredToThrust` is unchanged on all four (40, 240, 1800, 8000).
3. The ion flame is red: overrides of the flame effect's three emitters (`ParticleEmitter_Placeholder_ThrusterFlame_A`, `_B`, `_B_Dot`), with every key of `TimeLines.Color` red-dominant (R greater than G and B) and its alpha (W) unchanged. The light (`ParticleLightDefinition`) is optional; note it in the run note either way.
4. No other thruster class is changed.
5. The model called `show` on a changed block or override, and nothing outside the dev mod was changed.
