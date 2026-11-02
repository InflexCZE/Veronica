---
name: pistons-and-rotors
title: Pistons and rotors: base and head blocks, telescoping parts, the owner provider
source: SE2 source PistonComponent, HingeComponent, ToppedMechanicalBlockComponent, PistonBlockColliderComponent, PistonBlockColliderDefinition, PistonSubPartInfo, ParentOwnerProviderComponent, IOwnerProvider; vanilla Piston150 and PistonHead150 (Assets/Blocks/Pistons/150), Rotor250 and RotorHead250 (Assets/Blocks/Rotor/250)
description: How a piston or rotor block is built: the base and head as two separate blocks, a piston's telescoping parts and where they are listed, and the owner provider component a piston or rotor base needs.
---
Parts:
- A piston or rotor is two blocks: the base and the head that sits on it, each with its own definitions. Vanilla: Assets/Blocks/Pistons/150 (Piston150, PistonHead150) and Assets/Blocks/Rotor/250 (Rotor250, RotorHead250); their files show every part each needs.
- The base's moving component is PistonComponent for a piston and HingeComponent for a rotor (vanilla Rotor250_Rotors_HingeDefinition).

Telescoping parts:
- A piston's telescoping parts are the SubParts of its PistonBlockColliderDefinition. Each has a Model (a ModelComponentDefinition; its collider is the part's collider) and a BaseTransform (the part's place when the piston is fully retracted).
- The parts extend one after another as the head moves out.
- Vanilla gives each part its own model component definition (Piston150S1_ModelComponentDefinition and on) in the piston's folder.

Owner provider:
- PistonComponent and HingeComponent need a component in the base's IOwnerProvider tag slot; in vanilla a ParentOwnerProviderComponent. Without one: Unsatisfied dependency 'IOwnerProvider' on PistonComponent. Adding the component is not enough if it does not fill that tag slot; compare with the vanilla piston.
