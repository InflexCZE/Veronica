---
name: block-warhead
description: Use when a block should explode when players arm it and start its countdown, like a warhead, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Warhead template
- the blast: as one of the vanilla warheads (Assets/Blocks/Warheads), or the user's own
- the countdown a newly built block starts with, and whether it goes off when badly damaged: the user's own, or as a vanilla warhead
- a folder for the block's own definitions, e.g. the block's folder

Plan:
- Read the block: which of its components need a definition, and a vanilla warhead's explosive definitions
- Get from the user what this needs: the blast, the starting countdown and whether damage sets it off
- Give the explosive component a TimerExplosiveBlockDefinition with the user's countdown, damage trigger and blast
- Give the explosive effects component its effects definition
- Show the block

Steps:
1. read(block) lists its components. From the template, TimerExplosiveBlockComponent (the explosive, server) and ExplosiveBlockEffectsComponent (its armed, disarmed and explosion effects, client) show "needs": without a definition the block breaks when built or armed. Read a vanilla warhead's TimerExplosiveBlockDefinition and its Explosion (find_content "Warhead TimerExplosive" in Assets/Blocks/Warheads).
2. Ask the user, with ask_user, for the blast (a vanilla warhead's size, or a radius of their own), the countdown a new block starts with, and whether the warhead goes off when badly damaged while armed; offer a vanilla warhead's values as a suggestion.
3. Explosive: a vanilla warhead's TimerExplosiveBlockDefinition is used as it is when every value fits. Otherwise create_partial(it, "copy", folder) and write the values that differ on the copy:
   DefaultTimeToExplosion: the countdown a new block starts with, a TimeSpan written as text ("00:00:10" for 10 seconds; a bare number is read as days). Players change it on each built block.
   MinimumIntegrityForExplosion: an armed warhead damaged below this share of its integrity (0 to 1) goes off; 0 turns this off.
   Explosion: the ExplosionDefinition of the blast. A vanilla warhead's is used as it is; a blast of the user's own size is a copy with its ExplosionRadius (metres) and DamageTypeToDamage changed.
   Then write(block, "Components[TimerExplosiveBlockComponent].Definition", <that definition>).
4. Effects: write(block, "Components[ExplosiveBlockEffectsComponent].Definition", <the ExplosiveBlockEffectsDefinition of the vanilla warhead whose blast it has>). It holds the explosion's look and sound and the arm and disarm effects. read(block) again: no component shows "needs".
5. Arming and starting the countdown are done by players on each built block in its terminal, and need power on the grid; a mod does not preset them. Say so when the user asks.

Show:
show(the block). The warhead is tested in game: players arm it and start its countdown in the block's terminal.
