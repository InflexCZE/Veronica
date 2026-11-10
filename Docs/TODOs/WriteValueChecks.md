# Game validation hints beyond the written field

**Exists:** `write` runs the written field's range, string length and unit vector validations and hints in its reply when the value is out of range (`Source/Veronica.Editing/ValueChecks.cs`).

**Missing**, deferred by the owner until evals show the need:
- Fields whose bounds name the written field: lowering a battery's MaxDischargeRate below its MaxChargingRate gives no hint, the charging rate's own range is broken.
- `element_add` and `element_remove`: no checks, so `LengthRange` on collections and a new element's defaults are not hinted.
- Other validation attributes (`DefinitionAllowedElements`, `NotEmpty`, ...).
- `TimeRange`: left out because the game reports it in ticks while `read` shows a TimeSpan; it needs its bounds converted before it can hint.
- A null string under `StringLength`: the game's check throws on it, so no hint is given.
- A bare number written to a TimeSpan field is read as days (`write(DurationMin, 5)` stored 5.00:00:00); `block-timer` names the hh:mm:ss form and no MyTimer run hit it. Whether the tool refuses a bare number or reads it as seconds is an owner decision.
- Writing none to a resource handle the game requires (`UIData.Icon`) is accepted without a hint and leaves a lasting `ResourceHandleValidator` error ("Resource handle <undefined> is not a guid", run `.tmp/eval/runs/20261102-064122-MyDrill`). `create-block` now keeps the template's icon when the user has none; whether `write` should also hint it is an owner decision.
- A fracture name (`CriticalFractures`, `ModelDummy.SupportingFracture`) is taken without checking it against the fractured model's pieces; the Editor's validation warns on a name the model lacks (`FractureName`, a warning only). Nothing lets the model list a model's piece names, so on a modder's own model it can only guess them.
- `write(BlockToolbarServerDefinition.TilesCount, 12)` was accepted without a hint although the field has `[Range<float>(1, 10)]`: a float range on an int field may not be matched.
