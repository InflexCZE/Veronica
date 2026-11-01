---
name: lights
title: Lights: colour, brightness, reach, light dummy, power
source: SE2 source LightComponent, LightObjectBuilder, LightDefinition, LightRenderComponent, PointLightRenderComponent, ColoringMaterialSwapperComponent, PowerableBlockDefinition, the Brightness unit; vanilla Interior Light 50 (Assets/Blocks/Lights), the Lights (Point) template
description: How a light block works: where its colour, brightness and reach are set, what brightness numbers mean, the Light and Flare dummies, the model's glowing parts, toggling with the ship's lights, and power.
---
Parts of a point light block (vanilla Interior Light, Assets/Blocks/Lights/Interior/50):
- PointLightComponent, on both sides, with its PointLightDefinition: the light's settings.
- PointLightRenderComponent, on the client, with its PointLightRenderDefinition: draws the light at a dummy and the lens flare.
- ColoringMaterialSwapperComponent, on the client: makes the model's glowing parts take the light's colour and brightness.
- ToggleWithLightsServerComponent, on the server: lets the pilot's toggle-lights key switch the block on and off (its CockpitControl).

Colour and brightness:
- LightColor and LightBrightness sit on the block's PointLightComponent (Components[PointLightComponent] on the block). They are what a newly built block starts with; players change both in the terminal.
- LightColor is a linear colour: R, G, B and A, each 0 to 1. A colour picker's sRGB value (0-255 or hex) converts per channel: divide by 255, then raise to the power 2.2 (approximately); so sRGB 128 is about 0.22 linear, not 0.5.
- LightBrightness has no real-world unit. The engine's guidance: 15 to 100 for a light bulb, 3000 to 10000 for a spot or search light reaching over 100 m. Vanilla Interior Light starts at 60.
- PointLightDefinition:
  - MaxLightBrightness: the most brightness players can set, 0 to 1000. LightBrightness must stay within it. Vanilla Interior Light: 100; the template: 1000.
  - FalloffRangeMultiplier: stretches how far the light reaches at the same brightness, 0.001 to 50, default 1.
- Reach: the light fades with distance, so a brighter light reaches further. There is no radius field.

Dummies, in the BlockModelComponentDefinition:
- Light: where the light shines from; only its position counts for a point light. The PointLightRenderDefinition's LightDummyType names its type (Light). Without it the block gives no light.
- Flare: where the lens flare sits (the FlareDummyType, Flare). Optional: without it the flare sits at the light.
- Players never see dummies in game.

Glowing parts of the model: the ColoringMaterialSwapperDefinition lists the models (WorkingReplacementModels) and their emissive mesh parts that follow the light's colour; BrightnessMultiplier scales how strongly they glow. It depends on the model's own mesh part names, which are set up when the model is made.

Power: the block component is a PowerableBlockComponent. Its PowerableBlockDefinition's ConsumedResource is Electricity with Amount 0 on vanilla lights: the light draws nothing, but works only when the grid has power. An Amount above 0 makes it draw that much while on; no ConsumedResource at all (null) makes it work without power.
