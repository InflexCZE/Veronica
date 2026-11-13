---
name: inventory-pull
description: Use when a block's inventory should take items from the conveyors by itself, keeping a stock of them like a reactor keeps its fuel, on a block that already has the inventory.
---
Needs:
- the block: its prefab binding or any of its files, with the inventory to fill
- which items it pulls: named by the skill that activated this one (a fuel, an ingredient), otherwise the user's choice
- how full it keeps itself: as a vanilla block of the same kind, unless the user asks for another

Plan:
- Read the block's stockpile and the inventory it fills
- Get from the user what this needs and no skill named
- Set the items it pulls and when it pulls them
- Show the block, unless another skill activated this one: that skill shows it

Steps:
1. read(block) lists its components; the InventoryStockpileComponent names its InventoryStockpileDefinition. Read it. A block made from a template owns it; a definition the block does not own is copied first (create_partial(<it>, "copy", folder), then written to the component). A block without the component: component_add(block, "InventoryStockpileComponent").
2. Ask the user, with ask_user, for what the Needs list and neither the block nor the activating skill settled. Values left from a template were not chosen for this block.
3. On the InventoryStockpileDefinition: while the inventory is filled below FillRatioPull (a share of its capacity, 0 to 1), the block pulls its Items from the conveyor network, up to FillRatioPull * (1 + FillRatioPullMargin) (margin 0.5 when unset): 0.01 tops up to 1.5% of capacity when nearly empty, it does not keep the inventory full. FillRatioPull none or no Items: it pulls nothing, and items reach it only pushed in or loaded by hand. Per item: element_add(def, "Items"), write(def, "Items[<n>]", <item>). Write FillRatioPull as the user wants or as the vanilla block has it. The inventory must accept the items (block-inventory) and reach the conveyors through ports.

Show:
When another skill activated this one, mark_skipped: that skill shows the block at its end. Otherwise show(the block).
