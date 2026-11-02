---
name: localization
title: Localization: text keys, the Unrecognized value warning, languages and .loc-texts files
source: SE2 source LocKey, LocalizationKeyAttribute, LocalizationSuggester, SuggestersValidationBatch, DefaultTextSource, CultureIdDefinition, CultureInfoAssetDefinition, CultureInfoDefinitionPostProcessor, LocalizationCultureFileAssetType, LocalizationEngineComponent, DefinitionSetCollectionGenerator, ValidateProjectItemsFeature, SoundBlockDefinition; vanilla Assets/MainMenuData.meta and Assets/MainMenuData/Texts (en-US.def and its .loc-texts files)
description: How text players see (block, item and block kind names, descriptions, sound names) is stored as localization keys, why plain text gets an 'Unrecognized value' warning yet still shows as typed, how a mod adds its own language from a CultureIdDefinition, a CultureInfoAssetDefinition and .loc-texts files, why vanilla languages and main menu fonts cannot be overridden, and when a mod's language appears.
---
Text keys:
Text fields players see are localization keys (LocKey): UIData.Name and Description on a block definition, names on item and block kind definitions, DisplayName on a SoundBlockDefinition's AvailableEvents entries.
A key with no localized text is shown as the key string itself, so plain text written into the field shows in game as typed.
Validation reports such a field as 'Unrecognized value <text>': a warning only, it does not block the mod. A mod with many plain-text names gets many such warnings; they can be left.
The warning clears only when the key exists in a .loc-texts file listed by a CultureInfoAssetDefinition, a vanilla one (an existing game key) or the mod's own.
A .loc-texts file is JSON: each key maps to the text shown. A key appears once per file.

Languages:
A language is two definitions:
- CultureIdDefinition: the culture string CultureId (e.g. en-US), unique, and LocalizedName, the name shown in the language menu.
- CultureInfoAssetDefinition: CultureId references the CultureIdDefinition; Resources lists entries of ContentId and Asset (a .loc-texts file).
Vanilla languages: Assets/MainMenuData/Texts/<culture>.def with their files in Texts/<culture>/; read one as the pattern.

Assets/MainMenuData is the Core definition set; a mod's definitions are the World set. A partial (override or copy) whose base is in another set is rejected: 'Cannot use definition ... as base for partial definition ... as they are not in the same definition set'. So vanilla languages and main menu fonts (FontDefinition, FontFamilyDefinition) cannot be changed from a mod.
A mod's language is a new CultureInfoAssetDefinition with its own CultureId and .loc-texts files, not a partial of a vanilla one.
Localization is loaded at game start and a mod's cultures are known only once its definitions load, so a mod language appears only after a world with the mod is loaded, not in the main menu before.
