---
name: block-tank
description: Use when a block should store hydrogen or oxygen like a gas tank, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Tanks template
- the gas: hydrogen or oxygen
- how much it holds and how fast it fills and empties: the user's own, or as a vanilla tank (Assets/Blocks/Tanks)
- conveyor ports: how many and on which faces, the user's choice for their model; their spots from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its ResourceContainerDefinition, its explosion definition, its BlockKind and its model definition's dummies
- Get from the user what this needs: the gas, the capacity and fill rates, how many conveyor ports and where, and for a new model that they will place them
- Set the tank: the gas, capacity and rates on its ResourceContainerDefinition
- Set what happens when it breaks full: for oxygen, the explosion of a vanilla oxygen tank
- Set up the conveyor ports: UseConveyor dummies, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; the ResourceContainerComponent and the ResourceContainerBlockDestructionExplosionComponent name their definitions, which a block made from the template owns. Read both, the block definition's BlockKind and read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, which gas the tank holds, how much it holds and how fast it fills and empties; offer a vanilla tank of that gas as a suggestion. The template's capacity and rates are next to nothing and were not chosen for this block. Ask in the same message how many conveyor ports it has and on which faces: a vanilla tank's ports fit its own model, not the user's. For a new model, say they will place the ports in the Editor.
3. Tank, on the ResourceContainerDefinition:
   write(def, "ResourceType", <Assets/System/ResourceTypes/Hydrogen.def or Oxygen.def>).
   MaxCapacity is in litres, the rates in litres per second. The filling rate must stay below the emptying rate, so write MaxDischargeRate first, then MaxChargingRate.
4. When the template left BlockKind none, the block has no block menu place: ask where it goes, for a tank usually the family of the vanilla tanks of its gas (write that block's BlockKind).
5. Breaking: a tank destroyed with gas inside explodes with the Explosion of its explosion definition, scaled by how full it is. A block made from the template has a vanilla hydrogen tank's Explosion. For an oxygen tank: write(explosion definition, "Explosion", <the ExplosionDefinition a vanilla oxygen tank uses>).
6. Conveyor ports: gas reaches the tank only through the conveyor network, and the network connects only at UseConveyor dummies; a block made from the template has none. element_add(BlockModelComponentDefinition, "Dummies") once per port, then write its Name and Type (Assets/System/DummyTypes/UseConveyor.def).
   Positions: for a model another block uses, copy the Transform and Scale of that block's UseConveyor dummies; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the conveyor ports in the Render View (Preview Settings, Dummies).
