---
name: block-wind-turbine
description: Use when a block should make power from wind like a wind turbine, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Wind Turbine template
- the most power it makes: the user's own, or as the vanilla wind turbine (Assets/Blocks/WindTurbine)
- where the blades spin on the user's model: how high the rotor sits above the model's origin and how long the blades are
- a folder for the block's own turbine definition, e.g. the block's folder

Plan:
- Read the block: its WindTurbineComponent's WindTurbineDefinition
- Get from the user what this needs: the most power it makes, and where the blades spin on their model
- Set the output and the clearance probes on a WindTurbineDefinition of the block's own
- Show the block

Steps:
1. read(block) lists its components; the WindTurbineComponent names its WindTurbineDefinition. Read it.
2. Ask the user, with ask_user, how much power the turbine makes at most, offering the vanilla turbine's as a suggestion, and, for their own model, how high above the model's origin the rotor sits and how long the blades are. Values left from the template were not chosen for this block: its probe geometry fits the vanilla turbine's model, not this one.
3. A block made from the template uses the template's shared WindTurbineDefinition, a vanilla file. For values of its own:
   create_partial(that WindTurbineDefinition, "copy", folder), then write(block, "Components[WindTurbineComponent].Definition", <the copy>).
   On the copy:
   - MaxOutput: kW of Electricity, made in the best conditions.
   - VerticalOffsetForClearance: metres above the model's origin, along the block's up, where the clearance rays are cast: the rotor's height.
   - TurbineOffsetToProbe: metres from the rotor's axis where each ray starts. Put it just past the blade tips: a ray starting inside the block's own model hits the block itself and counts as blocked.
   Write the values that do not fit what the user wants.

Show:
show(the block). A wind turbine makes power only on a planet in atmosphere, on a station or a grid attached to something static; the user tests that in game.
