---
name: block-antenna
description: Use when a block should broadcast like a radio antenna, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Antennas template
- how far it broadcasts and how much power it draws: the user's own, or as a vanilla antenna (Assets/Blocks/Antennas/Antennas)
- a folder for the block's own antenna definition, e.g. the block's folder

Plan:
- Read the block: its AntennaComponent's AntennaDefinition and range, and its PowerableBlockDefinition's power draw
- Get from the user what this needs: how far the antenna broadcasts and the power it draws
- Set the range on an AntennaDefinition of the block's own, and the power draw on its PowerableBlockDefinition
- Show the block

Steps:
1. read(block) lists its components; the AntennaComponent names its AntennaDefinition, the PowerableBlockComponent its PowerableBlockDefinition. Read MaxBroadcastRange on the first and ConsumedResource on the second.
2. Ask the user, with ask_user, how far it broadcasts and how much power it draws; offer a vanilla antenna to match as a suggestion. Values left from the template were not chosen for this block.
3. Range: MaxBroadcastRange is in metres. A block made from the template uses the template's shared AntennaDefinition, a vanilla file that also serves the vanilla antennas. For a range of its own:
   create_partial(that AntennaDefinition, "copy", folder), then write(block, "Components[AntennaComponent].Definition", <the copy>), then write(copy, "MaxBroadcastRange", n).
   When the range the user wants is the shared definition's own, the block keeps it.
4. Power: read the matched antenna's PowerableBlockDefinition when the user named one. write(PowerableBlockDefinition, "ConsumedResource.Amount", n): kW of Electricity drawn while the block is on. Write it when it does not fit what the user wants.

Show:
show(the block). The broadcast is tested in game, on a powered ship.
