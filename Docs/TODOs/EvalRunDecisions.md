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
