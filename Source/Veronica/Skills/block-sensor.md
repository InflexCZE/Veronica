---
name: block-sensor
description: Use when a block should detect ships, players or objects near it and trigger actions like a sensor, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Sensor template
- how far the field can reach and how big it starts: the user's own, or as the vanilla sensor (Assets/Blocks/Sensor)
- a folder for the block's own sensor definition, e.g. the block's folder

Plan:
- Read the block: its SensorComponent's SensorComponentDefinition and field sizes
- Get from the user what this needs: how far the field can reach and how big it is when the block is built
- Set the field sizes on a SensorComponentDefinition of the block's own
- Show the block

Steps:
1. read(block) lists its components; the SensorComponent names its SensorComponentDefinition. Read InitialDetectionBoxSize and MaxDetectionBoxSize on it.
2. Ask the user, with ask_user, how far the field can reach and how big it is when the block is first built; offer the vanilla sensor's values as a suggestion. Values left from the template were not chosen for this block.
3. Field: both sizes are in metres, measured from the block outward in each direction, not full widths: X to the left and to the right, Y up and down, Z forward and back. MaxDetectionBoxSize is the most a player can set per direction in the terminal; InitialDetectionBoxSize is the field a built block starts with, kept within the maximum. A block made from the template uses the template's shared SensorComponentDefinition, a vanilla file. For sizes of its own:
   create_partial(that SensorComponentDefinition, "copy", folder), then write(block, "Components[SensorComponent].Definition", <the copy>), then write(copy, "MaxDetectionBoxSize", {X, Y, Z}) and write(copy, "InitialDetectionBoxSize", {X, Y, Z}).
4. What the sensor detects and the actions it triggers are set by players on each built block, in its terminal and toolbars; a mod does not set them on the block. Say so when the user asks for them.

Show:
show(the block). Detection is tested in game: players set the field, filters and actions in the block's terminal.
