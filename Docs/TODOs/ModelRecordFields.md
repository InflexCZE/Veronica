# Model record fields beyond Bonsai's

**Exists:** `wq_model` passes model id, model URL, library URL and sha256, required features and context window. `api.js` builds web-llm's model record from these only. This covers Bonsai (no MTP).

**Missing:** the other production models in the manifest (Qwen3.5-9B, Qwen3.8-27B) carry `mtp_head`, `mtp_draft_length`, `mtp_min_draft_prob`, which web-llm's model record needs to run their multi-token prediction head. Add them to `wq_model`, the managed mirror and `Manifest.Model`, with golden references for those models when they are offered.
