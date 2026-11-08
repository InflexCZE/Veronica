---
name: block-gyro
description: Use when a block should turn the ship like a gyroscope, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Gyroscopes template
- how strong it turns and how much power it draws: the user's own, or as a vanilla gyroscope (Assets/Blocks/Gyroscopes/Gyroscopes/<size in cm>)

Plan:
- Read the block: its GyroscopeDefinition's torque and its PowerableBlockDefinition's power draw
- Get from the user what this needs: how strong the gyroscope turns and the power it draws
- Set the torque on the block's GyroscopeDefinition and the power draw on its PowerableBlockDefinition
- Show the block

Steps:
1. read(block) lists its components; the GyroscopeComponent names the block's GyroscopeDefinition, the PowerableBlockComponent its PowerableBlockDefinition. Read MaxTorque on the first and ConsumedResource on the second.
2. Ask the user, with ask_user, how strong it turns and how much power it draws; offer a vanilla gyroscope to match as a suggestion (list_folder shows the sizes). Values left from the template were not chosen for this block: its MaxTorque is 0, and a gyroscope with 0 torque builds and draws power but never turns the ship, with no error.
3. Torque and power: read the matched gyroscope's GyroscopeDefinition and PowerableBlockDefinition when the user named one.
   write(GyroscopeDefinition, "MaxTorque", n): the most turning force it adds to the ship, in N*m.
   write(PowerableBlockDefinition, "ConsumedResource.Amount", n): kW of Electricity drawn while the block is on.
   Write each value that does not fit what the user wants: as the matched gyroscope, changed as the user asks, or their own value.

Show:
show(the block). Turning the ship is tested in game, on a powered ship.
