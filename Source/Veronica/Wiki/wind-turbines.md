---
name: wind-turbines
title: Wind turbines: output, where they work, clearance probes, height and weather
source: SE2 source WindTurbineComponent, WindTurbineDefinition, WindTurbineAnimationOverrideComponent; the vanilla wind turbine (Assets/Blocks/WindTurbine/750), the Wind Turbine template
description: What a wind turbine block does: its output and unit, where it works, what lowers its output (height above ground, air density, weather, blocked clearance rays), the probe geometry that must fit the model, what the template brings, and the parts that are only looks.
---
Parts:
- WindTurbineComponent, on the server only, with its WindTurbineDefinition.
- PowerableBlockComponent as the block component, with ConsumedResource none: the turbine makes power and draws none.
- No dummy is needed for it to work.

Where it works: only in atmosphere (air density above 0) and only on a static grid (a station) or a grid attached to something static. Elsewhere it makes nothing.

Output: MaxOutput, kW of Electricity, times four factors, each 0 to 1, multiplied together:
- Height: the distance from the block down to the ground divided by OptimalDistanceToGroundHeight (metres), at most 1.
- Air: the air density divided by OptimalAirDensity (kg/m^3), at most 1.
- Weather: BaseWeatherMultiplier without a wind event, rising towards 1 as a wind event gets stronger.
- Clearance: ProbingRayCount horizontal rays, evenly spread around the rotor; each ray that hits something cuts the output by one share. Characters do not block (LayersToIgnoreForClearance).

Clearance probe geometry (WindTurbineDefinition):
- VerticalOffsetForClearance: metres from the model's origin along the block's up, where the rays are cast.
- TurbineOffsetToProbe: metres from the block's up axis where each ray starts.
- OcclusionDetection: metres each ray reaches beyond its start.
- ProbingRayCount: rays around the axis, 1 to 32.
The template's geometry fits the vanilla turbine's model. On a model of another size the rays start in the wrong place: inside the block's own model they hit the block and count as blocked, and far outside it they miss obstacles near the blades.

The vanilla wind turbine: Assets/Blocks/WindTurbine/750. It uses the template's shared WindTurbineDefinition.

The Wind Turbine template: a block made from it gets its own PowerableBlockDefinition (in the vanilla turbine's G-Screen family) but keeps the template's shared WindTurbineDefinition. Values of its own need a definition of its own: a copy of the shared one, set on the block's WindTurbineComponent.

Looks only, not needed to make power: the spinning blades (a subpart with its animation controller on the vanilla block; the template has neither), WindTurbineAnimationOverrideComponent (sets the blades' speed from the output), the efficiency indicator material and the sounds.
