# Flame copy question

## Prompt

I copied the ion thruster's flame particles into my mod and made the copy red, but in game the flame is still blue. What did I do wrong?

## User

- Did this in another mod; the dev mod is empty. If the model says it finds no copy in the project, the user says it was in another mod and asks what the fix is in general.
- Wants to understand the fix, not have it done now. If the model offers to do it, the user says not now.
- Wants only their own thruster red if possible; would accept all ion thrusters red if that is the only way. Says so only when asked.
- The run ends after the model has explained the fix.
- Does not know engine internals.

## Pass

1. No project change (no editing tool, no `activate_skill`).
2. The cause: a copy is a new thing and changes nothing until something references it; the vanilla flame still points at the original.
3. By the end of the run, the fix has offered the two ways and what each changes (the second may come once the user says only their thruster should be red): override the original emitters (every ion thruster using them turns red), or point the thruster's flame at the copy (only what is pointed at it changes).
4. Where the colour lives, if named, is right: the emitters' `TimeLines.Color` keys, `ParticleLight` only the glow.
5. Each question is answered in the turn it was asked.
6. Nothing invented: no definition, field or folder that does not exist.

## Fixture

None: a clean dev mod.
