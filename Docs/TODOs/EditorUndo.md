# Undo for agent edits

**Exists:** editor tools change definitions through `MemberReference` and save. The changes are not Ctrl-Z steps; the user reverts with version control. See `Docs/Tech/EditorTools.md`.

**Missing:** each write as one Editor history step. The Editor records inspector edits through `IHistoryService` with `SerializedValueChangedHistoryCommand`, both `internal` in the shipped Editor, so it is reachable only through reflection. Created files (folders, prefabs) have no history step at all. Owner decision: whether undo is worth the reflection, and how created files fit in.
