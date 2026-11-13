---
name: block-item-to-resource
description: Use when a block should turn an item into power, hydrogen, oxygen or several of them, like a reactor or an O2/H2 generator, or when such a block should make another resource too; for one create-block just made or one that already exists. It does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, with an ItemToResourceConverterComponent
- the item it consumes
- what it makes: each resource and how much one unit of the item makes of it; the user's own, or as a vanilla block (reactors: Assets/Blocks/Reactor, O2/H2 generators: Assets/Blocks/O2H2Generator)
- how fast it consumes the item, or the most it makes of one resource
- the power it draws while converting, if any
- how much of the item it holds, and its conveyor ports

Plan:
- Read the block's converter and whether the block draws power
- Get from the user what this needs
- Set the input: the item it consumes and how fast
- Set the outputs
- Set the power it draws while converting
- Set up the input inventory: activate block-inventory
- Set up taking the item from the conveyors: activate inventory-pull
- Show the block

Steps:
The converter holds its outputs in one of two forms, and each step below writes only the fields of the form the block uses (the item-to-resource page):
- one resource: the single output, ResourceProduced with MaxResourceProduction and ResourcesPerItem;
- several resources: the list, ResourcesProduced with MaxItemsPerSecondConsumed.
The game reads only one form; the other form's fields are not offered, asked or written. A block with the single output that should make another resource moves to the list (step 4).
Units: Electricity in kW, and kW*s (kJ) per unit of the item; Hydrogen and Oxygen in L/s, and L per unit.

1. read(block) lists its components; the ItemToResourceConverterComponent names its ItemToResourceConverterDefinition, which a block made from a template owns. Read it and the block definition's ConsumedResource. Its form: ResourcesProduced with entries is the list, otherwise the single output.
2. Ask the user, with ask_user, for what the Needs list in what they care about: the item, what it makes and how much per unit, how fast or how much at most, the power while converting; for a block that already works, only what changes. Offer a vanilla block as a suggestion, its values read from vanilla. Values left from the template were not chosen for this block. How long a unit lasts follows from how much it makes and how much is drawn: an item that should "last as long as" in a block of another output means either the same amount per unit (consumed faster at more output) or the same time per unit (more per unit); ask which.
3. Input: write(def, "ItemConsumed", <the item's ItemDefinition>); without it the block makes nothing. How fast: single output, it follows from step 4 (MaxResourceProduction / ResourcesPerItem units a second); list, MaxItemsPerSecondConsumed units a second.
4. Outputs:
   - single output: ResourceProduced (the resource, Assets/System/ResourceTypes), ResourcesPerItem (how much one unit makes), MaxResourceProduction (the most per second).
   - list: per resource element_add(def, "ResourcesProduced"), then write its Resource, ConversionRate (how much one unit makes; a new entry starts at 0, which the game rejects) and ProductionPriority.Priority (as the vanilla block, or the list's other entries). Each resource's most per second is MaxItemsPerSecondConsumed * its ConversionRate. Then write(def, "ResourceProduced", null): while it is set too, the game warns.
   - single output to list, keeping what it makes: the old resource as the first entry with ConversionRate = ResourcesPerItem and the old ProductionPriority.Priority, MaxItemsPerSecondConsumed = MaxResourceProduction / ResourcesPerItem, then the new entries, then ResourceProduced null.
   Tell the user the figures that follow: the most of each resource per second, how fast the item is consumed.
5. Power while converting: PowerableBlockResourceRequiredToOperate, in kW, drawn only while converting; none when it needs no power to convert. It applies only with the block definition's ConsumedResource set (Electricity); with ConsumedResource none it is never drawn.
6. activate block-inventory for the inventory the converter takes the item from (the InventoryComponent tagged InventoryInput). It must accept the item: that is settled here, not asked. Its conveyor ports bring the item in, and hydrogen and oxygen leave the block only through them.
7. activate inventory-pull for the same block, pulling the item: settled here, not asked.

Show:
show(the block). Ask the user to check the conveyor ports in the Render View (Preview Settings, Dummies).
