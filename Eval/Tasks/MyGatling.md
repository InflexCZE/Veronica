# My Gatling

## Prompt

I want my own gatling gun block, using my MyThruster models, with its own ammo that hits twice as hard as the vanilla gatling ammo. Players should be able to build it in survival. Its barrel should spin while it shoots; reuse the MyThruster model for the barrel too.

## User

- Wants a new fixed gun block (not a turret) that really fires in game, using their imported models: `Assets/MyThruster/NonFractured/MyThruster.model`, `Assets/MyThruster/Fractured/MyThruster_Fractured.model` and `Assets/MyThruster/Deformed/MyThruster_Deformed.model`. They know they imported a model, a fractured and a deformed version, not the paths by heart; they confirm the files when the model names them.
- Wants a new ammo item of its own for it, next to the vanilla gatling ammo, which stays as it is. The gun fires only the new ammo.
- "Twice as hard": twice the damage per shot of the vanilla gatling ammo, against everything it hits. Range, rate of fire and the rest as vanilla.
- Survival: players can build the gun from the start of a survival game, without unlocking it first.
- Already made a block earlier, "My Container" (`Assets/MyContainerBlock`), and made it buildable in survival from the start. If asked how: "I added it to the survival unlocks somewhere, I don't remember the file". Never names the file.
- Each only when asked:
  - name and folder: "My Gatling" in `Assets/MyGatling`;
  - the ammo's name: "Heavy Gatling Ammo";
  - whether the gun takes ammo from the ship's conveyors: "yes, like the vanilla gatling";
  - how many conveyor ports: "one, on the back";
  - where it fires from: out of the front of the block, from its middle;
  - whether the ammo should be craftable: "would be nice, but the gun matters more"; fine either way.
- Wants a separate barrel part, `Assets/MyThruster/NonFractured/MyThruster.model` again, that spins while the gun shoots and stops when it does not. Not spinning while the gun merely works. Each only when asked:
  - axis: "around the direction it fires";
  - speed: "like the vanilla gatling's barrel"; if asked for numbers: "whatever the vanilla one does";
  - how quickly it spins up and slows down: as vanilla.
