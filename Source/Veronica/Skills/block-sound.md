---
name: block-sound
description: Use when a block should play sounds players pick, like a sound block or speaker, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, e.g. one made from the Sound Block template
- which sounds players can pick: the vanilla sound block's list, or the list of another block that has one

Plan:
- Read the block: whether it has the sound components, and the SoundBlockDefinition of its server one
- Add the sound components the block lacks: the server one with the sound list, the client one that plays
- Show the block

Steps:
1. read(block) lists its components. A sound block needs two: SoundBlockServerComponent (server) with a SoundBlockDefinition, and SoundBlockComponent (client), which takes no definition. The Sound Block template brings neither: a block made from it plays nothing until both are added.
2. The SoundBlockDefinition's AvailableEvents is the list of sounds players pick from in the block's terminal. Sounds come from audio events already in the game; a mod cannot add new ones in the Editor. Without another wish from the user, the block offers the vanilla sound block's list: find it with find_content("SoundBlockDefinition") in Assets/Blocks/Sound.
3. component_add(block, "SoundBlockServerComponent", <that SoundBlockDefinition>), then component_add(block, "SoundBlockComponent"). The block uses the vanilla list as it is, so it points at the vanilla file; nothing is copied.
4. read(block) again: both components are listed and none "needs" a definition.
5. Which sounds play, the volume, looping and the order are set by players on each built block in its terminal; a mod does not set them on the block. Say so when the user asks for them.

Show:
show(the block). The sounds are tested in game: players pick them in the block's terminal.
