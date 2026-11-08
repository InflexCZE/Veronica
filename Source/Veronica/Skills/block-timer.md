---
name: block-timer
description: Use when a block should run actions after a delay players set, like a timer block, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Timers template
- the shortest and longest delay players can set: the user's own, or as the vanilla timer (Assets/Blocks/Timer)
- a folder for the block's own timer definition, e.g. the block's folder

Plan:
- Read the block: its TimerBlockServerComponent's TimerBlockDefinition and its delay range
- Get from the user what this needs: the shortest and longest delay players can set
- Set the delay range on a TimerBlockDefinition of the block's own
- Show the block

Steps:
1. read(block) lists its components; the TimerBlockServerComponent names its TimerBlockDefinition. Read DurationMin and DurationMax on it.
2. Ask the user, with ask_user, for the shortest and longest delay players can set; offer the vanilla timer's range as a suggestion. A range left from the template was not chosen for this block.
3. Delay range: DurationMin and DurationMax bound the delay slider in the block's terminal. A block made from the template uses the template's shared TimerBlockDefinition, a vanilla file. For a range of its own:
   create_partial(that TimerBlockDefinition, "copy", folder), then write(block, "Components[TimerBlockServerComponent].Definition", <the copy>), then write(copy, "DurationMin", "hh:mm:ss") and write(copy, "DurationMax", "hh:mm:ss").
   Both are TimeSpans written as text, e.g. "00:00:10" for 10 seconds or "03:00:00" for 3 hours; a bare number is read as days. DurationMin stays below DurationMax.
4. The delay a built block runs with, and the actions it triggers, are set by players on each built block in its terminal and toolbar; a mod does not set them on the block. Say so when the user asks for them.

Show:
show(the block). The timer is tested in game: players set the delay and the actions in the block's terminal.
