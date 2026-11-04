# Vanilla template defects

**Exists:** blocks made with `create_prefab` from a vanilla template carry what the template has.

**Missing:** templates that fail validation as they are, so every block made from them carries an error the model did not cause:
- Sensor: its client composition has `BlockToolbarControllerComponent` without its `EntityAdapterComponent` ("Unsatisfied dependency 'EntityAdapterComponent' on BlockToolbarControllerComponent"); vanilla Sensor25 has neither. Seen in every MySensor run (`.tmp/eval/runs/20261104-085429-MySensor` and the 2026-11-01 runs).
- Landing Gear: DetectionBasedCooldownComponent and DetectionBasedCooldownDetector keep the template's abstract definitions ("Non-abstract definition cannot reference an abstract definition", 4 errors; `.tmp/eval/runs/20261104-103147-MyLandingGear`).
- Reactors: the BlockDestructionExplosionDefinition's Explosion is null ("non-nullable but null"; every MyReactor run, e.g. `.tmp/eval/runs/20261104-111637-MyReactor`); vanilla reactors point it at an ExplosionDefinition.
- Doors: OpeningDuration 0, no client definition, no parts provider (block-door fixes them).

**Owner decision:** whether create_prefab repairs known template defects (fill the missing dependency, drop the stray component), the skills do it, or the eval tasks accept the template's own errors.
