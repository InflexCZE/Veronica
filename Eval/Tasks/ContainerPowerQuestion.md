# Container power question

## Prompt

If I put thrusters on my container block, will they drain my batteries like the vanilla ion thrusters do?

## User

- Has a finished cargo container block, "My Container", in `Assets/MyContainerBlock`, made from the Cargo Containers template. It has no thrust yet.
- Is asking before deciding: wants an answer, not the work done. If the model offers to start or asks for values to build, the user says not yet, they want to understand it first.
- Follow-up, after the answer: "So what would I have to do to make it use power?" The run ends after the answer to it.
- Does not know engine internals: component and definition types, tags, field names, file paths in vanilla.

## Pass

1. Neither answer changes the project: no `write`, `element_add`, `element_remove`, `create_folder`, `create_prefab`, `create_partial`, `create_definition`, `component_add` or `component_upgrade`, and no `activate_skill`.
2. The first answer says no: thrust added to this block would not draw power, because the block's main component is a `FunctionalBlockComponent`, not a `PowerableBlockComponent`. The model or its researcher read the block to say so, not only a general rule.
3. The second answer says the block component would be upgraded to a `PowerableBlockComponent` (`component_upgrade`), keeping the block's settings, with a `PowerableBlockDefinition` whose consumed resource comes from a thruster or other powered block, and that the thrusters then draw power. No made-up tool or field.
4. Each question is answered in the turn it was asked.
5. Nothing invented: no component, definition, field or value that does not exist, no power numbers that were not read.

## Fixture

`Eval/Tasks/ContainerPowerQuestion/`: the imported MyThruster models and the "My Container" block in `Assets/MyContainerBlock` (the MyContainerThrust fixture).
