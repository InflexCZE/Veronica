---
name: block-light
description: Use when a block should give off light like a lamp, interior light or spotlight, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Lights (Point) or Lights (Spot) template
- the light's colour, its starting brightness and the most brightness players can set, and for a spotlight the width of its beam: the user's own, or as a vanilla light (Assets/Blocks/Lights)
- the light spot: from the block that already uses the model, or the user positions it in the Editor

Plan:
- Read the block: its light component's colour and brightness, its light definition and its model definition's dummies
- Get from the user what this needs: the colour, the starting brightness, the brightness limit, for a spotlight the beam width, and for a new model that they will place the light spot
- Set the light: colour and brightness on the block's light component, the brightness limit, reach and beam width on its light definition
- Set up the light dummy: one Light-type dummy, placed by copying or by the user; read it back
- Show the block

Steps:
1. read(block) lists its components: a PointLightComponent with a PointLightDefinition, or a SpotLightComponent with a SpotLightDefinition. read(block, "Components[<light component>]") shows LightColor and LightBrightness: what a newly built block starts with, later changed by players in the terminal. Read its light definition and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, for the colour, the starting brightness and the brightness limit (the top of the players' slider, MaxLightBrightness), and for a spotlight how wide the beam is; offer a vanilla light's values to match as a suggestion. Values left from the template were not chosen for this light, so they are asked like the others. For a new model, say in the same message that they will place the light spot in the Editor.
3. Light, on the block (one write reaches both sides):
   write(block, "Components[<light component>].LightColor", {R, G, B, A}): a linear colour, each channel 0 to 1. A colour given as 0-255 or hex is converted first; ask the wiki how.
   write(block, "Components[<light component>].LightBrightness", n). It must stay within the definition's MaxLightBrightness.
   On the light definition: MaxLightBrightness is the top of the players' brightness slider; FalloffRangeMultiplier makes the light reach further at the same brightness. A brighter light also reaches further; there is no radius field. A SpotLightDefinition also has OuterAngle, the full width of the beam's cone in radians (degrees x pi / 180). Write the values that do not fit what the user wants.
4. Light dummy: the light shines from the dummy whose Type is Light (the light render definition's LightDummyType); without one the block gives no light. A block made from a template has none: element_add(BlockModelComponentDefinition, "Dummies"), then write its Name ("Light") and Type (Assets/System/DummyTypes/Light.def). Only its position counts: a spotlight shines along the block's forward, the model's -Z, whatever the dummy's rotation.
   Positions: for a model another block uses, copy the Transform of that block's Light-type dummy; for a new model, the user places it: never compute positions. Then show(the block), ask the user to check or move it (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the position read back.

Show:
show(the block). Ask the user to check the Light dummy in the Render View (Preview Settings, Dummies).
