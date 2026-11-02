---
name: weapons
title: Weapons: ammo, projectiles, damage, muzzle effects, turret target priority
source: SE2 source AmmoDefinition, ProjectileDefinition, SimulatedProjectileDefinition, ExplosionDefinition, ProjectileWeaponEffectsDefinition, ProjectileWeaponEffectsComponent, AutoTargetingDefinition, PriorityTargetDefinition, PriorityTargetComponent; vanilla weapon blocks (Assets/Blocks/GatlingCannon, GatlingTurret, InteriorTurret, RocketLauncher, RocketTurret), Assets/System/Projectiles, Assets/Templates/Blocks/BaseDefinitions
description: How a weapon block's shots are defined: weapon, ammo and projectile definitions, where damage, range, trail, impact and muzzle effects live, explosive rounds, and how a turret picks priority targets.
---
Ammo and projectiles:
- The weapon and its ammo are separate definitions. An AmmoDefinition holds Capacity and the ProjectileDefinition it fires.
- The ProjectileDefinition holds what a shot does:
  - DamageTypeToDamage: damage per kind of target hit; may be empty for a round that only explodes.
  - Range: in metres, how far the shot travels before it disappears.
  - Visuals: the trail and impact look.
  - DefaultDecal: the mark left where it hits.
- Another trail or impact look needs a projectile definition of its own: a copy, which the ammo then points at.
- Explosive rounds: a SimulatedProjectileDefinition with an ExplosionDefinition (optional; the blast when the round is destroyed). ExplosionCullingDistance skips the blast when no observer is that close, in metres; 0 turns this off.

Muzzle effects: the muzzle flash and its offsets are on the weapon's ProjectileWeaponEffectsDefinition, apart from the projectile: OnFireParticle, BarrelOffset (position), BarrelOrientationOffset (rotation).

Turret target priority:
- The turret's AutoTargetingDefinition.HighestPriorityTargetType references a PriorityTargetDefinition (vanilla ones in Assets/System/Projectiles, e.g. HighestPriorityTargetDefinition).
- A target counts as priority when its prefab has a PriorityTargetComponent with that definition; vanilla meteors and rockets carry one. To make something else a priority target, add that component to its prefab.
- A turret already on a priority target of another type switches to a newly seen target of the highest priority type.

Vanilla weapon blocks: Assets/Blocks/GatlingCannon (a fixed gun, a base for a fixed cannon) and the turret folders (GatlingTurret, RocketTurret, InteriorTurret); the turrets' targeting definitions are in Assets/Templates/Blocks/BaseDefinitions.
