# Inventory blocks without BlockPermissionsComponent

**Exists:** `block-weapon` adds `BlockPermissionsComponent` with the gun's inventory.

**Missing:** validation requires it on every block with an inventory ("Block's with Inventory must have BlockPermissionsComponent"), but `block-inventory` and the inventory-and-conveyors wiki page do not say so; every MyThrusterCargo run carries the error (`.tmp/eval/runs/20261104-102202-MyThrusterCargo` and all earlier ones). Add the line to block-inventory (and the wiki), then re-run MyThrusterCargo and MyContainerThrust.
