# Game validation hints beyond the written field

**Exists:** `write` runs the written field's range, string length and unit vector validations and hints in its reply when the value is out of range (`Source/Veronica.Editing/ValueChecks.cs`).

**Missing**, deferred by the owner until evals show the need:
- Fields whose bounds name the written field: lowering a battery's MaxDischargeRate below its MaxChargingRate gives no hint, the charging rate's own range is broken.
- `element_add` and `element_remove`: no checks, so `LengthRange` on collections and a new element's defaults are not hinted.
- Other validation attributes (`DefinitionAllowedElements`, `NotEmpty`, ...).
- `TimeRange`: left out because the game reports it in ticks while `read` shows a TimeSpan; it needs its bounds converted before it can hint.
- A null string under `StringLength`: the game's check throws on it, so no hint is given.
