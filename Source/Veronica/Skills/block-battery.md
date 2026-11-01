---
name: block-battery
description: Use when a block should store electricity like a battery, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Battery template
- how fast it charges and gives power: the user's own, or as a vanilla battery (Assets/Blocks/Batteries/Battery/<size in cm>)
- where it goes in the block menu: with the vanilla batteries, or a place of its own
- the cell spot: from the block that already uses the model, or the user positions it in the Editor

Plan:
- Read the block: its PowerableBlockDefinition, its ResourceContainerInventoryDefinition, its cell slot inventories and its model definition's dummies
- Get from the user what this needs: charge and output rates, the block menu place, and for a new model that they will place the cell spot
- Set the battery: the rates on its ResourceContainerInventoryDefinition, no power draw and the block menu place on its PowerableBlockDefinition
- Set up the cell slot dummy: one CellSlot-type dummy per cell slot, placed by copying or by the user; read it back
- Show the block

Steps:
1. read(block) lists its components: the PowerableBlockComponent names its PowerableBlockDefinition, the ResourceContainerInventoryComponent its ResourceContainerInventoryDefinition, and each InventoryComponent tagged CellSlot<n> is one cell slot. Read both definitions and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, how fast it charges and how much power it gives at most, and where it goes in the block menu; offer a vanilla battery to match as a suggestion (list_folder shows the sizes). Values left from the template were not chosen for this block. For a new model, say in the same message that they will place the cell spot in the Editor.
3. Battery, on the ResourceContainerInventoryDefinition:
   write(def, "MaxDischargeRate", n): the most power it gives, in kW.
   write(def, "MaxChargingRate", n): the most power it takes while charging, in kW. It must stay below MaxDischargeRate, so a battery meant to charge as fast as it discharges takes a hair less (vanilla: 1 kW less).
   The energy it holds comes from the power cells in its slots, not from a field: MaxCapacity is set to what the cells hold, as vanilla does; ask the wiki for a cell's energy.
   On the PowerableBlockDefinition:
   write(def, "ConsumedResource", null). Vanilla batteries need no power to run; the template's ConsumedResource makes the battery count as unpowered until something else powers the grid, and an unpowered battery gives nothing.
   The template leaves BlockKind none, so players cannot pick the block: write the BlockKind of a vanilla battery for the vanilla batteries' place, or activate add-to-gscreen once this plan is done.
4. Cell slot dummy: each cell slot needs a dummy whose Type is CellSlot, in slot order; the cell shows there, and without one the block fails to load. A block made from the template has one slot and no such dummy: element_add(BlockModelComponentDefinition, "Dummies"), then write its Name ("CellSlot 0") and Type (Assets/System/DummyTypes/CellSlot.def).
   Positions: for a model another block uses, copy the Transform and Scale of that block's CellSlot dummies; for a new model, the user places it: never compute positions. Then show(the block), ask the user to check or move it (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the position read back.

Show:
show(the block). Ask the user to check the CellSlot dummy in the Render View (Preview Settings, Dummies).
