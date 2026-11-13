# Conveyor ports skill

**Exists:** block-inventory has a conveyor port step, which block-item-to-resource hands over to; block-tank, block-production and other skills each carry their own: element_add UseConveyor dummies, copy the positions from a block using the same model or have the user place them, then read them back. The inventory-and-conveyors and dummies wiki pages describe the ports.

**Missing:** one skill for conveyor ports, shared by every block with an inventory or a gas: how many ports, on which faces, placing them, and what goes through them. The block skills would hand over to it instead of repeating the step.

**Where to start:** the port steps of block-item-to-resource and block-tank, and the inventory-and-conveyors page.

**Owner decision:** settled that it is a separate skill shared with the inventory blocks; its shape is open.
