---
name: gravity-generators
title: Gravity generators: reach, strength, shapes, power
source: SE2 source GravityGeneratorComponent, GravityGeneratorDefinition, GeneratorBaseComponent, GeneratorBaseDefinition, GravityGeneratorBlockDetailModel, PowerableBlockDefinition, GravityGeneratorProcessorComponent; the vanilla gravity generator (Assets/Blocks/GravityGenerator/GravityGenerator/500), the Gravity Generators template
description: What a gravity generator block does: the field's reach and strength and their units, the limits players can set, the three field shapes and which way they pull, power, what the template brings, the parts that are only looks, and where a planet's gravity is set and why an edit does not change a spawned planet.
---
Parts:
- GravityGeneratorComponent, on both sides, with its GravityGeneratorDefinition: MaxAffectDistance (m), the most reach players can set; MinGravitationalAcceleration and MaxGravitationalAcceleration (m/s^2), the strength players can set; SupportedGravityShapes, the shapes players can pick.
- The component's own fields are the field a newly built block starts with, later changed by players in the terminal: AffectDistance (m, within MaxAffectDistance), GravitationalAcceleration (m/s^2, within the strength limits; negative pushes instead of pulling) and GravityShape.
- PowerableBlockComponent as the block component. ConsumedResource.Amount is the kW drawn while the block is on; gravity works only while the block works (built, on and powered), which needs power on the grid even at Amount 0 (the power page).
- No dummy is needed.

Units: the terminal shows strength in g, the definition and the block in m/s^2; 1 g is 9.80665 m/s^2.

Shapes (the block's axes, its model's axes):
- Field: a box reaching AffectDistance from the block in every direction; inside it, gravity pulls toward the block's down (the model's -Y) at the same strength everywhere. The usual floor gravity of a ship.
- Spherical: a sphere of radius AffectDistance; gravity pulls toward the block.
- Cylindrical: gravity pulls toward the line through the block along its up and down axis, within AffectDistance of that line.

Planets: a planet's gravity is the GravityGeneratorComponent on the planet prefab, the same fields as a block's; its GravityGeneratorDefinition only holds the limits. On the prefab, AffectDistance and AccelerationDistance (where GravitationalAcceleration applies when the gravity falls off with distance) are multiples of the planet's radius, scaled when the planet spawns; AffectDistance is capped at MaxAffectDistance. A spawned planet keeps the values it was saved with, so a change shows only on a newly spawned planet.

The Gravity Generators template: a block made from it gets its own GravityGeneratorDefinition and its own PowerableBlockDefinition, in the vanilla gravity generators' G-Screen family. It starts its blocks with reach 0 and strength 0, so a built block makes no gravity until a player sets them, and it draws no power. The vanilla gravity generator starts its blocks with a reach and strength set.

Looks only, not needed for gravity: the vanilla block's spinning rings (subparts with an animation controller), its LCD panel, material swapper and damage particles.
