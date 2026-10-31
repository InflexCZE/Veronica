---
name: cockpits
title: Cockpits and pilot seats: flying the ship, seat dummies, cameras
source: SE2 source SeatComponent, CockpitControllableComponent, CockpitInputHandlerComponent, CockpitComponent, ModelChangingSeatComponent, UsePilotableAction, CameraSystemComponent, PlayerControllerComponent, CharacterAnimationBlackboardServerComponent; vanilla Cockpit150 and ControlSeat250, the seat templates, CameraSystem.def, PlayerController.def, UseActionBindings.def, character AnimationControllers
description: How a cockpit or pilot seat works: the seat templates and how they differ, which components fly the ship, the Seat, Head and UsePilotable dummies, the sitting pose, the camera system entry, the pilot's inventory, power, and why a passenger seat is not possible.
---
Templates:
- Cockpit: a pilot seat that also stores items (InventoryComponent), has conveyor ports and hurts the pilot when the block is damaged. Vanilla Cockpit, Assets/Blocks/Cockpit/Cockpit/150.
- Controllable Seat: the pilot seat alone. Vanilla Control Seat, Assets/Blocks/ControlSeat/ControlSeat/250.
- Non-Controllable Seat: has no SeatComponent, so no one can sit in it. Sitting down always hands the ship's controls to the one who sits: a passenger seat cannot be made from these components.

What flies the ship:
- SeatComponent (server) and ModelChangingSeatComponent (client) seat the pilot; CockpitControllableComponent (client) hands the player's controls to the block; CockpitComponent (both sides) is what the game treats as a cockpit. All of them are needed.
- The block's own orientation is the ship's forward: the ship flies toward the block's Forward, whichever way the model or the pilot faces.
- Their tags stay as the template has them (SeatComponent, COCKPIT_TAG, CockpitControllableComponent, CockpitComponent, ToolsToolbar): the game finds these components by tag.
- CockpitControllableComponentDefinition's InputHandlerSpecificKey stays empty: the controls are looked up by it, and another key gets no controls.
- BlockToolbarServerComponent holds the block's toolbar; BlockToolsComponent the tools on it (weapons, drills).

Dummies, in the BlockModelComponentDefinition, found by Type:
- Seat: where the pilot sits; the pilot takes its position and rotation. Without it, sitting down fails.
- Head: the first-person camera; only its position counts. Without it, the cockpit camera fails.
- UsePilotable (vanilla names it Enter or after a canopy): an invisible box, its Scale the box's size, that sits the player down when they look at the block through it. Only the part of the box between the player and the model's surface counts: a box buried inside the model is never reached, so it has to reach out to the surface players look at. Vanilla makes it about as big as the block. Without it, the block cannot be entered, and nothing reports it.
- Each dummy's HighlightSection names the part of the model that lights up while the player points at the dummy; vanilla gives it the dummy's name.
- When the player's look passes through several dummy boxes, the one later in Dummies is tried first.
- No Pilot dummy belongs on the block: the seat definitions' PilotDummy is a spot on the character's model.
- There is no exit dummy: the pilot gets out where they stood when they sat down.

Sitting pose: SeatComponentDefinition's SeatType.Id picks the character's sitting animations. They know "Cockpit" and "ControlSeat"; the templates' "Seat" matches neither. The SeatType on the client ModelChangingSeatComponentDefinition is not used.

Cameras: the camera system definition (CameraSystem, a vanilla CameraSystemDefinition) lists, per client composition, the cameras of an entity someone controls. Vanilla pilot blocks have the cockpit third-person and first-person cameras in CompositionToCameraPrefabs and GridNamedCameraModesDefinition in CompositionToNamedCameraPrefabs. A block missing there still flies, but the camera stays on the character: no cockpit view, no cockpit HUD. A mod adds its block through an Override partial of CameraSystem. The cockpit HUD itself is one configuration for all cockpits.

Inventory: ModelChangingSeatComponentDefinition's Inventory names, by tag, the InventoryComponent the seated pilot opens from the terminal ("Inventory" on vanilla Cockpit). A block without storage leaves it empty.

Seat settings (SeatComponentDefinition):
- RequireSupply: sitting down needs power. Off on vanilla seats.
- RequireFunctional: sitting down needs the block built and undamaged enough to work, and the pilot is put out when it stops working. On by default.
- While seated, the character's backpack is hidden (ModelChangingSeatComponentDefinition's PilotPresetToHide), so it does not cut through the seat.
