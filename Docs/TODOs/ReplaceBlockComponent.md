# Replace a block's component

**Exists:** `component_add` refuses a type that would take over a role another component fills (its default tags), such as a PowerableBlockComponent on a block whose block component is a FunctionalBlockComponent.

**Missing:** a way to change a block's component for a related type, keeping its settings. Making a non-powered block draw power (a thruster added to a cargo container) needs the FunctionalBlockComponent replaced by a PowerableBlockComponent, and the block's settings (UIData, BlockKind, mount point settings, density, fragility, recipe, PCU, ...) moved from its FunctionalBlockDefinition into a new PowerableBlockDefinition with the consumed resource. In eval runs the model did this by hand: about 13 field writes, and the extra component crashed the Editor. Owner decision: the tool's shape (a replace operation, or a function-preparing tool that brings a template's parts).
