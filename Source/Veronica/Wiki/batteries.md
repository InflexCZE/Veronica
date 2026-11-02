---
name: batteries
title: Batteries: power cells, charge and output rates, power draw and the cell slots
source: SE2 source ResourceContainerComponent, ResourceContainerInventoryComponent, ResourceContainerDefinition, InventorySlotsServerComponent, InventorySlotRenderComponent, InventoryObjectBuilder, PowerableBlockComponent, PowerableBlockDefinition; vanilla batteries (Assets/Blocks/Batteries/Battery), PowerModule item, the Battery and Batteries templates
description: How a battery block stores and gives electricity: power cells in slots, charge and output rates and their unit, why vanilla batteries draw no power, the CellSlot dummies, what the template brings, and the parts that are only looks.
---
Parts:
- ResourceContainerInventoryComponent, on the server, with its ResourceContainerInventoryDefinition: MaxDischargeRate and MaxChargingRate, both in kW. MaxChargingRate must be below MaxDischargeRate; vanilla keeps it 1 kW below (0.1 kW on the smallest).
- Cell slots: each is an InventoryComponent that holds one power cell, tagged with a name ending in its number (CellSlot0, CellSlot 1, ...); slots are ordered by that number.
- PowerableBlockComponent as the block component.

Energy:
- The energy a battery holds is the sum of the power cells in its slots, each as charged as it is. A cell holds the MaxStored of its ResourceContainerItemDefinition, in kW*s (3600 kW*s is 1 kWh). An empty slot holds nothing; a battery with no cells has no capacity and gives no power.
- Charging and discharging charges and drains the cells themselves, so a cell keeps its charge when moved to another battery.
- MaxCapacity on the definition is replaced at run time by what the cells hold; vanilla sets it to the cells' total.
- A newly built block starts with the cells listed in its prefab's slots; the Battery template's one slot holds one PowerModule, partly charged.
- A slot's starting cell is an item in the server prefab's InventoryObjectBuilder for that slot; an empty item list builds the slot with no cell.
- A cell's starting charge is its MetaData FillRatio (0 to 1), set in two places: the prefab's slot item for a built block, and the cell item definition's DefaultMetaData for a cell crafted or spawned as an item. Vanilla uses 0.3 in both.

Power draw:
- Vanilla batteries have ConsumedResource none: they need no power to run.
- A block with a ConsumedResource counts as unpowered until power reaches it, and a battery that is not working stores and gives nothing. Such a battery cannot start a grid on its own; it works only while another source powers the grid.
- Players switch each built battery between Auto, Charge and Discharge in the terminal; a mod does not set it.

CellSlot dummies:
- Each slot needs a dummy of Type CellSlot (Assets/System/DummyTypes/CellSlot.def), in slot order: the first CellSlot dummy belongs to the first slot. The slot's cell is drawn there, and a cell falls out there when the block part the dummy's SupportingFracture names breaks off.
- Without a CellSlot dummy the block fails to load: the Editor's preview of it errors.

Templates: find_template("battery") lists two. Battery is how the vanilla batteries are built: power cells in slots. Batteries is an older one with a plain stored amount and no cells.
The Battery template gives the new block its own PowerableBlockDefinition (no BlockKind, so no block menu place; ConsumedResource Electricity at 0) and its own ResourceContainerInventoryDefinition with the template's rates, one cell slot and no CellSlot dummy.

Storage without cells, on any block (a generator with its own buffer, the Batteries template): a ResourceContainerComponent with a plain ResourceContainerDefinition, as the gas tanks use for gas: ResourceType Electricity, MaxCapacity in kW*s, MaxChargingRate below MaxDischargeRate, ProductionPriority. InfiniteSourceInCreative (on by default) makes it endless in creative mode only.

Vanilla batteries: Assets/Blocks/Batteries/Battery/<size in cm> (50, 150, 250). They differ in rates and number of slots (one on the 50 and 150, four on the 250); the smallest takes a smaller cell.

Looks only, not needed to store power: the charge indicator material, the damage particles (on a ParticleEffects dummy in vanilla) and sounds.
