---
name: create-block
description: Use when the user wants a new placeable block, whatever it does: a light, thruster, door or any other; the block-* skills then add its function to the block it makes.
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
- Give the block a G-Screen place when the template leaves BlockKind none: ask the user where it goes
- Fill the models: the Model, FractureModel and DamagedModel assets
- Next skill: activate the skill of what the block does; without one, mirror a vanilla block's values and show the block
- Survival unlock: activate block-survival when the user wants the block buildable in survival

Steps:
1. Ask the user for the name and folder, unless the user named them, and wait for the answer.
2. find_template("<what it does>") and pick the template of that function. Breakable templates expect FractureModel and DamagedModel, NonBreakable ones do not. A seat to fly the ship from: Cockpit also stores items and has conveyor ports, Controllable Seat has neither; ask the user which. Non-Controllable Seat seats no one. A battery: Battery, built like the vanilla batteries with power cells; Batteries is an older one without cells. A block turning an item into power or gas: find_template matches words in the template's name, so search both "reactor" and "generator" (Gravity Generators is not one); more than one can fit (Reactors, O2H2 Generator), so show the user those that do and what each brings, as read or from the wiki, not guessed from its name, and ask which.
3. create_prefab(template, name, folder). The reply lists every generated file with its definition type; name files from that list, as file names differ per template.
4. On the block definition: the definition of the block component, e.g. PowerableBlockDefinition or FunctionalBlockDefinition. It holds UIData; BreakableBlockDefinition also ends in BlockDefinition but holds only how the block breaks.
   write(def, "UIData.Name", name)
   write(def, "UIData.Icon", icon) when there is an icon; without one, the template's icon stays: the game requires an icon, so it is never cleared to none
   Its BlockKind already makes the block a size variant of the vanilla family of the template, in that family's G-Screen place. For another place, activate add-to-gscreen once this plan is done.
   Some templates (Lights (Point)) leave BlockKind none: such a block has no G-Screen place, so players cannot pick it. Ask the user where it goes: the family of a vanilla block of the same function (write that block's BlockKind), or a place of its own with add-to-gscreen.
5. On the BlockModelComponentDefinition:
   write(def, "Model", <Model>)
   write(def, "FractureModel", <FractureModel>) and write(def, "DamagedModel", <DamagedModel>) when the block breaks. FractureModel is the block broken into pieces (vanilla names it Fractured), DamagedModel the dented block (vanilla names it Deformed). Models another block already uses are copied from its BlockModelComponentDefinition as they are.
6. For each thing the block does, activate its skill when the skill list has one (block-thruster for thrust, block-inventory for storage, block-cockpit for a seat to fly from, block-light for a lamp or spotlight, block-gyro for turning the ship, block-antenna for broadcasting, block-solar for power from sunlight, block-ore-detector for finding ore, block-sensor for detecting what comes near, block-battery for storing power, block-timer for actions after a delay, block-sound for playing sounds, block-wind-turbine for power from wind, block-gravity for artificial gravity, block-tank for storing hydrogen or oxygen, block-item-to-resource for turning an item into power, hydrogen or oxygen (a reactor, an O2/H2 generator), block-landing-gear for locking to a surface, block-warhead for exploding, block-production for crafting items, block-welder for welding blocks in front of it, block-drill for digging and collecting ore, block-button for buttons players press, block-connector for docking to another ship, block-weapon for firing like a gatling gun, block-door for a door that opens). Otherwise read the template's definition for that function and the same definition of a comparable vanilla block (find_content in Assets/Blocks), and write the values that should match the new block. A dummy the vanilla block has and the new block needs (a gun's barrel, a conveyor port) is added with element_add to the new block's Dummies with its Type only: vanilla positions fit vanilla's model, not this one, so ask the user to position it.
7. In survival with progression on, a new block is locked unless its kind is unlocked from the start, and nothing in vanilla unlocks it later, not its recipe or tech level. When the user wants it buildable from the start, activate block-survival: it checks the kind and unlocks the block if needed. Otherwise it is buildable only in creative or with progression off, or from the start when its kind already is. Whether its kind already is, only block-survival's check tells: ask the user without saying whether this block is locked or buildable now.

Show:
show(the block's prefab). Ask the user to check the model in the Render View, and the block's dummies (Preview Settings, Dummies) when its model definition has any.
