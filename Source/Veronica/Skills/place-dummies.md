---
name: place-dummies
description: Reference: setting a block's dummies (nozzles, conveyor ports, ...), when to copy their spots and when the user places them.
---
A dummy is an entry in the Dummies of the block's BlockModelComponentDefinition: Name, Type (what it is for), Transform (position and rotation) and Scale. read(def, "Dummies") first and keep the dummies that are there. element_add(def, "Dummies") adds one; then write(def, "Dummies[i].Name", ..) and write(def, "Dummies[i].Type", <dummy type>).

Where a dummy sits depends on the model:
- A model another block already uses (vanilla or another mod's): that block's BlockModelComponentDefinition holds the spots. Copy the Transform and Scale of its dummies of that type, then show(the block) and ask the user to check, with wait_for_user.
- A new or imported model: the spot is the user's call. Its geometry is not visible to you, so never compute positions, not even from a description. show(the block), tell the user which dummy to move (its paths, e.g. Dummies[1].Transform and Dummies[1].Scale) and that the Render View shows dummies under Preview Settings, Dummies; then wait_for_user.

After the user's answer, read(def, "Dummies") again and cite the positions and scales read in the item's proof. What the user set stays as it is.
