# Calls made during the wide block run, for owner review

**Exists:** calls made without the owner during the unattended wide block run (2026-11-01) on eval tasks and context.

**Missing:** the owner's review of each. Keep, change or revert; delete the line once reviewed.

| kind | call | why |
|---|---|---|
| Light | Missing-BlockKind note added to shared context (`create-block` step 4, `templates` wiki) rather than `block-light` only | applies to any template without a family; regressions MyThrusterBlock and MyCockpit passed with it |
| Light | Wiki gives sRGB to linear as divide by 255, raise to 2.2 | standard approximation, not taken from the game source |
| Light | MyLight Pass 2 accepts any linear orange (G 0.36-0.46, B 0.08-0.15 seen); Pass 3 expects the vanilla Interior Light 60 / 100, the user saying only "as bright as the normal interior light" | tests read-then-write without handing out values |
| Light | `block-light` asks the brightness limit (MaxLightBrightness) with colour and brightness, since a run kept the template's 1000 unasked | batch 2 went 3/3 after it |
| Light | create-block plan item "Fill block name and icon" does not mention the G-Screen place; 2 of 6 runs set BlockKind only after the light plan ended (on their own) | not changed; candidate if a later kind fails on it |
| Gyro | MyGyro covers values only (torque, power), no Editor step; 3/3 at once, so it may be too easy to tell contexts apart | the template's traps are only values; no dummy is needed |
| Gyro | Wiki leaves out vanilla torque numbers, so the task checks that the model reads them | the thrusters page does state Ion 100 values; inconsistent on purpose |
| Shared | create-block step 4 names the block component's definition as the one with UIData, not BreakableBlockDefinition | all 3 gyro runs wrote UIData to BreakableBlockDefinition first; 0/3 antenna runs after the fix, regressions pass |
| Antenna | MyAntenna Pass 6 is a knowledge follow-up (custom HUD text) inside a build task; the wiki says the HUD text is a per-block player setting a mod does not set | AntennaObjectBuilder fields have no ForceShowInInspector, so the Editor cannot set them either |
| Antenna | write accepted MaxBroadcastRange 0 although the game's Range attribute rejects it; the skill states the bounds | tools do not enforce [Range]; related to ValidationTool TODO |
| Spotlight | block-light covers point and spot (one skill per feature); MyLight runs now ask point or spot first | owner rule; MyLight regressions 2/2 |
| Spotlight | MySpotlight Pass 3 requires reach (FalloffRangeMultiplier) 8, which 2 of 3 runs matched from vanilla without asking the user | stricter variant would require asking |
| Solar | block-solar asks one side or both (EfficiencyMultiplier 1 or 2) and how many sun sensors per face; MySolarPanel 3/3 at once, so the template trap is always caught by the skill's question | the multiplier's meaning is from the definition's own comment and the averaging code |
