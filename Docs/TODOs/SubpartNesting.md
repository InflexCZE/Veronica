# A part inside a part

**Exists:** `block-subpart` places a part at a spot on the block through the block's ConditionalSubPartsComponent, and `subpart-animation` gives it one motion.

**Missing:** a part inside a part. The game nests them as child entities inside one subpart prefab's hierarchy (vanilla GatlingCannon200's barrel: a container entity carries the spin, a child entity carries the model, offset so the model turns about the container's axis). ConditionalSubPartsComponent cannot do it: it needs a block on its entity. No tool adds a child entity inside a prefab, so a part cannot get a second motion (spin and slide) or turn about a point other than its model's origin. Owner: test the shape together in the Editor before choosing the tool.
