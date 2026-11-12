---
name: add-item
description: Use when the mod needs a new inventory item, such as a component, ore or other item players carry, craft or drop.
---
Needs:
- what kind of item it is: a vanilla item it is like (a component, an ore, a tool)
- name and folder, e.g. Assets/<Name>
- its mass per unit
- its icon: a png the user imported into the mod
- its look when dropped: a .model the user imported into the mod
- what makes it, when it is crafted: the recipe goes on the block that crafts it (block-production)

Plan:
- Read a vanilla item of the same kind
- Get from the user what this needs: name and folder, mass, the icon and the model
- Create the item definition and write its values
- Create its dropped look from the Floating Object template and give it the model
- Map the item to its dropped look in the floating object configuration
- Show the item

Steps:
1. find_content with the vanilla item's name (e.g. "SteelPlate") finds its ItemDefinition, a .def or a partialdef. read it: Type decides which inventories accept the item (a block's input and output inventories, the character's), Flags whether units stack and whether amounts may be fractions. Consumables, datapads and quest items have their own definition types: copy a vanilla one of that type instead (create_partial(<it>, "copy", folder)).
2. Ask the user, with ask_user, the name and folder, the mass per unit, and which imported icon and model it uses; find_content in the mod finds the png and the .model to offer. When a recipe will make it, its mass is at most the summed mass of the recipe's inputs (Amount times their MassPerUnit): validation flags heavier outputs.
3. create_definition("ItemDefinition", <name>, folder). write DisplayName, Type and Flags as the vanilla item has them, MassPerUnit, and Icon (the png).
4. create_prefab("Floating Objects/Floating Object", <name>, folder). The reply lists the prefab chain and a ModelComponentDefinition copy that both compositions already use and that carries the pickup dummy: write(that ModelComponentDefinition, "Model", <the .model>). create_prefab refuses a name whose files would collide with a file already in the folder, the item definition from step 3 included: give the prefab its own name, e.g. <name>Dropped. The user's icon keeps its own name.
5. A dropped item finds its look through FloatingObjectPrefabsConfiguration; without an entry it shows a plain cube. find_content("FloatingObjectPrefabsConfiguration"), create_partial(<it>, "override", folder); when the mod already has an override of it, the error names that file: use it. element_add(override, "ItemPrefabs", key=<the ItemDefinition>, value=<the prefab's _Server definition>).
6. When a block should craft it: its recipe is set up on that block with block-production. When this skill was started from block-production, that skill resumes and does it.

Show:
show(the item's prefab binding). Tell the user the icon shows in inventories in game, and the dropped look when a unit is thrown out of an inventory.
