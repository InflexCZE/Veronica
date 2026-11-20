---
name: subparts
title: Subparts: parts of a block with their own model, how they attach, collision, paint
source: SE2 source ConditionalSubPartsComponent, ConditionalSubPartsDefinition, DummyTypeDefinition, PrefabDefinition, BlockModelComponentDefinition, ChildTransformComponent, HierarchyComponent, ModelComponentDefinition (PreciseCollider, SubpartColliderOffset), RigidAnimationComponent, DoorBlockColliderDefinition, PistonBlockColliderDefinition, BlockSubpartRenderComponent, BlockMaterialSwapperDefinition; vanilla gyroscope and door subpart prefabs (their Subparts folders)
description: How a block gets parts with their own model, such as parts that move: subpart prefabs and the ConditionalSubPartsDefinition that places them at dummies, the components the block and the subpart need, subpart collision, and paint on subparts. How a part moves is subpart animation.
---
A subpart is a prefab of its own with its own model, spawned at each dummy of one dummy type on the block's model. A moving part of a block is always a separate model made a subpart.

Linking:
- The block holds one ConditionalSubPartsComponent. Its ConditionalSubPartsDefinition.SubParts maps a DummyTypeDefinition (key) to the subpart's PrefabDefinition (value).
- The dummies go in the Dummies of the block's own BlockModelComponentDefinition; a dummy on the subpart's model spawns nothing.
- Every dummy of a key's type gets one copy of that prefab. Different subparts (a left and a right door leaf) need different dummy types, one SubParts entry each, still in the one ConditionalSubPartsComponent. A new DummyTypeDefinition can be created for a subpart's spot.
- The subpart sits at the dummy's position and rotation: the dummy's transform overwrites the subpart prefab's ChildTransform. Centre the subpart model on its own origin, the point it turns about.

What the block needs:
- FunctionalBlockComponent, ModelComponent, BlockRenderComponent and HierarchyComponent. Without FunctionalBlockComponent validation says "Malformed dependency 'GridBlockComponent' on ConditionalSubPartsComponent. Expected FunctionalBlockComponent". A powerable block already has it.
- Subparts are spawned while the block is functional. MeshPartsToHideWhenActive lists mesh parts of the block's model hidden while the subparts are spawned.

What the subpart prefab needs:
- HierarchyComponent (else "The specified prefab doesn't have required components: (HierarchyComponent)"), ChildTransformComponent, a ModelComponent pointing at the subpart's model, and its render components (ModelRenderComponent, ParentRenderAdapterComponent).
- A vanilla subpart prefab, in a vanilla block's Subparts folder (e.g. a gyroscope's or a door's), shows the full set to copy.

Moving: a subpart moves through a rigid animation component on its prefab; no scale, bone or keyframe animation exists for blocks.

Collision:
- The subpart prefab's ModelComponentDefinition collider (PreciseCollider, shifted by SubpartColliderOffset) is added to the block's collision at the dummy's spot. It does not follow the part's movement.
- Collision that changes with a part's movement exists only for doors (DoorBlockColliderDefinition, switching between open and closed) and pistons (PistonBlockColliderDefinition).

Paint:
- BlockSubpartRenderComponent passes the block's paint colour to the subpart. It shows only where the subpart model uses a colourable material, as the block model's ...Colorable mesh parts do.
- BlockMaterialSwapperDefinition swaps materials and models; it is not paint.
