---
name: block-button
description: Use when a block should have buttons players press to run actions, like a button or button panel, one create-block just made or one that already exists; it does not create the block: a new one starts with create-block.
---
Needs:
- the block: its prefab binding or any of its files, made from the Buttons template; a new block comes from create-block first, which also gives it its name and models
- how many buttons: the user's own
- the button spots: from the block that already uses the model, or the user positions them in the Editor

Plan:
- Read the block: its toolbar component and its definition, and its model definition's dummies
- Get from the user what this needs: how many buttons, where it goes in the block menu, and for a new model that they will place the button spots
- Give the toolbar one slot per button: a toolbar definition with that many tiles, set on the block's toolbar component
- Set up the button dummies: one per button, named with its slot number, placed by copying or by the user; read them back
- Show the block

Steps:
1. read(block) lists its components; one listed with "needs <type>" has no definition yet. A block made from the template has a BlockToolbarServerComponent with no BlockToolbarServerDefinition. read(BlockModelComponentDefinition, "Dummies").
2. Ask the user, with ask_user, how many buttons the block has and whether it sits with the single buttons or the button panels in the block menu. For a new model, say in the same message that they will place one spot per button in the Editor.
3. Toolbar: the BlockToolbarServerDefinition sets the slots: ToolbarsCount groups of TilesCount tiles each; a button panel has one group with one tile per button. Read the toolbar definitions of the vanilla buttons and button panels (Assets/Blocks/Button, Assets/Blocks/ButtonPanels). When one has the counts the user wants, write it to Components[BlockToolbarServerComponent].Definition as it is; otherwise create_partial(it, "Copy", folder), write ToolbarsCount and TilesCount on the copy, and set the copy on the component.
   The template's BlockKind is the single button's family. A panel of several buttons sits with the vanilla button panels when the user wants it there: write a vanilla button panel's BlockKind on the block definition.
4. Dummies: a button is a dummy of Type UseToolbarSlot (Assets/System/DummyTypes/UseToolbarSlot.def). The game reads which tile it presses from the last word of its Name, a number counted from 0: "Button 0", "Button 1", ... A name without a number at its end, or a number past the last tile, presses nothing. For each button, element_add(BlockModelComponentDefinition, "Dummies"), then write its Name and Type.
   Positions: for a model another block uses, copy the Transform and Scale of that block's UseToolbarSlot dummies; for a new model, the user places them: never compute positions. Then show(the block), ask the user to check or move them (name the paths, e.g. Dummies[0].Transform) with wait_for_user, read(def, "Dummies") again and mark the item with the positions read back.

Show:
show(the block). Ask the user to check the button spots in the Render View (Preview Settings, Dummies). What each button does is set by players in game.
