# Block kinds skipped or left partial

**Exists:** block skills with eval tasks for thrusters, storage, cockpits and point lights (`Source/Veronica/Skills/block-*.md`, `Eval/Tasks/My*.md`).

**Missing:** block kinds and block parts left out of the wide block run (2026-11-01), each with why. To be taken with the owner, one at a time.

| kind or part | why left out | where to start |
|---|---|---|
| Light: model glow | `ColoringMaterialSwapperDefinition.WorkingReplacementModels` reads as "not readable here", so the tools cannot set which model parts glow in the light's colour; it depends on the model's own material names | the swapper's definition and the set type in read/write |
| Light: lens flare, toggle from cockpit | `PointLightRenderDefinition.LensFlare` and `ToggleWithLightsServerComponent.CockpitControl` stay as the template has them; not core to giving light | vanilla light blocks |
| LightPanels, NeonTubes | separate vanilla families, not point lights | Assets/Blocks/LightPanels, NeonTubes |
