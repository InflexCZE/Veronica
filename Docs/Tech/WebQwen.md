# WebQwen

The local model provider: `WebQwen.exe` runs the WebQwen inference engine outside the browser, and `Veronica.dll` drives it as a child process.

## Scope

- Windows x64, D3D12, AMD and NVIDIA GPUs.
- Models are picked to fit 8 GB and 16 GB cards.
- Output stays close to the browser build of the engine; bit-identical output is not required.

## Runtime

- The engine (the web-llm JS bundle and the model library `.wasm`) runs unchanged in an embedded V8, so one engine source serves the browser and `WebQwen.exe`.
- `wq.dll` holds V8, the script host and the WebGPU bindings behind a C API tailored to inference: load a model, run a chat, stream its output, score tokens. It is not a generic script host.
- A chat request crosses the C API as one OpenAI-shaped JSON string: structs would have to marshal nested messages, tool calls and schemas, and a request is rare next to the work it starts.
- `wq.dll` owns one engine thread that runs the V8 event loop and every Dawn call. Every C API call queues work and returns; callbacks arrive on that thread.
- The browser environment the engine expects is provided by JS trampolines, each web API only as far as the engine uses it. Low-level APIs (clock, timers, WebGPU) end in `wq.dll`, high-level ones (files, downloads, storage) in C#.
- Scripts reach only the functions the host installs: no file system, network or process access, so the runtime exposes nothing of the machine beyond what the engine needs.
- Engine bundle, trampolines, native bindings and the managed side ship together and version as one unit; the C API has no versioning or backward compatibility.

## GPU

- WebGPU is Dawn on D3D12, the implementation the browser build runs on: every engine kernel compiles unchanged and inference speed matches the browser.
- WebGPU calls map 1:1 onto native functions without command batching. The GPU is the bottleneck: a V8-to-native call costs about 10 ns, and Dawn's command translation inside `submit` overlaps GPU work.
- Dawn is linked into its own `webgpu_dawn.dll`, because V8 and Dawn both bundle Abseil and collide in one binary. That DLL also carries the residency control of large buffers (`VramSharing.md`).
- The DirectX Shader Compiler Dawn needs is not shipped: the plugin copies it from the Editor's folder before it starts `WebQwen.exe`.
- Compiled pipelines are to be cached by C# so they compile once per machine (`Docs/TODOs/ShaderCache.md`).

## Models and I/O

- C# does all I/O. The engine's browser cache is a pass-through to files C# serves by URL, so the weights exist on disk once.
- `manifest.json` ships beside `WebQwen.exe` and pins every model: weights at a Hugging Face commit, model library by sha256, and its requirements. Everything is on disk before a load, so the engine stays offline.
- Whether a machine can run a model is answered by `WebQwen.exe`, using the engine's own adapter check against the manifest record (`Docs/TODOs/RequirementsCheck.md`).

## Process and protocol

- The plugin runs `WebQwen.exe` as a child process, one process per model, started with the first round and kept up. A process that exits or breaks the protocol fails the running round, and the next round starts a new one.
- They talk over stdio, one JSON object per line, commands on stdin and events on stdout; the log goes to stderr. The message types are one source file compiled into both sides. Both come from the same release, so the protocol has no version.
- The process dies with the Editor however the Editor ends: it sits in a job object whose handle the plugin never closes, and it exits by itself when its stdin closes.
- Each chat request carries the whole conversation. The engine reuses its KV cache for the prefix shared with the previous request, which only works while every earlier reply, reasoning included, comes back byte-identical.
- The plugin always starts `WebQwen.exe` with a context window (`VERONICA_CONTEXT_SIZE`), and the provider reports that size: without one the engine silently caps the window below what the agent assumes. The KV cache is allocated for the whole window at load, and a window that does not fit VRAM spills into shared memory without an error and decodes several times slower.
- A prompt that does not fit the context window, and a reply that ends as `length` short of its token limit, are reported as the context wall (`Compaction.md`).
- The provider holds the Editor's VRAM for each whole turn and for each model load until the load completes, even when the turn that started it is stopped (`VramSharing.md`).
- Stop during start or load ends the turn; the load continues.

## Build

- V8 is the prebuilt static monolith of one pinned version, built with Chromium's libc++ and without C++ exceptions, so code that includes `v8.h` compiles with clang against that libc++.
- Prebuilts (V8, Dawn, DXC for development) are pinned by URL and sha256 and fetched, not committed.
- The engine bundle is vendored as the WebQwen dist tarball, checked against its manifest's sha256 and embedded in `wq.dll`.
- `WebQwen.exe` is a framework-dependent single file on .NET 9, which the Editor already requires. The native DLLs stay beside it: packed in, .NET would extract them to `%TEMP%` on the first start of every version.

## Correctness gate

- Correctness is gated against the browser build, not by speed: the same prompts, teacher-forced so one disagreement cannot cascade, compared on the next-token distribution.
- The references are the browser build's dumps for the exact model library, committed with the tests and refreshed whenever a pulled engine build changes a library.
