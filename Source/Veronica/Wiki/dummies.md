---
name: dummies
title: Dummies: thruster nozzles, conveyor ports and other spots on a block
source: vanilla BlockModelComponentDefinitions (Dummies) under Assets/Blocks; vanilla DummyTypeDefinitions in Assets/System/DummyTypes; SE2 source InteractorEntityComponent, ModelComponent, DummyTypeDefinition, UseActionBindingsDefinition, UseActionDefinition, ThrusterEffectsComponentDefinition, BlockEffectsComponentDefinition, ConditionalSubPartsDefinition; vanilla UseActionBindings.def and GridConveyorSystem.def; Veronica's element_add and write tools
description: What a dummy is and its fields (Type, SupportingFracture, HighlightSection), that dummies are not imported with the model, its types (Thruster, ParticleEffects, UseConveyor, UseInventory, Use* spots and their actions, a pilot seat's Seat, Head and UsePilotable, a light's Light and Flare, subpart spots), whether players see them, why a use prompt or highlight does not show, when to copy dummy positions from another block and when the user places them in the Editor.
---
A dummy is an entry in the Dummies of the block's BlockModelComponentDefinition: Name, Type (what it is for), Transform (position and rotation) and Scale.
Type is a DummyTypeDefinition; the vanilla types are in Assets/System/DummyTypes. Components find dummies by Type, never by Name: names are free, except a UseToolbarSlot's trailing slot number (the button-panels page). Where a component takes one dummy of a type, the first of that type in Dummies is used.
Dummies are not imported from the exported model file: they exist only in this list. The model brings the meshes; every spot a component needs is added here.
- read(def, "Dummies") first and keep the dummies that are there.
- element_add(def, "Dummies") adds one; then write(def, "Dummies[i].Name", ..) and write(def, "Dummies[i].Type", <dummy type>).
- SupportingFracture: the fracture (breakable part of the model) the dummy belongs to; the spot goes with that part when it breaks off.
- HighlightSection.Id: the mesh section of the exported model that glows while a player points at the dummy. The section is made in the model export (a section group), not in the Editor. It need not match the dummy's Name; vanilla gives both the same name. Empty, or naming a section the model lacks: no glow, but the use prompt still works.

Types:
- Thruster: a nozzle. Flame comes out along the dummy's +Z. One per nozzle. The ThrusterEffectsComponentDefinition's ThrustDummyType names which type carries the flame (Thruster in vanilla).
- ParticleEffects: where block effects such as fire on a damaged block appear; each effect in the BlockEffectsComponentDefinition names its own Dummy type (vanilla: ParticleEffects, dummy named Particles). Looks only.
- UseConveyor: a conveyor port, a flat box facing out along its Z, with equal X and Y scale. One per port. It is also where a player opens the block's inventory in the world.
- UseInventory: only where a player opens the block's inventory in the world; not a conveyor port.
- Seat, Head and UsePilotable: a pilot seat's spots: where the pilot sits, the first-person camera, and where the player looks to sit down.
- Light and Flare: where a light block shines from and where its lens flare sits (the lights page).
- Use spots (Use* types): a box a player looks at and presses use on. UseActionBindingsDefinition's DummyTypesMap (vanilla UseActionBindings.def) maps each Use* type to its action, e.g. UseTerminal opens the terminal, UsePowerable toggles the block on and off (needs a PowerableBlockComponent), UseDoor works a door. A secondary key on the same spot is the action's InteractSecondary input, not another dummy. The dummy's position and Scale make the box.
- A subpart's spot: any dummy type a ConditionalSubPartsDefinition uses as a key; the subpart appears at each dummy of that type on the block's model.

Players never see dummies in game: they are invisible spots on the model. Only the Editor's Render View draws them. The Render View preview lists each named dummy with its own toggle that lights its highlight; the preview looks up the section by the dummy's Name, not by HighlightSection, so the two given the same name make the preview match the game.

Use prompt does not show, check:
- the block is built, functional and on: a use action is not made on a disabled block unless its UseActionDefinition has WorkWhenDisabled;
- RequireFacingPlayer on the use action hides the prompt when the player looks from behind the dummy's forward;
- the box reaches out past the model's surface: only the part between the player and the surface counts, a buried box is never hit;
- UsePowerable needs a PowerableBlockComponent on the block.
Prompt shows but nothing glows: HighlightSection is empty or names no section of the model.

Where a dummy sits depends on the model:
- A model another block already uses (vanilla or another mod's): that block's BlockModelComponentDefinition holds the spots. Copy the Transform and Scale of its dummies of that type, then show the block and ask the user to check.
- A new or imported model: the spot is the user's call. The model's geometry is not visible to Veronica, so positions are never computed, not even from a description. Show the block, tell the user which dummy to move (its paths, e.g. Dummies[1].Transform and Dummies[1].Scale) and that the Render View shows dummies under Preview Settings, Dummies; then wait for the user.

After the user moved them, read(def, "Dummies") again: what the user set stays as it is.
