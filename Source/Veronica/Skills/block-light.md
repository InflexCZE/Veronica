---
name: block-light
description: Use when a block should give off light like a lamp or interior light, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Lights (Point) template
- the light's colour, its starting brightness and the most brightness players can set: the user's own, or as a vanilla light (Assets/Blocks/Lights)
- the light spot: from the block that already uses the model, or the user positions it in the Editor

Plan:
- Read the block: its light component's colour and brightness, its PointLightDefinition and its model definition's dummies
- Get from the user what this needs: the colour, the starting brightness and the brightness limit, and for a new model that they will place the light spot
- Set the light: colour and brightness on the block's PointLightComponent, the brightness limit and reach on its PointLightDefinition
- Set up the light dummy: one Light-type dummy, placed by copying or by the user; read it back
- Show the block

Steps:
1. read(block) lists its components. read(block, "Components[PointLightComponent]") shows LightColor and LightBrightness: what a newly built block starts with, later changed by players in the terminal. Read its PointLightDefinition and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, for the colour, the starting brightness and the brightness limit (the top of the players' slider, MaxLightBrightness); offer a vanilla light's values to match as a suggestion. A limit left from the template was not chosen for this light, so it is asked like the others. For a new model, say in the same message that they will place the light spot in the Editor.
3. Light, on the block (one write reaches both sides):
   write(block, "Components[PointLightComponent].LightColor", {R, G, B, A}): a linear colour, each channel 0 to 1. A colour given as 0-255 or hex is converted first; ask the wiki how.
   write(block, "Components[PointLightComponent].LightBrightness", n). It stays within the definition's MaxLightBrightness.
   On the PointLightDefinition: MaxLightBrightness is the top of the players' brightness slider; FalloffRangeMultiplier makes the light reach further at the same brightness. A brighter light also reaches further; there is no radius field. Write the values that do not fit what the user wants.
4. Light dummy: the light shines from the dummy whose Type is Light (the PointLightRenderDefinition's LightDummyType); without one the block gives no light. A block made from the template has none: element_add(BlockModelComponentDefinition, "Dummies"), then write its Name ("Light") and Type (Assets/System/DummyTypes/Light.def). Only its position counts.
   Positions: for a model another block uses, copy the Transform of that block's Light-type dummy; for a new model, the user places it: never compute positions. Then show(the block), ask the user to check or move it (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the position read back.

Show:
show(the block). Ask the user to check the Light dummy in the Render View (Preview Settings, Dummies).
