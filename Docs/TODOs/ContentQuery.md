# Content query: one field across many items

**Exists:** `find_content` finds items by name and type, `read` shows one item, `find_references` lists what points at one item.

**Missing:** a way to read the same fields across every item of a type and aggregate them, such as "ConsumedResource and ResourcesRequiredToThrust of every thruster". In an eval run the researcher read five thrusters one by one (22 calls, 14K of context) and then claimed a value held for every vanilla thruster, having read five. Field names are not searchable, so find_content cannot help.

**Direction (owner):** a query tool, possibly with a jq-like syntax, so the model can mass-query and aggregate in one call. Its syntax, scope and output cap are owner decisions.
