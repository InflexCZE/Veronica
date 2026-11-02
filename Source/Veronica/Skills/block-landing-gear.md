---
name: block-landing-gear
description: Use when a block should lock the ship to a surface like a landing gear or magnetic plate, one create-block just made or one that already exists; it does not create the block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Landing Gear template
- the lock pad: which face of the model touches the surface, from the block that already uses the model or from the user
- the lock box and the release box: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its components, which of them need a definition, and its model definition's dummies
- Get from the user what this needs: which face is the lock pad, and for a new model that they will place the lock box and the release box
- Set up the two detector dummies: the lock box (LockArea) and the release box (CooldownRange), placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; one listed with "needs <type>" has no definition yet. Two DummyShapedEntityDetectorComponents each name the dummy type they measure (QueryDummy on their definitions): the lock detector LockArea, the release detector CooldownRange. read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, which face of the model is the lock pad. For a new model, say in the same message that they will place two boxes in the Editor: the lock box just outside the pad, and a larger release box around it.
3. Dummies: each detector finds its dummy by Type and uses it as a box: the dummy's position and rotation place the box, its Scale is the box's full size. A block made from the template has neither: without them the detectors fail when the block is built. For each, element_add(BlockModelComponentDefinition, "Dummies"), then write its Name and Type (Assets/System/DummyTypes/LockArea.def, Assets/System/DummyTypes/CooldownRange.def).
   - Lock box (LockArea): a flat box just outside the pad. A surface inside it can be locked to when it is flat enough against the box's forward axis (the dummy's Z), so the box's thin side faces away from the pad.
   - Release box (CooldownRange): larger than the lock box and around it. After an unlock, auto-lock waits until nothing is inside it, so the gear does not lock again to the surface it just left.
   Positions: for a model another block uses, copy the Transform and Scale of that block's dummies of the same Type; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the lock box and release box in the Render View (Preview Settings, Dummies); locking is tested in game.
