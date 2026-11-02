---
name: configurations
title: Configurations: game-wide settings and how a mod changes them
source: SE2 source GPSMarkersConfiguration, ContractMarkerTrackerConfiguration, ColonizationMapConfiguration, FastTravelConfiguration, LcdCoreConfiguration, LcdImageCatalogConfiguration, AmmoDefinitionsConfiguration, ProjectileWeaponBaseComponent, PhysicsSessionConfiguration, CharacterPhysicsEntityComponentDefinition, JetpackMovementComponentDefinition, JetpackFuelComponentDefinition; vanilla Assets/System/Configurations, Assets/UI/HUD/GPS/GPSMarkersConfiguration.def, Assets/Characters/System/CharacterPhysics.def
description: What a configuration definition is and why a mod overrides vanilla's instead of making a new one, where vanilla keeps them, and which one holds GPS and contract marker icons, LCD images, the ammo of a new ammo item, character and ship speed limits and jetpack fuel and thrust.
---
A configuration is a definition the game looks up by its type; there is one per type.
- A new configuration definition is never read. A mod changes a game-wide setting with an override of vanilla's (create_partial kind override), adding list entries or changing values there.
- Vanilla configurations: Assets/System/Configurations. find_content with the type name finds one.

Marker icons:
- GPSMarkersConfiguration (Assets/UI/HUD/GPS): in-world GPS marker icons (DefaultIcon, PlayerCreatedMarkerIcon, Icons, PlayerBackpackIcon).
- ContractMarkerTrackerConfiguration: mission and contract marker icons.
- ColonizationMapConfiguration, FastTravelConfiguration: icons shown only on the map.

LCD images:
- LcdCoreConfiguration: Images is the one image list players pick from on every LCD; Fonts the fonts.
- LcdImageCatalogConfiguration is obsolete.

Ammo:
- AmmoDefinitionsConfiguration: AmmoTypes maps each ammo ItemDefinition to its AmmoDefinition.
- A new ammo item needs an entry. Without one the game crashes when a weapon reloads it, and nothing reports it before.

Speed limits, in m/s:
- PhysicsSessionConfiguration: MaximumCharacterSpeedLinear caps every character; MaximumSpeedLinear caps every other body (ships, objects), never characters.
- A character's own limits: BaseGroundVelocity and MaxVelocity of its CharacterPhysicsEntityComponentDefinition (vanilla Assets/Characters/System/CharacterPhysics.def). A faster character needs both these and MaximumCharacterSpeedLinear raised.
- Jetpack: without boost capped by PrecisionModeSpeedLimit of the JetpackMovementComponentDefinition, with boost by MaximumCharacterSpeedLinear.

Jetpack:
- JetpackFuelComponentDefinition (Assets/Characters/System; NPCJetpackFuelComponentDefinition for NPCs): StationaryFuel, PrecisionFlightFuel and BoostFlightFuel, each a character stat and an amount. Vanilla burns Energy except in boost, which burns Hydrogen.
- JetpackMovementComponentDefinition: thrust is a constant MaxThrust, times BoostThrustMultiplier in boost. No property ramps thrust up over time.
