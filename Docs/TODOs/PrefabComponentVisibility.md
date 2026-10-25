# Prefab component visibility

**Exists:** editor tools show and write the fields the inspector's default visibility rules allow (`Fields.IsVisible` in `Source/Veronica.Editing/Fields.cs`).

**Missing:** inside a `PrefabDefinition`, the Property Inspector shows component object builders through a stricter rule (`ObjectBuilderVisibilityStrategy`, used by `PrefabComponentPropertyElement`): a member shows only with an unbroken `[ForceShowInInspector]` path from the component down. The tools ignore it, so `read` on a client or server prefab definition dumps internal component data, and `write` accepts it. The thruster flow does not touch prefab definitions. The rule is private and needs a live property element, so the tools would carry their own copy of it (`ShowInInspectorInternal` / `GetVisibilityModifierFromParents` in `VRage.UI.Shared`).
