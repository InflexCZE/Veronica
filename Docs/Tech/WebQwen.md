# WebQwen.exe

Local model runtime provider. Runs the WebQwen inference engine outside the browser.

## Scope

- Windows x64 desktop, D3D12, AMD and NVIDIA GPUs with 8 GB VRAM or more.
- First model: Bonsai (Ternary-Bonsai-2-27B).
- Output is close to the browser build of the engine; bit-identical output is not required.

## Architecture

### Runtime

- The WebQwen engine (built JS plus the model library `.wasm`) runs unchanged in an embedded V8. One engine source serves the browser and WebQwen.exe.
- A native library does the inference: V8 plus WebGPU, behind a small C API.
- The C API is typed and tailored to inference (loading a model, running a request, streaming its output), not a generic script host. The managed side mirrors it 1:1. Both ship together in one release, so the API has no versioning or backward compatibility.
- The engine bundle (web-llm, from the WebQwen dist) and the trampolines are embedded in `wq.dll`. The bundle is vendored as the dist's tarball and manifest; the build checks the tarball against the manifest's sha256 and embeds only the engine script. Engine, trampolines and native bindings version as one unit.
- `wq.dll` owns one engine thread. Every C API call queues its work and returns; results and streamed output arrive through callbacks on that thread, which the managed side surfaces as tasks and async streams.
- Scripts reach only the functions WebQwen.exe installs. There is no file system, network or process access from script, so the runtime exposes nothing to the user's machine beyond what the engine needs.
- The browser environment the engine expects is provided by JS trampolines preloaded before the engine. Each web API is implemented only as far as the engine uses it, and terminates by level:
  - low level (clock, timers, WebGPU) in `wq.dll`;
  - high level (files, downloads, storage) in C#.

### GPU

- WebGPU is Dawn on D3D12. Dawn is the implementation the browser build runs on: every engine kernel compiles unchanged, and inference speed matches the browser.
- One thread runs the V8 event loop and every Dawn call. The GPU is the bottleneck: Dawn's command-list translation inside `submit` (about 2 ms per submit) overlaps GPU work and costs no throughput.
- WebGPU calls map 1:1 onto native functions, with no command batching. A V8 call into native code costs about 10 ns, about 35 µs per decoded token.

### I/O and models

- C# does all I/O: downloading, caching, file access. Script I/O goes through callbacks into C#.
- Model artifacts come from one fixed Hugging Face repo: the weights, the model library `.wasm` (under a fixed file name) and a manifest describing the model. Veronica downloads the latest.
- Veronica downloads the manifest first and asks WebQwen.exe whether this machine meets the model's requirements (GPU features, limits, VRAM). Only then does it download the weights.
- C# owns the model files: it downloads and stores them, and the engine reads them through the trampolines. The engine's own browser cache is a pass-through, so the weights exist on disk once: every cache scope is a view of the resources C# serves by URL.
- C# stores Dawn's compiled shader cache, so pipelines compile once per machine rather than on every start.

## Binaries

One folder, side by side:

| file | what |
|---|---|
| `WebQwen.exe` | C#, .NET 9, framework-dependent (the Editor requires .NET 9 already). All I/O; loads `wq.dll` in-process. |
| `wq.dll` | V8, the script host and the WebGPU functions; the C API `WebQwen.exe` calls. |
| `webgpu_dawn.dll` | Dawn's `webgpu.h` C API. |
| `dxcompiler.dll`, `dxil.dll` | DirectX Shader Compiler, required by Dawn's D3D12 backend. Not part of the WebQwen release: Veronica copies them from the Editor's binaries folder when it installs or updates WebQwen. |

## Build

- **V8:** the prebuilt static monolith from the just-js/v8 GitHub releases, one pinned version (15.3). It is built with Chromium's libc++ and without C++ exceptions, so code that includes `v8.h`:
  - compiles with clang against the release's libc++ headers (`libcxx-headers-win-x64.zip`), with `-fno-exceptions`;
  - links `msvcprt`;
  - links without unreferenced-section removal (lld-link `-opt:noref`), because V8 crashes on its first script when the linker drops those sections.
- **Prebuilts** (V8, Dawn, and DXC for development and test runs) are pinned by URL and sha256 and fetched into a gitignored folder. Machine-specific settings (the LLVM path) live in an untracked local props file.
- **Dawn:** the prebuilt static library from the google/dawn GitHub releases (Windows Release), one pinned version. It is linked into its own DLL, `webgpu_dawn.dll`, which exports the `webgpu.h` C API, because V8 and Dawn both bundle Abseil and collide in one binary. The release references MSVC STL vectorized helpers (`__std_*`) newer than MSVC 14.44; the DLL supplies scalar versions of them.

## Testing

- Correctness is gated against the browser build: the same prompts, teacher-forced (every reference token is fed back, so one disagreement cannot cascade), compared on the next-token distribution.
- Gate: top-1 agreement >= 97.2%, median KL <= 0.002, p95 KL <= 0.025 (KL over the reference top-20, both sides renormalised), first token right on at least 11 of 12 prompts. Speed is not gated.
- The references are the browser build's dumps for the exact model library, committed with the tests. They are refreshed with the engine: pulling a new WebQwen build checks which slices shipped and takes the dumps of any changed library.
- Model files for development and tests come from the manifest's pinned Hugging Face revision and are verified against its hashes, whether downloaded or copied from a local build.
