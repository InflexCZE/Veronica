# TimeSpan writes do not show their format

**Exists:** `write` sets a TimeSpan field from text such as `"00:00:00.5"` (DoorServerDefinition.OpeningDuration).

**Missing:** a number (`0.5`) fails with "'0.5' is not a valid TimeSpan" and the reply does not show the accepted form, so the model guesses. The reply should name the form (`hh:mm:ss.fff`), or seconds as a number should be accepted. Start in `Source/Veronica.Editing/Values.cs`.
