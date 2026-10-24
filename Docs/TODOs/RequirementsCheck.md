# Machine requirements check

**Exists:** `Docs/Tech/WebQwen.md` decides that Veronica asks WebQwen.exe whether the machine meets the model's requirements before downloading weights. Manifest records carry `required_features`, `required_limits`, `subgroup_min_size` / `subgroup_max_size` and `vram_required_MB`. The engine bundle exports `checkAdapter` and `combineRequirements` (Wrenchie uses them). Nothing calls them yet.

**Missing:**
- A C API call that requests the adapter and runs the engine's check against a record, without loading a model.
- Free VRAM, not only total. Measured on an RTX 3070: with other applications holding 4.7 of 8 GB, Bonsai (6.9 GB) loads and runs, but Dawn pages on every submit and decode drops from about 40 to 0.3 tok/s. The check, or a warning at load, must catch this (D3D12 / DXGI `QueryVideoMemoryInfo` gives budget and usage). Under `Docs/Tech/VramSharing.md` the Editor's own VRAM is evicted during turns, so only other processes count against the model. Demotion does not show in `QueryVideoMemoryInfo` usage; the `GPU Process Memory` performance counters show it.
