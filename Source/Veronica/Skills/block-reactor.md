---
name: block-reactor
description: Use when a block should make power by burning fuel like a reactor, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Reactors template
- the fuel: an item the block's fuel inventory accepts
- the power it makes and the energy a unit of fuel gives: the user's own, or as a vanilla reactor (Assets/Blocks/Reactor)
- how much fuel it holds
- conveyor ports: how many and on which faces, the user's choice for their model; their spots from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its ItemToResourceConverterDefinition, its fuel InventoryDefinition, its InventoryStockpileDefinition and its model definition's dummies
- Get from the user what this needs: the fuel, the power, the energy per unit of fuel, how much fuel it holds, how many conveyor ports and where, and for a new model that they will place them
- Set the converter: the fuel, the power and the energy per unit of fuel
- Set the fuel inventory and the fuel the block pulls from the conveyors
- Set up the conveyor ports: UseConveyor dummies, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; the ItemToResourceConverterComponent, the InventoryComponent and the InventoryStockpileComponent name their definitions, which a block made from the template owns. Read all three and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, for the fuel, the power it makes, the energy a unit of fuel gives, how much fuel it holds, and how many conveyor ports it has and on which faces; offer a vanilla reactor as a suggestion. How long a unit of fuel lasts follows from the energy and the power drawn: fuel that should "last as long as" in a reactor of another power means either the same energy per unit (it burns faster at more power) or the same burn time (more energy per unit); ask which. The template burns no fuel (ItemConsumed none) and makes next to no power: its values were not chosen for this block. A vanilla reactor's ports fit its own model, not the user's. For a new model, say they will place the ports in the Editor.
3. Converter, on the ItemToResourceConverterDefinition:
   write(def, "ItemConsumed", <the fuel's ItemDefinition>). Without it the block makes no power.
   MaxResourceProduction: the most power it makes, in kW. ResourcesPerItem: the energy one unit of fuel gives, in kW*s (kJ); at full power it burns MaxResourceProduction / ResourcesPerItem units a second, so a bigger ResourcesPerItem makes fuel last longer. MaxItemsPerSecondConsumed does not apply to this block and is left as it is. Write the values that do not fit what the user wants.
4. Fuel inventory, on the InventoryDefinition: MaxMass is how much fuel it holds, in kg. Its WhitelistedItems decide which items go in; the fuel must be one of them.
   Fuel from the conveyors: the block pulls the stockpile's Items from the conveyor network whenever its inventory is filled below FillRatioPull (a share, 0 to 1). The template leaves both empty, so the block takes no fuel by itself. element_add(stockpile definition, "Items"), write(stockpile definition, "Items[0]", <the fuel>), and write FillRatioPull as a vanilla reactor has it.
5. Conveyor ports: fuel reaches the block through the conveyor network, which connects only at UseConveyor dummies, and a player loads fuel by hand at them too; a block made from the template has none. element_add(BlockModelComponentDefinition, "Dummies") once per port, then write its Name and Type (Assets/System/DummyTypes/UseConveyor.def).
   Positions: for a model another block uses, copy the Transform and Scale of that block's UseConveyor dummies; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the conveyor ports in the Render View (Preview Settings, Dummies).
