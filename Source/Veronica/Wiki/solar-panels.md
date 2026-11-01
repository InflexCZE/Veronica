---
name: solar-panels
title: Solar panels: output, one or two sides, sun sensors and shade
source: SE2 source SolarGeneratorComponent, SolarGeneratorDefinition, SolarGeneratorValidator; vanilla solar panels (Assets/Blocks/SolarPanel/SolarPanel), the Solar Panels template
description: How a solar panel block makes power: its output and unit, the efficiency multiplier for one- or two-sided cells, the sun sensor dummies and their direction, shade from other objects and planets, what the template brings, and the parts that are only looks.
---
Parts:
- SolarGeneratorComponent, on the server only, with its SolarGeneratorDefinition: MaxOutput, the kW of Electricity made in full sunlight; EfficiencyMultiplier; DummyType, the dummy type of its sun sensors (SolarOcclusionDetector); OcclusionDetection and LargeObjectsOcclusionDetection, how far in metres it looks for shade.
- FunctionalBlockComponent as the block component: a solar panel draws no power.

How it makes power:
- Each sun sensor dummy faces out along its forward, the dummy's -Z. It adds how directly the sun shines on it (0 when the sun is behind it, 1 when straight ahead), or 0 when something shades it.
- Output = MaxOutput times the average over all sensors times EfficiencyMultiplier, never more than MaxOutput.
- EfficiencyMultiplier: 1 for cells on one side. 2 for cells on both sides with sensors facing out of each: only half the sensors see the sun at once, and 2 gives full output with one side lit. Vanilla panels are two-sided.
- Shade: each sensor checks, one at a time, for objects between it and the sun up to the OcclusionDetection distances, and for planets up to LargeObjectsOcclusionDetection. All of them 0 turns shade checks off.
- A sensor whose SupportingFracture is broken off stops counting.
- At least one and at most 32 sensors. With none the block fails validation and makes no power.

Vanilla solar panels: Assets/Blocks/SolarPanel/SolarPanel/<size> (500, 1000), standing panels with cells toward the model's -Z and +Z, six sensors on each face (the back ones turned 180 degrees about Y).

The Solar Panels template: a block made from it gets its own SolarGeneratorDefinition (1000 kW, EfficiencyMultiplier 2) and its own FunctionalBlockDefinition in the vanilla solar panels' G-Screen family. Its dummies list is empty: no sun sensors until they are added.

Looks only, not needed for power: the damage particles (on a Particles dummy in vanilla), the material swapper and the LOD materials of the vanilla panels.
