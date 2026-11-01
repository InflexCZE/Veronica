---
name: antennas
title: Antennas: broadcast range, HUD marker, power and the HUD text
source: SE2 source AntennaComponent, AntennaDefinition, AntennaObjectBuilder, AntennaMarkersSessionComponent, AntennaConnectionsSessionComponent, AntennaBlockDetailModel, PowerableBlockDefinition; vanilla antennas (Assets/Blocks/Antennas/Antennas), the Antennas template
description: What an antenna block does: its broadcast range and unit, the HUD marker other players see and its text, how antennas link grids, power, what the template brings, and the parts that are only looks.
---
Parts:
- AntennaComponent, on the server only, with its AntennaDefinition: MaxBroadcastRange, in metres, above 0 and at most 50000; and HUDMarker, the marker shown on other players' HUD (none: no marker).
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity; its Amount is the kW the antenna draws while it is on. Range and power are independent: a longer range draws no more power.
- No dummy is needed for the antenna to work.

What it does:
- Its HUD marker shows to players within MaxBroadcastRange of the block, while the block works (built, on and powered).
- An antenna links its grid to another grid whose working antenna is within its range; antennas on one grid count as one.
- The marker's text is the grid's name. Players can switch it to a custom message of their own in the block's terminal (Broadcast and Custom message). That text belongs to each built block: a mod does not set it on the block.

Vanilla antennas: Assets/Blocks/Antennas/Antennas/<size> (150, 1250). The size names the block, not its range: both use the template's shared AntennaDefinition (10000 m) and differ in power draw and model. Vanilla has no separate beacon: the antenna's marker does that job. The LaserAntenna folder is another block.

The Antennas template: a block made from it gets its own PowerableBlockDefinition (160 kW, in the vanilla antennas' G-Screen family) but keeps the template's shared AntennaDefinition. A range of its own needs an AntennaDefinition of its own: a copy of the shared one, set on the block's AntennaComponent.

Looks only, not needed to broadcast: the damage particles (on a Particles dummy in vanilla) and sounds of the BlockEffectsComponentDefinition, and the material swapper of the large vanilla antenna.
