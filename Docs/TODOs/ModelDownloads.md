# Model load cancellation

**Exists:** `Veronica.exe` downloads and verifies the models before WebQwen loads one (`Docs/Tech/Distribution.md`). The Cache API trampoline (`Source/WebQwen.Native/js/runtime.js`) rejects `cache.add` for anything the host lacks, and ignores abort signals.

**Missing:** cancellation of a model load (web-llm's reload abort expects a `DOMException` named `AbortError`).
