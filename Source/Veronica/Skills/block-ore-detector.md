---
name: block-ore-detector
description: Use when a block should find ore around the ship like an ore detector, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Ore Detector template
- how far it finds ore and how much power it draws: the user's own, or as a vanilla ore detector (Assets/Blocks/RSS_Scanner/<size>)
- a folder for the block's own ore detector definition, e.g. the block's folder

Plan:
- Read the block: its OreDetectorComponent's OreDetectorComponentDefinition and range, and its PowerableBlockDefinition's power draw
- Get from the user what this needs: how far the detector finds ore and the power it draws
- Set the range on an OreDetectorComponentDefinition of the block's own, and the power draw on its PowerableBlockDefinition
- Show the block

Steps:
1. read(block) lists its components; the OreDetectorComponent names its OreDetectorComponentDefinition, the PowerableBlockComponent its PowerableBlockDefinition. Read Range on the first and ConsumedResource on the second.
2. Ask the user, with ask_user, how far it finds ore and how much power it draws; offer a vanilla ore detector to match as a suggestion. Values left from the template were not chosen for this block: its power Amount is 0, so the detector draws nothing.
3. Range: Range is in metres, the most a player can set in the block's terminal. A block made from the template uses the template's shared OreDetectorComponentDefinition, a vanilla file. For a range of its own:
   create_partial(that OreDetectorComponentDefinition, "copy", folder), then write(block, "Components[OreDetectorComponent].Definition", <the copy>), then write(copy, "Range", n).
   The copy keeps the shared definition's DetectableOreMaterials, the ores the detector finds; change them only when the user asks.
4. Power: read the matched detector's PowerableBlockDefinition when the user named one. write(PowerableBlockDefinition, "ConsumedResource.Amount", n): kW of Electricity drawn while the block is on. Write it when it does not fit what the user wants.

Show:
show(the block). Finding ore is tested in game, on a powered ship near ore.
