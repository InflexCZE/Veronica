# create_prefab outside the create-block plan

**Exists:** create-block's plan sets a new block's name and its Model, FractureModel and DamagedModel after `create_prefab`.

**Missing:** when the model calls `create_prefab` without create-block active, nothing names what is still left. Bonsai MyDoor `.tmp/eval/runs/20261107-191602-MyDoor` read block-door, explored, called `create_prefab` directly and activated only block-door: the name stayed the template's and FractureModel and DamagedModel stayed none, 8 validation errors at the end.

**Options:** create_prefab's reply names what is still to set (name, the three models), or points at create-block when no create-block plan is active.

The skill-activation wording in the system prompt (hypothesis H1a) targets the same failure and may make this unnecessary. Observe how often it recurs before acting.
