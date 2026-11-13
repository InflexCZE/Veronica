# Stockpile component added on both sides

**Exists:** inventory-pull adds an InventoryStockpileComponent with component_add to a block that lacks one; it binds to the block's InventoryComponent by tagging it InventoryStockpile.

**Missing:** component_add puts the stockpile on the client and the server, while the inventory is on the server only, so the client composition fails validation with an unsatisfied InventoryStockpile dependency. Vanilla and the Reactors template have the stockpile on the server only. The model cannot fix it from the skill.

**Where to start:** how component_add picks sides for a component whose dependency lives on one side only.
