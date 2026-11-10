# Editor tools: writers

Tools that change existing content: `write`, `element_add`, `element_remove`, `component_remove`, `component_upgrade`, and edits of a block's components. The shared contract is in `Tools_Overview.md`.

## write

- The value is JSON, coerced to the field's declared type. A struct that read prints inline takes the same form, and fields left out keep their value.
- **References** point at an existing item, in the mod or a read-only project; nothing is copied or created. The description says so, because a small model otherwise made its own copies of an item it could point at.
- **Reference to an override:** written as the override's original. An override merges into its original and its own id names no definition, so the reference would dangle, while the original already carries the override's values. The reply names the override it went past and says when the value did not change: a model that wanted values for one block alone read the bare `original -> original` reply as a failed write and retried it.
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

## component_upgrade

Changes a block's component to a type derived from its own, such as a cargo container's FunctionalBlockComponent to a PowerableBlockComponent so it draws power. Nothing the block had is lost: losing data is worse than a stale file.

- **Base:** a template, a prefab or block, or a definition, by template path or by item. The new type is the one the base offers that derives from the component's type: the base block's component, or the component types taking the definition's type, an exact match before an assignable one. None, several, or the component's own type is refused, naming what the base offers.
- **Definition:** a partial copy of the base's definition, so the fields only the new type has hold the base's values, which a template sets for its family (a thruster template's resource and priority). Every field of the old definition, resolved through its partials, is then written over it where it differs, as the inspector's per-field copy and paste. The model picks the base, so no hand-kept defaults are needed.
- The new definition is named after the old one with its type name swapped for the new one's, numbered when taken, and goes in the block's folder. There is no name argument: one thing fewer for the model to get wrong.
- **Component:** removed and added again on each side it was on, with its id and tags, plus any default tag of the new type no other component holds. Its fields start as the base block's component has them, or the type's defaults for a bare definition, and every field of the old component, hidden ones included, is written over them.
- Dependencies, interface dependencies and companions are filled as `component_add` fills them.
- The reply names the fields the new type brought, with the values they got, so the model can change them.
- The old definition file stays, and the reply says when nothing references it, through the same code as `component_remove`, so both always behave alike.
