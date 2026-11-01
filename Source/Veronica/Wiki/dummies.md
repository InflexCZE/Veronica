---
name: dummies
title: Dummies: thruster nozzles, conveyor ports and other spots on a block
source: vanilla BlockModelComponentDefinitions (Dummies) under Assets/Blocks; SE2 source InteractorEntityComponent, ModelComponent; vanilla UseActionBindings.def and GridConveyorSystem.def; Veronica's element_add and write tools
description: What a dummy is, its types (Thruster, UseConveyor, UseInventory, a pilot seat's Seat, Head and UsePilotable, a light's Light and Flare), whether players see them, when to copy dummy positions from another block and when the user places them in the Editor.
---
A dummy is an entry in the Dummies of the block's BlockModelComponentDefinition: Name, Type (what it is for), Transform (position and rotation) and Scale.
- read(def, "Dummies") first and keep the dummies that are there.
- element_add(def, "Dummies") adds one; then write(def, "Dummies[i].Name", ..) and write(def, "Dummies[i].Type", <dummy type>).

Types:
- Thruster: a nozzle. Flame comes out along the dummy's +Z. One per nozzle.
- UseConveyor: a conveyor port, a flat box facing out along its Z, with equal X and Y scale. One per port. It is also where a player opens the block's inventory in the world.
- UseInventory: only where a player opens the block's inventory in the world; not a conveyor port.
- Seat, Head and UsePilotable: a pilot seat's spots: where the pilot sits, the first-person camera, and where the player looks to sit down.
- Light and Flare: where a light block shines from and where its lens flare sits (the lights page).

Players never see dummies in game: they are invisible spots on the model. Pointing at one that can be used highlights a part of the block's model instead (the dummy's HighlightSection). Only the Editor's Render View draws them.

Where a dummy sits depends on the model:
- A model another block already uses (vanilla or another mod's): that block's BlockModelComponentDefinition holds the spots. Copy the Transform and Scale of its dummies of that type, then show the block and ask the user to check.
- A new or imported model: the spot is the user's call. The model's geometry is not visible to Veronica, so positions are never computed, not even from a description. Show the block, tell the user which dummy to move (its paths, e.g. Dummies[1].Transform and Dummies[1].Scale) and that the Render View shows dummies under Preview Settings, Dummies; then wait for the user.

After the user moved them, read(def, "Dummies") again: what the user set stays as it is.
