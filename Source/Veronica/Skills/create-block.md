---
name: create-block
description: Use when the user wants a new placeable block, whatever it does.
---
Needs:
- name: the block's name as players see it
- folder: a mod folder for the new files, e.g. Assets/MyBlock
- model: the block's .model, imported into the mod; also its fractured and deformed .model when the block breaks
- icon: an imported .png (optional)
- what the block does: thrusts, lights, stores, ...

Plan:
- Get the name and folder from the user
- Create the prefab from the template of what the block does
- Fill block name and icon
- Fill the models: model, fractured and deformed; only the model fields, the dummies belong to the function's skill
- Next skill: activate the skill of what the block does; without one, mirror a vanilla block's values and show the block

Steps:
1. activate_skill("create-block"), then ask the user for the name and folder, unless the task gives them, and wait for the answer.
2. find_template("<what it does>") and pick the template of that function. Breakable templates expect fractured and deformed models, NonBreakable ones do not.
3. create_prefab(template, name, folder). The reply lists every generated file with its definition type; name files from that list, as file names differ per template.
4. On the block definition (its type ends in BlockDefinition, e.g. PowerableBlockDefinition):
   write(def, "UIData.Name", name)
   write(def, "UIData.Icon", icon) when there is an icon
   Its BlockKind already makes the block a size variant of the vanilla family of the template, in that family's G-Screen place. For another place, activate add-to-gscreen once this plan is done.
5. On the BlockModelComponentDefinition (create_prefab makes no model files; the model fields take any .model in the project as it is):
   write(def, "Model", model)
   write(def, "FractureModel", fractured) and write(def, "DamagedModel", deformed) when given
6. For each thing the block does, activate its skill when the skill list has one (block-thruster for thrust). Otherwise read the template's definition for that function and the same definition of a comparable vanilla block (find_content in Assets/Blocks), and write the values that should match the new block.

Show:
show(the block's prefab). Ask the user to check the model and its dummies in the Render View (Preview Settings, Dummies).
