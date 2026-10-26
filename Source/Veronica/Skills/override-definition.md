---
name: override-definition
description: Use when the user wants to change something that already exists in the game, such as a vanilla block's value, flame, sound, the skybox or the character.
---
Needs:
- what to change and the new value
- folder: a mod folder for overrides, e.g. Assets/Overrides

Steps:
1. Find the definition that holds the value: find_content with words of the thing and of the definition type, e.g. "IonThruster100 ThrusterEffects". Vanilla blocks are in Assets/Blocks/<family>/<name>/<size in cm>, one definition per component: ThrusterDefinition (force), ThrusterEffectsComponentDefinition (flame), BlockEffectsComponentDefinition (sound), PowerableBlockDefinition (name, power, build cost).
2. read(def) to find the field.
3. create_partial(def, "override", folder). An override changes the original everywhere it is used; values left alone keep following the original. When the mod already has an override of it, the error names that file: write to it instead.
4. write(override, field, value) for each change.

Copy instead of override when the user wants a new thing next to the original (create_partial(def, "copy", folder)); a copy changes nothing in the game until something references it.

Show:
show(the prefab of the block that changed, or the override itself).
