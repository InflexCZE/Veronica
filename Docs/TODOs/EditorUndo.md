# Undo for agent edits

**Exists:** editor tools change definitions through `MemberReference` and save. The changes are not Ctrl-Z steps; the user reverts with version control. See `Docs/Tech/EditorTools.md`.

**Missing:**
- For the user: each write as one Editor history step. The Editor records inspector edits through `IHistoryService` with `SerializedValueChangedHistoryCommand`, both `internal` in the shipped Editor, so it is reachable only through reflection. Created files (folders, prefabs) have no history step at all.
- For the model: no tool takes back its own mistake. A partial it created by mistake stays in the mod; `element_remove` and `write` can only empty it, and an empty override still overrides. Seen with Bonsai on IonThrustersOverride: it overrode the shared particle effect definition instead of the thruster's, could not remove the file, and had to leave an unchanged override and tell the user. A delete of files the model created in the current conversation, or undo/redo of its own edits, would cover it.

Owner decision: whether undo is worth the reflection, how created files fit in, and what the model may delete or undo (only files it created, only in the current conversation, or any edit).
