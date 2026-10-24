# Split model load

**Needs an owner decision.**

**Exists:** `Docs/Tech/VramSharing.md` decides that the Editor is frozen and evicted only for the weight upload of a model load, and live for the shader compile after it. Today a load is one engine operation: WebQwen releases its VRAM when the whole load completes, so the Editor stays frozen for all of it (about 37 s for Bonsai, about 10 s of it the weight upload). A spike proved the split works: WebQwen evicted its buffers when web-llm reported its first "Loading GPU shader modules" progress, the Editor resumed about 0.9 s later and rendered normally through the compile, and the allocations made after that point were not demoted.

**Missing:** the design. WebQwen cannot assume the Editor is frozen during a load. Open questions: who decides that the weights may be released mid-load, how WebQwen tells the host the upload is done (web-llm has no event for it; its progress text is the only signal), and what happens to GPU work later in the load once the weights are evicted.
