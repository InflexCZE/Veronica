# Subparts that move on a toggle

**Exists:** `block-subpart` attaches a part with its own model to a block, and `subpart-spin` makes it spin continuously (while the block works, or faster with a thruster's thrust).

**Missing:** parts that move to a target and back: RotationAnimation and TranslationAnimation components on the subpart (the subparts wiki page). What toggles them on a block that is not a door (FunctionalBlockAnimationControllerComponent with the Working state, ToolStatusBasedAnimationController, a weapon's firing) is not researched, and no skill or eval task covers them. Doors move their leaves by their own components, not these.
