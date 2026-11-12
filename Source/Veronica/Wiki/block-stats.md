---
name: block-stats
title: Block stats: mass, health, build cost, build time and PCU
source: SE2 source CubeBlockDefinition (mass, MaxHealth, ComputeRecipeAmounts, BuildTime, PCU, WeakThreshold), CubeBlockDensityDefinition, CubeBlockFragilityDefinition, CubeBlockMassConfiguration, CubeBlockRecipeDefinition, ItemDefinition, GeneratedBlockDataLoader, CubeBlockMassComputationTests; vanilla block recipes, Assets/Templates/Blocks/BaseDefinitions
description: How a block's mass and health are computed (density, fragility, size, the weak-while-built share), how its build cost works (the CubeBlockRecipeDefinition, why the component counts in game differ from the recipe's amounts and how to change them), how long building takes, and its PCU.
---
The stats below sit on the block definition: PowerableBlockDefinition or the block's other *BlockDefinition.

Mass:
- Computed, not set and not the sum of the components: Density.MassCurveModifier * sqrt(cells) * log10(cells) + MinBlockMass, with cells the block's occupied 0.25 m cells.
- Density references a CubeBlockDensityDefinition. With no Density the mass is MinBlockMass (CubeBlockMassConfiguration).
- So mass changes with the Density or the block's size, nothing else.

Health:
- No field of its own: max HP = Fragility.MaxHPMassMultiplier * mass. Fragility references a CubeBlockFragilityDefinition; with none the multiplier is 1.
- It changes with the Density, the Fragility or the block's size; the build components do not affect it.
- While the block is built below WeakThreshold (a share of build progress), its HP is multiplied by WeaknessMultiplier.

Build cost:
- Recipe references a CubeBlockRecipeDefinition. Its CriticalItems and OptionalItems list ItemAmount entries: an item definition and an Amount.
- The Amounts are ratios, not counts. Count in game = Amount * scale, with scale = block mass / sum of (Amount * the item's MassPerUnit), rounded, at least 1. So the components' total mass matches the block's mass.
- A bigger block made from a smaller one's recipe costs more of the same items; so does a denser one.
- An item with a tiny MassPerUnit (ItemDefinition) inflates the count in every block that uses it. A new component item needs a mass in line with the vanilla components: read one.
- To change counts, change the Amount ratios, the items' MassPerUnit, the Density or the block's size.
- Every entry needs an existing Item: one cleared or pointing at a deleted item definition breaks the mod's load instead of failing validation. Remove the entry instead.

Build time: the recipe's BuildTimePerItem * the total item count, then scaled by the tool. Fewer or heavier items build faster.

PCU:
- The block definition's PCU, a whole number from 1 (default) to 999999. A grid's PCU is the sum of its blocks'.
- Vanilla blocks take theirs from their kind's template definition (Assets/Templates/Blocks/BaseDefinitions).
- There is no formula: compare with a similar vanilla block.
