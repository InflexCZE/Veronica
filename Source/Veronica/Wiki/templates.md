---
name: templates
title: Block templates
source: vanilla block templates (Assets/Templates/Blocks), read with find_template, create_prefab and read
description: What a block template brings: breakable or not, the files create_prefab generates, the family and G-Screen place the new block joins, and the parts thruster and storage templates come with.
---
find_template("<what it does>") lists the templates of a function; create_prefab(template, name, folder) makes a new placeable block from one.

- Breakable templates expect a FractureModel and a DamagedModel besides the Model; NonBreakable ones do not.
- create_prefab's reply lists every generated file with its definition type. File names differ per template, so files are found by their definition type, not by a name.
- The template's BlockKind makes the new block a size variant of the template's vanilla family, in that family's G-Screen place. Another place is a change of its own (the add-to-gscreen skill).
- A thruster template (find_template("thrust")) brings a ThrusterComponent with its own ThrusterDefinition, a ThrusterEffectsComponent for the flame, one Thruster-type dummy, and a PowerableBlockComponent as the block component.
- A storage template (Cargo Containers) brings an InventoryComponent and a ConveyorComponent, the explosion component vanilla containers carry, and a FunctionalBlockComponent as the block component. It brings no conveyor port dummies: the dummies list is empty until ports are added.
- A template's own values, such as its Recipe, icon and names, come from some vanilla block of its family. They say nothing about the new block: the Cargo Containers template's recipe is CargoContainer150's, whatever size the new block is.
- A block made from a template for another function has none of these parts; they are added with component_add (the block-components page).
