---
name: create-block
description: Use when the user wants a new placeable block, whatever it does.
---
Needs:
- name: the block's name as players see it
- folder: a mod folder for the new files, e.g. Assets/MyBlock
- models: the block's three .model assets, imported into the mod: Model, FractureModel and DamagedModel (the last two when the block breaks)
- icon: an imported .png (optional)
- what the block does: thrusts, lights, stores, ...

Plan:
- Get the name and folder from the user
- Create the prefab from the template of what the block does
- Fill block name and icon
- Fill the models: the Model, FractureModel and DamagedModel assets
- Next skill: activate the skill of what the block does; without one, mirror a vanilla block's values and show the block

Steps:
1. Ask the user for the name and folder, unless the user named them, and wait for the answer.
2. find_template("<what it does>") and pick the template of that function. Breakable templates expect FractureModel and DamagedModel, NonBreakable ones do not. A seat to fly the ship from: Cockpit also stores items and has conveyor ports, Controllable Seat has neither; ask the user which. Non-Controllable Seat seats no one. A battery: Battery, built like the vanilla batteries with power cells; Batteries is an older one without cells.
3. create_prefab(template, name, folder). The reply lists every generated file with its definition type; name files from that list, as file names differ per template.
4. On the block definition: the definition of the block component, e.g. PowerableBlockDefinition or FunctionalBlockDefinition. It holds UIData; BreakableBlockDefinition also ends in BlockDefinition but holds only how the block breaks.
   write(def, "UIData.Name", name)
   write(def, "UIData.Icon", icon) when there is an icon
   Its BlockKind already makes the block a size variant of the vanilla family of the template, in that family's G-Screen place. For another place, activate add-to-gscreen once this plan is done.
   Some templates (Lights (Point)) leave BlockKind none: such a block has no G-Screen place, so players cannot pick it. Ask the user where it goes: the family of a vanilla block of the same function (write that block's BlockKind), or a place of its own with add-to-gscreen.
5. On the BlockModelComponentDefinition:
   write(def, "Model", <Model>)
   write(def, "FractureModel", <FractureModel>) and write(def, "DamagedModel", <DamagedModel>) when the block breaks. FractureModel is the block broken into pieces (vanilla names it Fractured), DamagedModel the dented block (vanilla names it Deformed). Models another block already uses are copied from its BlockModelComponentDefinition as they are.
6. For each thing the block does, activate its skill when the skill list has one (block-thruster for thrust, block-inventory for storage, block-cockpit for a seat to fly from, block-light for a lamp or spotlight, block-gyro for turning the ship, block-antenna for broadcasting, block-solar for power from sunlight, block-ore-detector for finding ore, block-sensor for detecting what comes near, block-battery for storing power, block-timer for actions after a delay, block-sound for playing sounds, block-wind-turbine for power from wind, block-gravity for artificial gravity, block-tank for storing hydrogen or oxygen, block-reactor for power from fuel, block-landing-gear for locking to a surface, block-warhead for exploding, block-production for crafting items). Otherwise read the template's definition for that function and the same definition of a comparable vanilla block (find_content in Assets/Blocks), and write the values that should match the new block.

Show:
show(the block's prefab). Ask the user to check the model in the Render View, and the block's dummies (Preview Settings, Dummies) when its model definition has any.
