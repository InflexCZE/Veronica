# Release publish

**Exists:** `Docs/Tech/Distribution.md` decides the release: `Veronica.exe` and `Veronica.zip` (`Runtime`, `Skills`, `Wiki`) on GitHub releases of this repo. The dev build puts the C# output in `Source/WebQwen/bin/...` with the native DLLs and the pinned DXC copied next to it.

**Missing:**
- A publish step producing `Veronica.zip`: the plugin and its private DLLs, `WebQwen.exe` as a framework-dependent single file with `wq.dll`, `webgpu_dawn.dll` and `manifest.json` (no DXC, no pdb, no test files), and the shipped skills and wiki.
- The self-contained single-file publish of `Veronica.exe`.
- Creating the GitHub release with both assets.
