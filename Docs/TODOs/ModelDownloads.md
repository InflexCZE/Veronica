# Model downloads in C#

**Exists:** `Docs/Tech/WebQwen.md` decides that C# downloads and stores the manifest, model library and weights from one fixed Hugging Face repo, manifest first. Today `WebQwen.exe` only serves files already on disk (`ModelFiles` in `Source/WebQwen/ModelFiles.cs`), placed there by `Build/FetchModel.ps1`. The Cache API trampoline (`Source/WebQwen.Native/js/runtime.js`) rejects `cache.add` for anything the host lacks, and ignores abort signals.

**Missing:**
- Downloader: manifest, then (after the requirements check) library and weights from the record's pinned revisions; verification against Hugging Face hashes (as `FetchModel.ps1` does) and the manifest's library sha256; resume; progress.
- Where it hooks in: either C# fetches everything before `LoadModelAsync`, or `cache.add` becomes an async host call that downloads on demand. Pick one; the first keeps the engine offline.
- Storage location and cleanup of old revisions.
- Cancellation of a load (web-llm's reload abort expects a `DOMException` named `AbortError`).
