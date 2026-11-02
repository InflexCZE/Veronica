---
name: warheads
title: Warheads: arming, countdown, damage trigger, the blast and its effects, power
source: SE2 source TimerExplosiveBlockComponent, ExplosiveBlockComponent, ExplosiveBlockDefinition, TimerExplosiveBlockDefinition, ExplosionDefinition, ExplosiveBlockEffectsComponent, TimerExplosiveBlockDetailModel, PowerableBlockDefinition; vanilla warheads (Assets/Blocks/Warheads/<size in cm>), the Warhead template
description: What a warhead block does: arming and the countdown, going off when destroyed or damaged, the blast's radius and damage, its effects, power, what the template brings and leaves without a definition.
---
Parts:
- TimerExplosiveBlockComponent, on the server, with its TimerExplosiveBlockDefinition:
  - Explosion: the ExplosionDefinition of the blast.
  - DefaultTimeToExplosion: the countdown a newly built block starts with, a TimeSpan (hh:mm:ss). Players change it on each built block; the terminal sets no limits on it.
  - MinimumIntegrityForExplosion: an armed warhead damaged so its integrity falls to this share (0 to 1) goes off at once; 0 turns this off.
  - DestroyInExplosionRange: blocks wholly inside the blast are destroyed outright instead of damaged.
  - UseExtendedLightRadius: a wider explosion light; heavy on performance and can leak light through grids.
- ExplosiveBlockEffectsComponent, on the client, with its ExplosiveBlockEffectsDefinition: the explosion's look and sound (ExplosionEffects) and the effects while armed and disarmed.
- PowerableBlockComponent as the block component, ConsumedResource Electricity with Amount 0: it draws nothing, but arming and starting the countdown need power on the grid.
- No dummy is needed.

The blast (ExplosionDefinition):
- ExplosionRadius: in metres.
- DamageTypeToDamage: the damage released at the centre per kind of target (blocks, characters, flora, floating objects); it falls off with distance.
- BlastWaveCoefficient: how far beyond the radius the push reaches, as a multiple of the radius.
- MaximumVoxelHardnessAffected: the hardest terrain the blast digs into, 0 to 10.

How it goes off: players arm the warhead in its terminal, then start the countdown; when it runs out the block is removed and explodes. An armed warhead also explodes when it is destroyed, or damaged below MinimumIntegrityForExplosion. A disarmed warhead never explodes. Armed state and countdown belong to each built block: a mod does not preset them.

Vanilla warheads: Assets/Blocks/Warheads/<size in cm> (50, 250), each with its own TimerExplosiveBlockDefinition, ExplosionDefinition and ExplosiveBlockEffectsDefinition; the larger has the bigger blast.

The Warhead template: a block made from it gets its own PowerableBlockDefinition (Amount 0, in the vanilla warheads' G-Screen family), but its TimerExplosiveBlockComponent and ExplosiveBlockEffectsComponent have no definition: read shows "needs", and the block breaks when it is built or armed until both get one.

Looks only, not needed to explode: the vanilla warheads' subparts and their animation while armed, and the block's damage particles.
