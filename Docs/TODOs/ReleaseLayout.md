# WebQwen release folder

**Exists:** `Docs/Tech/WebQwen.md` lists the release as one folder: `WebQwen.exe` (.NET 9, framework-dependent), `wq.dll`, `webgpu_dawn.dll`; `dxcompiler.dll` / `dxil.dll` are copied in by Veronica from the Editor. The dev build puts the C# output in `Source/WebQwen/bin/...` with the native DLLs and the pinned DXC copied next to it.

**Missing:** a publish step that produces exactly the release folder (no DXC, no test files, the manifest only if the product still needs one locally), and its place in the GitHub release of this repo.
