# Block kinds skipped or left partial

**Exists:** block skills with eval tasks for thrusters, storage, cockpits and point lights (`Source/Veronica/Skills/block-*.md`, `Eval/Tasks/My*.md`).

**Missing:** block kinds and block parts left out of the wide block run (2026-11-01), each with why. To be taken with the owner, one at a time.

| kind or part | why left out | where to start |
|---|---|---|
| Light: model glow | `ColoringMaterialSwapperDefinition.WorkingReplacementModels` reads as "not readable here", so the tools cannot set which model parts glow in the light's colour; it depends on the model's own material names | the swapper's definition and the set type in read/write |
| Light: lens flare, toggle from cockpit | `PointLightRenderDefinition.LensFlare` and `ToggleWithLightsServerComponent.CockpitControl` stay as the template has them; not core to giving light | vanilla light blocks |
| LightPanels, NeonTubes | separate vanilla families, not point lights | Assets/Blocks/LightPanels, NeonTubes |
| Gyroscope: spinning parts, hum, damage particles | ConditionalSubPartsComponent with the S1-S3 subpart models and a RotatingSubparts dummy, the animation controller, material swapper, GyroscopeSM use sound, Particles dummy; looks only, the gyro turns the ship without them | vanilla Assets/Blocks/Gyroscopes |
| Antenna: HUD marker look, LaserAntenna | a custom marker (icon, colour) via AntennaDefinition.HUDMarker stays as the template has it; LaserAntenna is another block, not looked at | AntennaHUDMarker.def, Assets/Blocks/LaserAntenna |
| Sound block | the template adds no sound components: vanilla's SoundBlockServerComponent and SoundBlockComponent would have to be added, and sounds beyond vanilla's need FMOD banks the modder authors | vanilla Assets/Blocks/Sound/25 |
| Spotlight: cookie, cone start, ambient glow, cockpit toggle | CookieTexture, OuterConeStartRadius, IncludeAmbientPointLight and its multiplier stay as the template has them; vanilla's block component carries a TOGGLE_LIGHTS tag the template's lacks (likely the cockpit lights key, not verified) | vanilla Spotlight250 |
| Solar panel: fracture link, looks | the sensors' SupportingFracture (vanilla ties each to a fracture piece so a broken-off piece stops counting) left null; Particles dummy, material swapper, LOD materials | vanilla SolarPanel1000 BlockModelDefinition |
