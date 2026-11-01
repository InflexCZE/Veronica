---
name: sound-blocks
title: Sound blocks: the sound list, playback, per-block settings, power
source: SE2 source SoundBlockServerComponent, SoundBlockComponent, SoundBlockSyncComponent, SoundBlockDefinition, SoundBlockDetailModel, PowerableBlockDefinition; the vanilla sound block (Assets/Blocks/Sound/25), the Sound Block template
description: What a sound block is made of: its sound list and where sounds come from, the component that plays them, what players set on each block, power, and what the Sound Block template lacks.
---
Parts:
- SoundBlockServerComponent, on the server, with its SoundBlockDefinition. AvailableEvents is the list players pick from in the block's terminal: each entry has a DisplayName (a localization key, the name players see) and an AudioEvent (the game's audio event, e.g. event:/UI/SoundBlock/...).
- SoundBlockComponent, on the client: plays the sound. It takes no definition. It needs a SpatialAudioComponent, which block templates already have.
- PowerableBlockComponent as the block component. Its PowerableBlockDefinition's ConsumedResource is Electricity with Amount 0: it draws nothing but needs power on the grid. A sound stops while the block is not working and plays again when it works.
- No dummy is needed for the sound.

Sounds:
- AudioEvents are audio events already built into the game. A new sound needs audio work outside the Editor; in the Editor a mod picks from the existing events.
- A shorter list for a block of its own is a create_partial copy of the definition with entries removed, set on the block's SoundBlockServerComponent.

Set by players on each built block, in its terminal: the playlist, volume, looping, loop delay, random order, play and stop. These belong to each built block: a mod does not set them on the block, and the Editor does not show them.

The vanilla sound block: Assets/Blocks/Sound/25. It is built from the Sound Block template and adds both sound components, the server one with a SoundBlockDefinition of its own. Like the template it does not break apart (no BreakableBlockComponent).

The Sound Block template: a block made from it gets its own PowerableBlockDefinition (Amount 0, in the vanilla sound block's G-Screen family) but no sound components: it plays nothing until SoundBlockServerComponent and SoundBlockComponent are added.
