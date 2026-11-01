---
name: timers
title: Timers: delay range, actions, sounds, status glow, power
source: SE2 source TimerBlockServerComponent, TimerBlockDefinition, TimerBlockDetailModel, TimerBlockIndicatorComponent, TimerBlockAudioComponent, SilentModeComponent, PowerableBlockDefinition; the vanilla timer (Assets/Blocks/Timer/50), the Timers template
description: What a timer block does: the delay range players can set and its unit, the actions it runs, its sounds and status glow, power, what the template brings.
---
Parts:
- TimerBlockServerComponent, on the server, with its TimerBlockDefinition: DurationMin and DurationMax, TimeSpans (hh:mm:ss). They are the ends of the delay slider in the block's terminal; a delay players set is kept within them. The delay a block already has is not changed by them.
- A BlockToolbarServerComponent: the actions the timer runs when its delay runs out.
- TimerBlockComponent on the client, TimerBlockAudioComponent (start, running and end sounds, from a shared vanilla definition) and SilentModeComponent (players can silence the timer's repeated beeps).
- TimerBlockIndicatorComponent: the status glow (idle, running, paused, no power). Its definition names the model's emissive mesh parts and the material for each state; with its lists empty the block shows no glow, and nothing reports it.
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity with Amount 0: it draws nothing but needs power on the grid. The countdown stops while the block is not working.
- No dummy is needed for the timer to work.

Set by players on each built block, in its terminal and toolbar: the delay, start and stop, and the actions. These belong to each built block: a mod does not set them on the block, and the Editor does not show them.

The vanilla timer: Assets/Blocks/Timer/50. It uses the template's shared TimerBlockDefinition and audio definition, and an indicator definition of its own for the glow.

The Timers template: a block made from it gets its own PowerableBlockDefinition (Amount 0, in the vanilla timer's G-Screen family) but keeps the template's shared TimerBlockDefinition. A delay range of its own needs a definition of its own: a copy of the shared one, set on the block's TimerBlockServerComponent.
