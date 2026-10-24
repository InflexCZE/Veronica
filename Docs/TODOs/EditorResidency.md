# Editor: freeze and evict around a turn

**Exists:** `Docs/Tech/VramSharing.md` decides that the Editor stops all engine-device submits and evicts every engine heap and committed resource for the length of a turn (and for the weight upload of a model load), then makes them resident and resumes. Nothing implements it.

**Missing:**
- Engine hook (VRAGE3): stop every engine queue (graphics, compute, copy, streaming) and wait for it to go idle; enumerate every engine-owned pageable for `ID3D12Device::Evict`; restore with `EnqueueMakeResident` (or `MakeResident`) and wait before the first frame.
- A guarantee that no engine work is submitted while evicted: any access faults the GPU.
- Veronica: drive the sequence (Editor evict, WebQwen resident, turn, WebQwen evict, Editor resident) through the Veronica/WebQwen protocol.
