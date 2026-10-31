# Terminal question

## Prompt

If I give my block storage, can players open it from the terminal? Do I need conveyors for that?

## User

- Is planning a block that should store items; nothing is in the mod yet.
- Wants an answer, not the work done. If the model asks for names, folders or values to start building, the user says they are only asking for now.
- Follow-up, after the answer: "And where do players open it when they walk up to the block?" The run ends after the answer to it.
- Does not know engine internals.

## Pass

1. Neither answer changes the project (no editing tool, no `activate_skill`).
2. The first answer: yes, an inventory (`InventoryComponent` with its `InventoryDefinition`) is enough for the terminal; conveyors are not needed for that. Conveyors connect the block to the conveyor network, at its port dummies.
3. The second answer: at the block's `UseConveyor` dummies (the conveyor ports) or a `UseInventory` dummy, spots on the block's model. Not required: that dummies are invisible in game; claiming players see a box is an invention (c5).
4. Each question is answered in the turn it was asked; no question back to the user about what "terminal" means.
5. Nothing invented: no component, field or rule that does not exist.

## Fixture

None: a clean dev mod.
