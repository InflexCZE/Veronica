---
name: particle-colors
description: Reference: where the colour of a flame or another particle effect lives.
---
A flame's or effect's colour is in its particle effect's emitters: the effect's ParticleEmitters; in each emitter, the keys under TimeLines.Color, with X, Y, Z = red, green, blue and W = alpha (keep W). The effect's ParticleLight is only the glow.
Override the emitters themselves: a copy of an emitter changes nothing until something points at it.
