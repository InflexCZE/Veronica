# Dictionary keys in field paths

**Exists:** read prints dictionary entries as `Path[key]`, and the walker resolves `[key]` or `.key` (`Source/Veronica.Editing/FieldWalker.cs`). Keys are text, enum names, numbers or StringIds.

**Missing:** some printed entries cannot be addressed back:
- A key containing `]` prints as `Map[a]b]`, which the path parser rejects.
- A key with leading or trailing spaces is trimmed on the way back and not found.
- Keys of types without a text form (definitions, `Type`, other classes) print with `ToString()`, which can show an id, and cannot be parsed back.

None of these occur in the thruster flow. Start with an escape or quoting rule for keys in `FieldPath`.
