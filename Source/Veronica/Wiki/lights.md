---
name: lights
title: Lights: point and spot lights, colour, brightness, reach, beam width, light dummy, power
source: SE2 source LightComponent, LightObjectBuilder, LightDefinition, SpotLightDefinition, LightRenderComponent, PointLightRenderComponent, SpotLightRenderComponent, ColoringMaterialSwapperComponent, PowerableBlockDefinition, the Brightness unit; vanilla Interior Light 50 and Spotlight 50 and 250 (Assets/Blocks/Lights), the Lights (Point) and Lights (Spot) templates
description: How a light block works, point and spot: where its colour, brightness, reach and beam width are set, what brightness numbers mean, the Light and Flare dummies, the model's glowing parts, toggling with the ship's lights, and power.
---
Parts of a point light block (vanilla Interior Light, Assets/Blocks/Lights/Interior/50):
- PointLightComponent, on both sides, with its PointLightDefinition: the light's settings.
- PointLightRenderComponent, on the client, with its PointLightRenderDefinition: draws the light at a dummy and the lens flare.
- ColoringMaterialSwapperComponent, on the client: makes the model's glowing parts take the light's colour and brightness.
- ToggleWithLightsServerComponent, on the server: lets the pilot's toggle-lights key switch the block on and off (its CockpitControl).

A spotlight (vanilla Spotlight, Assets/Blocks/Lights/SpotLight/50 and 250) has the same parts with SpotLightComponent, SpotLightDefinition, SpotLightRenderComponent and SpotLightRenderDefinition in place of the point ones.

Colour and brightness:
- LightColor and LightBrightness sit on the block's light component (Components[PointLightComponent] or Components[SpotLightComponent] on the block). They are what a newly built block starts with; players change both in the terminal.
- LightColor is a linear colour: R, G, B and A, each 0 to 1. A colour picker's sRGB value (0-255 or hex) converts per channel: divide by 255, then raise to the power 2.2 (approximately); so sRGB 128 is about 0.22 linear, not 0.5.
- LightBrightness has no real-world unit. The engine's guidance: 15 to 100 for a light bulb, 3000 to 10000 for a spot or search light reaching over 100 m.
- The light definition (PointLightDefinition or SpotLightDefinition):
  - MaxLightBrightness: the most brightness players can set. LightBrightness must stay within it. The spot template starts its blocks above its own limit.
  - FalloffRangeMultiplier: makes the light reach further at the same brightness.
- Reach: the light fades with distance, so a brighter light reaches further. There is no radius field.
- SpotLightDefinition only:
  - OuterAngle: the full width of the beam's cone, in radians (degrees x pi / 180; 60 degrees is 1.0472).
  - OuterConeStartRadius: 0 starts the beam from a point.
  - IncludeAmbientPointLight and AmbientLightIntensityMultiplier: the vanilla spotlights have this glow.
  - The SpotLightRenderDefinition's CookieTexture: the vanilla spotlights use one (Assets/BlockMaterials/Spotlight); the spot template has none.
- Direction: a spotlight shines along the block's forward, the model's -Z; the Light dummy's rotation is not used.

Dummies, in the BlockModelComponentDefinition:
- Light: where the light shines from; only its position counts. The light render definition's LightDummyType names its type (Light). Without it the block gives no light. Neither light template brings one.
- Flare: where the lens flare sits (the FlareDummyType, Flare).
- Players never see dummies in game.

Glowing parts of the model: the ColoringMaterialSwapperDefinition lists the models (WorkingReplacementModels) and their emissive mesh parts that follow the light's colour. It depends on the model's own mesh part names, which are set up when the model is made.

Power: the block component is a PowerableBlockComponent. Its PowerableBlockDefinition's ConsumedResource is Electricity with Amount 0 on vanilla lights and both templates.
