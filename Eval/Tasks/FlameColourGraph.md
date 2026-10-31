# Flame colour graph

## Prompt

How is the colour of the ion thruster's flame controlled? Which files are involved?

## User

- Wants to understand it, not change it now. If the model offers to make a change, the user says not now.
- The run ends after the model has explained it.
- Does not know engine internals.

## Pass

1. No project change (no editing tool, no `activate_skill`).
2. `present_graph` is called, and its call is accepted.
3. The graph holds the ion thruster's chain as it is in Vanilla, every node an item the model read or found: the block or its `ThrusterEffectsComponentDefinition`, the flame particle effect, and its emitters; the light is optional. Every edge points from the item that references to the item referenced.
4. The text explains it as well: the colour lives in the emitters' `TimeLines.Color` keys (with `RenderingParameters` colour variance), `ParticleLight` only colours the glow.
5. The question is answered in the turn it was asked.
6. Nothing invented: no definition, field or file that does not exist.

## Fixture

None: a clean dev mod.
