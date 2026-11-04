# Vanilla template defects

**Exists:** blocks made with `create_prefab` from a vanilla template carry what the template has.

**Missing:** templates that fail validation as they are, so every block made from them carries an error the model did not cause:
- Sensor: its client composition has `BlockToolbarControllerComponent` without its `EntityAdapterComponent` ("Unsatisfied dependency 'EntityAdapterComponent' on BlockToolbarControllerComponent"); vanilla Sensor25 has neither. Seen in every MySensor run (`.tmp/eval/runs/20261104-085429-MySensor` and the 2026-11-01 runs).
- Doors: OpeningDuration 0, no client definition, no parts provider (block-door fixes them).

**Owner decision:** whether create_prefab repairs known template defects (fill the missing dependency, drop the stray component), the skills do it, or the eval tasks accept the template's own errors.
