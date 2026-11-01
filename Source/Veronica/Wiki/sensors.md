---
name: sensors
title: Sensors: detection field, what is detected, actions, power
source: SE2 source SensorComponent, SensorComponentDefinition, SensorBlockDetailModel, PowerableBlockDefinition; the vanilla sensor (Assets/Blocks/Sensor/25), the Sensor template
description: What a sensor block does: its detection field and units, the largest field players can set, what it detects and the actions it triggers, power, what the template brings.
---
Parts:
- SensorComponent, on the server only, with its SensorComponentDefinition:
  - InitialDetectionBoxSize: in metres from the block in each direction (a half size: X 10 is 10 m to the left and 10 m to the right).
  - MaxDetectionBoxSize: the most a player can set in each direction, in metres, per axis: X for left and right, Y for up and down, Z for forward and back. Every axis must stay above 0.01: a smaller maximum breaks the terminal's field sliders.
  - GridLayers, FloatingObjectsLayer, VoxelsLayer, PlayersLayer: vanilla's are kept.
- Two BlockToolbarServerComponents, OnFirstEnteredToolbar and OnLastExitedToolbar: the actions run when the first thing enters the field and when the last one leaves it.
- SensorFieldDrawerComponent and its controller: draw the field in game when a player switches its drawing on.
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity with Amount 0.
- No dummy is needed for the sensor to work.

Set by players on each built block, in its terminal: the six field sizes (left, right, up, down, forward, back, each up to the maximum), what it detects (ships, floating objects, players, terrain, the owner, friends, enemies) and the toolbar actions. These belong to each built block: a mod does not set them on the block, and the Editor does not show them.

The vanilla sensor: Assets/Blocks/Sensor/25. It uses the template's shared SensorComponentDefinition.

The Sensor template: a block made from it gets its own PowerableBlockDefinition (Amount 0, in the vanilla sensor's G-Screen family) but keeps the template's shared SensorComponentDefinition. Field sizes of its own need a definition of its own: a copy of the shared one, set on the block's SensorComponent.
