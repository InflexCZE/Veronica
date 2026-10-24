# Keep the Editor live when everything fits

**Needs an owner decision.**

**Exists:** `Docs/Tech/VramSharing.md` runs ping-pong residency on every card, so the Editor freezes during every turn even when the Editor, the model and everything else fit in VRAM. Measured with simulated loads that fit (3 GB model, 2 GB Editor on 8 GB): nothing was demoted, and an Editor rendering through the turn slowed inference by about 30% (shared GPU time).

**Missing:** whether to keep the Editor rendering when everything fits, and the signal that decides it. `QueryVideoMemoryInfo` does not reveal demotion; the `GPU Process Memory` performance counters do.