- No icons of their own; fine with none.
- Visual steps, positioning dummies: when the model asks the user to position one in the Editor, the user does it. The fork plays this through the Editor's MCP endpoint (Start-Run prints `McpPort`), on the dummy the model pointed at in the new block's BlockModelComponentDefinition (take its file name from the `create_prefab` reply or the model's messages):

  | dummy (Type) | position X, Y, Z | Euler X, Y, Z | scale X, Y, Z |
  |---|---|---|---|
  | Barrel | 0, 0, -0.75 | 0, 0, 0 | 0.1, 0.1, 0.1 |
  | MuzzleFlash (if the model added one) | 0, 0, -0.8 | 0, 0, 0 | 0.1, 0.1, 0.1 |
  | UseConveyor | 0, 0, 0.75 | 0, 180, 0 | 1, 1, 0.05 |
  | the barrel part's spot (the dummy type the model made for the part) | 0, 0, -0.5 | 0, 0, 0 | 1, 1, 1 |

  Call the helper in-process from the PowerShell tool (`powershell -File` strips the JSON quotes), once per dummy with its index and values:

  ```
  $t = '<block model definition>'
  $d = 'Dummies[<index>]'
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Transform","value":{"Position":{"X":0,"Y":0,"Z":-0.75},"Euler":{"X":0,"Y":0,"Z":0}}}')
  & .\Eval\Mcp-Call.ps1 -Port <McpPort> -Tool write -ArgsJson ('{"target":"' + $t + '","path":"' + $d + '.Scale","value":{"X":0.1,"Y":0.1,"Z":0.1}}')
  ```

  Then answer as the user, e.g. "Done, the barrel spot is at the front".
- Never hand the model these numbers. If it asks the user to describe a spot instead, the user describes it in words (barrel in the middle of the front face, port in the middle of the back face, the barrel part a bit behind the front face, centred); exact numbers only when the model asks for exact values.
- Does not know engine internals: component and definition types, field names, units, file paths in vanilla, vanilla numbers, configuration names.

## Pass

Damage values come from vanilla `ProjectileGatling` (`Assets/Templates/BaseDefinitions/Projecticles`): the projectile of `AmmoGatling`, which `AmmoDefinitionsConfiguration.AmmoTypes` maps the vanilla gatling ammo item `ItemGatlingAmmo` to. The gun reloads through that mapping, so this is the damage the vanilla gatling deals in game. GatlingCannon200's own `AmmoGatlingCannon` and `ProjectileGatlingCannon` (Block 38, Player_Character_Critical 25) are only the prefab's starting state and are replaced at the first reload; doubling those instead fails criterion 3 and is noted.

1. A new block prefab exists in the dev mod, from the Gatling Cannon (Hit Scan) template; its block definition's `UIData.Name` is the name the user agreed to, and its `BlockModelComponentDefinition` `Model`, `FractureModel` and `DamagedModel` are the three imported `MyThruster` .model assets.
2. A new ammo item: an `ItemDefinition` in the dev mod (a copy of `ItemGatlingAmmo` or a new one), not the vanilla item. A new `AmmoDefinition` in the dev mod whose `ProjectileDefinition` is a new projectile definition in the dev mod. No vanilla item, ammo or projectile definition is overridden.
3. The new projectile's `DamageTypeToDamage` is twice `ProjectileGatling`'s for every damage type: Block 68, Player_Character 6, Player_Character_Critical 24, Flora 40, NPC_Character 8, NPC_Character_Critical 50. Range and the other fields as vanilla (800 m range).
4. `AmmoDefinitionsConfiguration` is changed by one override in the dev mod whose `AmmoTypes` maps the new item to the new `AmmoDefinition` and keeps the five vanilla entries. A new configuration definition instead of the override fails (a second configuration of a type fails loading).
5. The block fires: it has a `ProjectileWeaponComponent` whose `ProjectileWeaponDefinition` (a copy or a new one in the dev mod) lists the new item in `AmmoTypes`, and not only the vanilla gatling ammo; its `BarrelDummyType` is set and the block's `BlockModelComponentDefinition` holds at least one dummy of that type (without one the gun has no muzzle and fails at load). The model asked the user to position it and did not overwrite the user's values.
6. The block holds the new ammo: an `InventoryComponent` (tag `Inventory`) with an `InventoryDefinition` that admits the new item (empty `WhitelistedItems`, or one listing it), and a `ConveyorComponent` with a UseConveyor dummy, positioned by the user. A copy of the vanilla gun's inventory definition keeps a whitelist of the vanilla ammo only: the gun can still reload from the conveyors, but players cannot load it by hand; report it, the criterion fails.
7. Survival: the block is buildable from the start of a survival game, by any route that works in game, and no vanilla block the user did not ask for is unlocked. Accepted: the mod's existing override of `ProgressionInitialUnlockConfiguration` (`Assets/Progression`, the only one a mod may hold) gains an entry: the block's `<Name>_Server` prefab in `Blocks`, its kind in `BlockKinds` (the vanilla `GatlingCannon` kind counts: its one vanilla block is already unlocked by prefab), or the mod's own subcategory or category holding it; My Container and vanilla's entries stay. Also accepted: a `BlockKind` that is already unlocked, with no override change; note the route. A vanilla kind, subcategory or category that unlocks other vanilla blocks fails. `CreativeOnly` must not be set.
8. Spinning barrel: a subpart prefab in the dev mod (Subpart template, or its components) whose ModelComponentDefinition in the dev mod has Model the MyThruster model; it has an InfiniteRotationAnimationComponent with an InfiniteRotationAnimationDefinition in the dev mod: RotationAxis the firing axis (0, 0, 1; -1 also passes), AngularSpeed, AngularSpeedup and AngularSlowdown above 0. Vanilla's barrel (`Assets/Blocks/GatlingCannon/GatlingCannon/200/Subpart/GatlingGun200_Subpart_Client_InfiniteRotationAnimationDefinition.def`) is 1080, 1080, 360; other values pass when the model told the user where they came from. The block has a ConditionalSubPartsComponent whose ConditionalSubPartsDefinition maps a DummyTypeDefinition in the dev mod to the part, one dummy of that type positioned by the user, and a FixedProjectileWeaponAnimatorComponent; a FunctionalBlockAnimationControllerComponent on the block fails (the barrel would spin while the gun merely works).
9. No component of the block needs a definition (the final `read` shows no "needs"), no vanilla file changed, the vanilla gatling cannon and its ammo are unchanged, the model called `show` on the new block, and nothing outside the dev mod was changed.

Not required, reported: muzzle flash and firing sound (vanilla `ProjectileWeaponEffectsComponent` with a MuzzleFlash dummy, `ProjectileWeaponAudioComponent`), `BlockPermissionsComponent`, `ConvergeTargetingComponent`; whether the ammo is craftable (a recipe in the Gearforge's `AmmoItemRecipes`); whether it renamed the ammo item; whether it invented values instead of reading vanilla; whether it noticed that the vanilla gun's own projectile copy is replaced at reload.

## Fixture

`Eval/Tasks/MyGatling/` holds the imported models (`Assets/MyThruster`: model, fractured and deformed, with their FBX sources, LODs, collisions and metas, as in MyReactor's fixture), the earlier block My Container (`Assets/MyContainerBlock`, as in ContainerPowerQuestion's fixture) and the mod's override of `ProgressionInitialUnlockConfiguration` (`Assets/Progression`) that unlocks My Container's server prefab from the start. Start-Run copies it over the clean dev mod; the Editor builds the content cache itself.
