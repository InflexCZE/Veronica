# Units of values the model reads

**Exists:** the wiki states the units of the thruster fields (`ThrustPower` in N, `ResourcesRequiredToThrust` in kW of Electricity or L/s of Hydrogen), and the system prompt says values and units come from a read or the wiki, not from memory.

**Missing:** in build tasks the model does not ask the wiki for a unit it only mentions in its summary, and invents one: "40 mW", "40 MJ/s", "40 power units" for the Ion 100's 40 kW (eval batches W1, C1, H2, 3 of 4 runs in H2). Options for the owner:
- `read` shows a field's unit where the game declares one (unit attributes on definition fields, a resource type's flow unit).
- The thrust step of `block-thruster` names the unit, which brings back field-specific steering the skills were rid of.
- Accept it: the values written are right, only the wording of the summary is off.
