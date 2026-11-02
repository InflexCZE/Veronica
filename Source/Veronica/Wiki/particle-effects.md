---
name: particle-effects
title: Particle effects: colour, emitters, timing and where blocks play them
source: vanilla particle effect and emitter definitions in Assets/System/Particles (ParticleEmitters, ParticleLight, e.g. Thruster_ParticleEffectFlame.def); SE2 source ParticleEmitterDefinition, ParticleEmissionParameters, BlockEffectsComponentDefinition, ThrusterEffectsComponentDefinition; SE2 particle shaders (ParticleRenderingVertex.hlsl, ParticleRenderingPixel.hlsl)
description: How a particle effect is built from emitters: where a flame's or effect's colour, particle life and duration are set, IsParameterDriven for thruster flames, which block slots play effects, and what to copy or override to change one block's effect.
---
A flame's or effect's colour is in its particle effect's emitters: the effect's ParticleEmitters; in each emitter, the keys under TimeLines.Color, with X, Y, Z = red, green, blue and W = alpha (keep W). The effect's ParticleLight is only the glow.

An effect only lists its emitters (ParticleEmitters) and its ParticleLight; what the particles look like and do (colour, size, life, emission) is on each ParticleEmitterDefinition. Every variant of a look needs its own emitter definition. Vanilla effects and emitters are in Assets/System/Particles (Blocks, Character, Generic, Planets); scan there for a starting point.

Other emitter fields that change how the colour looks:
- RenderingParameters HueColorVariance, SaturationColorVariance, ValueColorVariance: each particle's colour is shifted at random around the key colour, by up to plus or minus the value in hue, saturation and value (0 to 1 scale; hue 1 is the whole colour wheel). 0 means every particle has exactly the key colour.
- TimeLines.Emissivity and TimeLines.ScatteringIntensity: how bright the particle glows on its own and how much scene light it picks up; brightness, not hue.
- The emitter's atlas texture is multiplied into the colour. With AnimationParameters.TextureIsMonochromatic it counts as grey, so the colour comes from the keys alone; a coloured texture tints the particles with its own colours.

Timing: the effect's EffectDuration and EffectDurationVariation, and EmitterLifeTime; each emitter's ParticleLifeSpan (a timeline) is how long one particle lives. Longer-lived particles travel further, so a flame reaches further.

Parameter-driven slots (a thruster's OnThrustPoweredParticleEffect) play the effect along a 0 to 1 game parameter (thrust) and fail validation unless the effect has IsParameterDriven true. Every emitter of it must agree: its EmissionParameters.IsParameterDriven equals the effect's (the emitter has no IsParameterDriven of its own outside EmissionParameters).

Where a block plays effects:
- BlockEffectsComponentDefinition: OnDamagedBlockParticleEffect (only while damaged); OnActivationParticleEffects and OnUsedParticleEffects (looped, while the block is active or used, at a dummy).
- ThrusterEffectsComponentDefinition: OnThrustPoweredParticleEffect (looped, while thrusting).
- Any looped effect fits these slots. No slot that plays always is known; a thruster block whose OnThrustPoweredParticleEffect is the wanted effect plays it while thrusting.

To change one block's effect only: make a new effect with its own emitters (copies of the vanilla ones, changed) and point that block's slot at it; changing the vanilla effect or emitters changes every block that plays them.

Override the emitters themselves: a copy of an emitter changes nothing until something points at it.
