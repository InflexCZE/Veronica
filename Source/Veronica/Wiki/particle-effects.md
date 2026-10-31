---
name: particle-effects
title: Particle effects: where a flame's or effect's colour lives
source: vanilla particle effect definitions (ParticleEmitters, ParticleLight); SE2 particle shaders (ParticleRenderingVertex.hlsl, ParticleRenderingPixel.hlsl)
description: Where the colour of a thruster flame or another particle effect is set, and what to override to change it.
---
A flame's or effect's colour is in its particle effect's emitters: the effect's ParticleEmitters; in each emitter, the keys under TimeLines.Color, with X, Y, Z = red, green, blue and W = alpha (keep W). The effect's ParticleLight is only the glow.

Other emitter fields that change how the colour looks:
- RenderingParameters HueColorVariance, SaturationColorVariance, ValueColorVariance: each particle's colour is shifted at random around the key colour, by up to plus or minus the value in hue, saturation and value (0 to 1 scale; hue 1 is the whole colour wheel). 0 means every particle has exactly the key colour.
- TimeLines.Emissivity and TimeLines.ScatteringIntensity: how bright the particle glows on its own and how much scene light it picks up; brightness, not hue.
- The emitter's atlas texture is multiplied into the colour. With AnimationParameters.TextureIsMonochromatic it counts as grey, so the colour comes from the keys alone; a coloured texture tints the particles with its own colours.
- RenderingParameters.AlphaMultiplier scales opacity.

Override the emitters themselves: a copy of an emitter changes nothing until something points at it.
