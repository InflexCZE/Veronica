---
name: block-cockpit
description: Use when a block made from the Cockpit or Controllable Seat template should seat a pilot who flies the ship; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Cockpit or Controllable Seat template
- the sitting pose: a cockpit pose or a control seat pose
- seat spots: from the block that already uses the model, or the user positions them in the Editor
- a folder for the camera file, e.g. the block's folder

Plan:
- Read the block: its components, which of them need a definition, its seat definitions and its model definition's dummies
- Get from the user what this needs: the sitting pose, and for a new model that they will place the seat spots
- Set up the seat: the sitting pose, the inventory the seated pilot opens when the block stores items, and a definition on every component that needs one
- Set up the seat dummies: Seat, Head and Enter, placed by copying or by the user; read them back
- Register the cameras: a camera system override that gives the block's client composition the cockpit cameras
- Show the block

Steps:
1. read(block) lists its components; one listed with "needs <type>" has no definition yet. Read its SeatComponentDefinition, its ModelChangingSeatComponentDefinition and the Dummies of its BlockModelComponentDefinition.
2. Ask the user, with ask_user, for the sitting pose: Cockpit (vanilla Cockpit) or ControlSeat (vanilla Control Seat); suggest the one of the template. For a new model, say in the same message that they will place three spots in the Editor.
3. Seat:
   write(SeatComponentDefinition, "SeatType.Id", "Cockpit" or "ControlSeat"). The character's sitting animations know only these two; the template's "Seat" matches none.
   When the block has an InventoryComponent: write(ModelChangingSeatComponentDefinition, "Inventory", <that component's tag, e.g. "Inventory">). It names the inventory the seated pilot opens from the terminal.
   For each component that needs a definition: write(block, "Components[<component>].Definition", <the definition of the same component on a vanilla pilot block>). Vanilla pilot blocks are in Assets/Blocks/Cockpit and Assets/Blocks/ControlSeat.
4. Dummies: the seat uses three dummies of the BlockModelComponentDefinition, found by Type; the template made them at the model's origin.
   - Seat: where the pilot sits, facing the dummy's forward.
   - Head: the first-person camera at the pilot's eyes; only its position counts.
   - Enter, Type UsePilotable: where the player looks to sit down.
   Positions: for a model another block uses, copy the Transform and Scale of that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[1].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.
5. Cameras: a seated pilot gets the cockpit cameras and HUD only when the camera system lists the block's client composition (the EntityCompositeDefinition whose name ends in ClientComposition); otherwise the camera stays on the character.
   create_partial(the CameraSystemDefinition found with find_content("CameraSystem"), "Override", folder).
   read(camera system, "CompositionToCameraPrefabs[Cockpit150_ClientComposition]") and read(camera system, "CompositionToNamedCameraPrefabs[Cockpit150_ClientComposition]") give the vanilla cockpit's cameras.
   element_add(override, "CompositionToCameraPrefabs", <client composition>), element_add(override, "CompositionToCameraPrefabs[<client composition>]") once per camera, then write each camera in the same order. element_add(override, "CompositionToNamedCameraPrefabs", <client composition>), then write the same value as the vanilla cockpit's.

Show:
show(the block). Ask the user to check the Seat, Head and Enter dummies in the Render View (Preview Settings, Dummies).
