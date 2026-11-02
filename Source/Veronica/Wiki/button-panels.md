---
name: button-panels
title: Buttons and button panels: the toolbar slots, the button dummies and their names
source: SE2 source BlockToolbarServerComponent, BlockToolbarServerDefinition, BlockToolbarValidator, UseToolbarSlotAction, UseToolbarSlotActionDefinition, UseActionDefinition.GetSlotIndex, BlockGroupToolbarTile, PowerableBlockDefinition; vanilla UseActionBindings.def, vanilla buttons and button panels (Assets/Blocks/Button, Assets/Blocks/ButtonPanels), the Buttons template
description: How a button or button panel block works: the toolbar component and its slot counts, the UseToolbarSlot dummies and how their names pick a slot, what players set in game, power, what the template brings, and the parts that are only looks.
---
Parts:
- BlockToolbarServerComponent, on the server only, with its BlockToolbarServerDefinition: ToolbarsCount groups of TilesCount tiles each, both 1 to 10. A button panel uses one group, one tile per button.
- The tiles themselves live on the block's prefab (Components[BlockToolbarServerComponent].Tiles); the game's validation pads or trims that list to the definition's counts, so it needs no edit when the counts change.
- PowerableBlockComponent as the block component. A button press does nothing while the block is not working (the press action does not work on a disabled block), so it needs power on the grid even at Amount 0.

Buttons:
- A button is a dummy of Type UseToolbarSlot in the block's BlockModelComponentDefinition. Looking at it and pressing use activates one tile of the block's toolbar.
- Which tile: the last word of the dummy's Name, a number counted from 0 ("Button 0", "Button 1", ...). The words before it are free. A name with no number at its end, or a number past the last tile, presses nothing.
- Buttons reach one toolbar group only, the ToolbarGroupIndex of the UseToolbarSlot action definition; tiles in other groups cannot be pressed by a button. Pressing needs no seat.
- A button whose tile has no action opens the block's control panel instead, where players assign actions. What each tile does is set by players in game, not by a mod; a tile can target a block group as well as a single block.
- The dummy's position and Scale make the spot players look at; its forward does not matter for pressing.

Vanilla: Assets/Blocks/Button (a single button) and Assets/Blocks/ButtonPanels (a panel). Each has its own toolbar definition, and they sit in two block menu families (single buttons, button panels).

The Buttons template: a block made from it has the toolbar component with no definition ("needs BlockToolbarServerDefinition"), the single button's BlockKind, power Electricity at Amount 0, and no dummies.

Looks only, not needed to press: the UseTerminal spot vanilla panels carry (the control panel also opens from an unassigned button or the grid's terminal), the material swapper, particles and their dummies, each dummy's HighlightSection and SupportingFracture.
