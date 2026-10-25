# Editor: freeze and evict around a turn, edge cases

**Exists:** `Docs/Tech/VramSharing.md` (Editor residency) is implemented in `Source/Veronica/Residency` and passed the owner's live test: local turns ran with the Editor frozen and evicted (about 2.2 GB on a fresh Editor) and came back without issues.

**Missing:** edge cases the live test did not specifically cover:
- Nothing faults the GPU while evicted:
  - `FileTexture` heap allocations from a worker;
  - Media Foundation decode threads, when a scene has a video player;
  - asset previews and thumbnails generated during a turn.
- The frame commit job disabled during a turn: a write-heavy turn with an asset preview open (each content push used to commit 2 frames until the UI thread blocked); the Editor rendering the turn's recorded commands correctly in one frame afterwards.
- Tools during a turn, with the update loop stopped: `write` and `create_prefab` waiting for file locks or generators that need engine ticks (the 15 s busy retry).
- Project unload during a turn: whether `UnloadProject` waits on the parked render thread (see `ReloadDuringTurn.md` for reload).
- An Editor started without a project: the startup log must still say "GPU resource pool tracking is on".
- Turn-start and turn-end cost against the spike's numbers.
