---
name: block-drill
description: Use when a block should dig terrain and collect ore like a ship drill, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Drills template; a new block comes from create-block first, which also gives it its name and models
- how it digs and how much ore it holds: the user's own, or as a vanilla drill (Assets/Blocks/Drills)
- conveyor ports: how many and on which faces, the user's choice for their model
- the drill head spots and the ports: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its drill, detector, damage, inventory and stockpile definitions, and its model definition's dummies
- Get from the user what this needs: how it digs, how much ore it holds, how many conveyor ports and where, and for a new model that they will place the drill head spots and the ports
- Make it dig: brush shapes and drilling speed, the reach that starts drilling, what it damages in front, and the effects definition
- Make it hold and pass on ore: ore storage, and pushing ore out to the conveyors
- Set up the dummies: PrimaryDrill, SecondaryDrill, DrillDamage and the UseConveyor ports, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components with their definitions. A block made from the Drills template has its own copy of each, but they are left empty: read the block's DrillDefinition, SphereShapeEntityDetectorDefinition, BoxShapeEntityDetectorDefinition, ToolDamageDefinition, InventoryDefinition and InventoryStockpileDefinition, and the same definitions of a vanilla drill to compare. read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, how it digs (speed, how much it digs at once) and how much ore it holds; offer a vanilla drill as a suggestion. Ask in the same message how many conveyor ports it has and on which faces: a vanilla drill's ports fit its own model. For a new model, say they will place the drill head spots and the ports in the Editor.
3. Digging. The template leaves these empty; without them the drill never digs, and the game's validation reports them:
   - DrillDefinition: PrimaryBrushShape and SecondaryBrushShape, the shapes it cuts out of the terrain (a sphere's Radius in metres); PrimaryDrillingTimeModifier and SecondaryDrillingTimeModifier multiply the time a material takes to dig: 1 is standard, less is faster.
   - SphereShapeEntityDetectorDefinition QueryRadius: metres around the PrimaryDrill spot within which terrain starts the drilling.
   - ToolDamageDefinition DamageAdapterPrefab and BoxShapeEntityDetectorDefinition QueryHalfExtents: what it damages in front of it, in a box of that half size in metres around the DrillDamage spot.
   Write the values into the block's own definitions: a vanilla drill's where the user wants it as vanilla (its brush shapes and damage prefab are referenced as they are), the user's own otherwise. Setting a component on a vanilla definition instead leaves the block's own copy empty, and that copy still fails the game's validation.
   The template's DrillToolEffectsComponent points at an abstract base definition, which the game's validation rejects: write(block, "Components[DrillToolEffectsComponent].Definition", <a vanilla drill's DrillToolEffectDefinition>).
4. Ore. Primary drilling puts ore into the block's inventory; when it is full the rest drops on the ground.
   - InventoryDefinition: MaxMass, in kg, is how much ore it holds; WhitelistedTypes Ore keeps other items out. PreventStockpilingFromAddingItems keeps other blocks from pushing their items into the drill; PreventStockpilingFromRemovingItems would stop the ship's other blocks from taking the ore out, so it stays false on a drill.
   - InventoryStockpileDefinition FillRatioPush: the stockpile pushes ore out to the conveyors while the inventory is fuller than this ratio (0 pushes everything out). None, as the template has it, never pushes.
   Write the values that do not fit what the user wants into the block's own definitions.
5. Dummies: PrimaryDrill and SecondaryDrill are where the two drilling actions cut (often the same spot at the drill head); DrillDamage centres the damage box. Each is required: without it the block fails when it is built. Conveyor ports are UseConveyor dummies, one per port. element_add(BlockModelComponentDefinition, "Dummies") per dummy, then write its Name and Type (Assets/System/DummyTypes/PrimaryDrill.def, SecondaryDrill.def, DrillDamage.def or UseConveyor.def).
   Positions: for a model another block uses, copy that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.
6. The drill digs only while the block works: built, on and powered.

Show:
show(the block). Ask the user to check the drill head spots, the damage box and the conveyor ports in the Render View (Preview Settings, Dummies).
