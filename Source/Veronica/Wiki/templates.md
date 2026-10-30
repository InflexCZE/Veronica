---
name: templates
title: Block templates
source: moved from the create-block, block-thruster and block-inventory skills
description: What a block template brings: breakable or not, the files create_prefab generates, the family and G-Screen place the new block joins, and the parts thruster and storage templates come with.
---
find_template("<what it does>") lists the templates of a function; create_prefab(template, name, folder) makes a new placeable block from one.

- Breakable templates expect a FractureModel and a DamagedModel besides the Model; NonBreakable ones do not.
- create_prefab's reply lists every generated file with its definition type. File names differ per template, so files are found by their definition type, not by a name.
- The template's BlockKind makes the new block a size variant of the template's vanilla family, in that family's G-Screen place. Another place is a change of its own (the add-to-gscreen skill).
- A thruster template (find_template("thrust")) brings a ThrusterComponent with its own ThrusterDefinition, a ThrusterEffectsComponent for the flame, one Thruster-type dummy, and a PowerableBlockComponent as the block component.
- A storage template brings an InventoryComponent and a ConveyorComponent.
- A block made from a template for another function has none of these parts; they are added with component_add (the block-components page).
