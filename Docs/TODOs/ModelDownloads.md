# Model downloads in Veronica.exe

**Exists:** `Docs/Tech/Distribution.md` decides that `Veronica.exe` downloads the models the WebQwen manifest pins, through `Downloads` into `Models\<model id>`, before WebQwen ever loads one. Today the files are placed by `Build/FetchModel.ps1`, which already verifies against the Hugging Face hashes. `WebQwen.exe` serves them (`Source/WebQwen/ModelFiles.cs`). The Cache API trampoline (`Source/WebQwen.Native/js/runtime.js`) rejects `cache.add` for anything the host lacks, and ignores abort signals.

**Missing:**
- The downloader in `Veronica.exe`: weights and library at the pinned revisions, resume after an interruption, progress, verification against the Hugging Face hashes and the library sha256, move into `Models` only when verified.
- Replacing an installed model whose pinned revision changed, deleting the old revision after the new one is verified.
- Cancellation of a load (web-llm's reload abort expects a `DOMException` named `AbortError`).
