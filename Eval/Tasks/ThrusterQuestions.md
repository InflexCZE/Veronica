# Thruster questions

## Prompt

What would I need to make my own thruster block?

Second message, after the model has answered the first:

And how would I make it hold items too, like a small cargo container?

## User

- Is planning a mod and asks before starting. Has nothing in the mod yet: no models imported, no name, no folder.
- Wants an answer, not the work done. If the model starts asking for names, folders or values to begin building, the user says they are only asking for now and have not decided yet. If the model asks whether to start, the user says not yet.
- The second question is about the same thruster as the first; the user does not repeat what it was.
- Does not know engine internals: component and definition types, tags, field names, file paths in vanilla.
- The run ends after the model's answer to the second question.

## Pass

1. Neither answer changes the project: no `write`, `element_add`, `element_remove`, `create_folder`, `create_prefab`, `create_partial`, `create_definition` or `component_add`, and no `activate_skill`. Reading, `skill` and `wiki` are fine.
2. Each question is answered to the user in the same turn it was asked, not deferred to a later turn or turned into questions back.
3. The first answer sums up what the user brings: the block's models (Model, and FractureModel and DamagedModel for a breakable block), an optional icon, a name players see, a mod folder, and which vanilla thruster to match (class and size) or their own thrust values.
4. The first answer sums up the steps: a block from a thruster template, filling its name, icon and models, the thrust values, and the nozzle dummies placed by the user in the Editor.
5. The second answer builds on the same thruster block: it adds an inventory with a capacity the user picks, and conveyors with port dummies placed by the user, on top of the thrust, not as a new block.
6. Nothing invented: no component, definition, field or folder that does not exist, and no claim that contradicts the wiki pages.

Not required, a plus: the second answer says that a thruster made from a thruster template already draws power, or that thrust and storage are separate components that live side by side on one block.

## Fixture

None: a clean dev mod.
