---
name: block-audio
title: Block sounds: which sounds a block plays and when, looping and distance, game audio events
source: SE2 source BlockEffectsComponentDefinition, BlockEffectsComponent, ThrustEffectsComponentDefinition, SoundBlockDefinition, AudioConfiguration (FmodStudioProjectHash); vanilla thrusters (Assets/Blocks/Thrusters), the vanilla sound block (Assets/Blocks/Sound/25)
description: Where a block's sounds are set and when each one plays (working, damaged, destroyed, built, taken apart), why a sound does not loop or is heard everywhere, and how to use the game's own audio events or bring new sounds.
---
Where block sounds are set: the block's BlockEffectsComponentDefinition, Audio group. Each field names an audio event, e.g. event:/SFX/Block/ThrusterIonMID; an empty field plays nothing.
- OnUseAudio: starts when the block starts working (built, on, powered) and stops when it stops working. It does not follow what the block is doing: a thruster's plays while it is working, firing or not.
- OnDamagedAudio: plays when the block takes damage; DamageIntegrityThresholds lists integrity values, and the sound plays as integrity falls below each.
- OnDestroyedAudio: plays when the block is fully destroyed; not an explosion sound, explosions have effects of their own.
- OnConstructionAudio and OnDeconstructionAudio: play at each build stage reached while building or grinding; the event gets the build progress as a BuildProgress parameter.
- The block sends OnUseAudio an OnOff parameter (On, Off) as it starts and stops working, and a Process parameter (1, 0). Names and values are fields of the definition (Audio/Parameters); they matter only to an event built to react to them.
- OnActivationAudio and AudioPlaysWhenNoThrust belong to ThrustEffectsComponentDefinition (jetpack and ship thrust effects), not to a block.

Looping and distance:
- The definition only names the event. Looping, 3D distance falloff and what happens on Off are set in the audio event itself; the definition has no loop or distance setting.
- A block sound that does not loop or is heard everywhere is fixed in the event, outside the Editor.

Audio events:
- The game's sounds are FMOD events packed in .bank files. Any vanilla event can be used by its name as it appears in a vanilla definition (a vanilla block's BlockEffectsComponentDefinition, the sound block's SoundBlockDefinition); nothing needs extracting.
- A new sound is an FMOD event made in FMOD Studio and shipped in the mod's own banks (AudioConfiguration's FmodStudioProjectHash); .wav files are not used. The Editor only picks event names.
