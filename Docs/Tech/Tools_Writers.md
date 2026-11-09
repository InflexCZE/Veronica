# Editor tools: writers

Tools that change existing content: `write`, `element_add`, `element_remove`, `component_remove`, and edits of a block's components. The shared contract is in `Tools_Overview.md`.

## write

- The value is JSON, coerced to the field's declared type. A struct that read prints inline takes the same form, and fields left out keep their value.
- **References** point at an existing item, in the mod or a read-only project; nothing is copied or created. The description says so, because a small model otherwise made its own copies of an item it could point at.
- **Reference to an override:** written as the override's original. An override merges into its original and its own id names no definition, so the reference would dangle, while the original already carries the override's values.
- **Clear** (`null`) resets a field to its type's default; it never removes an object or list that is not nullable.
- **Null parents** on the path are created with their defaults first, and removed again when the write then fails.
- Decimal numbers take a dot only: invariant parsing would read a comma as a thousands separator.

## Game validation hint

- The written field's own validation runs through the engine's validation, and what it reports is added to the reply. The value is written all the same: two fields can bound each other (a battery's charge rate below its discharge rate), and refusing either write would deadlock.
- A value in range adds nothing: a report after every write confuses a small model.
- A bound held by another field, or by a component's definition, is named with that field's current value, so the model knows which value to correct.
- Only the written field is checked, not the fields whose bounds name it (`Docs/TODOs/WriteValueChecks.md`).

## element_add and element_remove

- Both go through the engine's own collection operations, so on a partial they are recorded as overrides like any write.
- `element_add` adds a default element and returns its path; the model fills it with `write`. A list the engine keys itself (mergeable lists) takes no key, as the inspector's add button relies on.
- A value given with the add is written at once. A value that does not fit removes the element again and the error says nothing was added: told only how to write the element's fields, a model took the element as kept.
- An abstract element type needs a concrete type; without one the error lists the types the inspector's add button offers.
- A set's element is the element itself: it is added and removed, never written.

## Block components

- **Tags** are added and removed one at a time, never written as a whole. Both are refused when they would break the block: a side keeps one component per tag, so adding a tag held elsewhere would take it from its holder, and removing a tag another component requires breaks that dependency at load. Moving a tag that dependents rely on is not possible (`Docs/TODOs/PrefabTagMove.md`).
- `component_remove` removes a component from every side it is on. It is refused when another component on the same side requires a tag it fills, since the block would break at load.
- The removed component's definition file stays; the reply says when nothing references it any more.
- Replacing a block's component is not a tool operation (`Docs/TODOs/ReplaceBlockComponent.md`).
