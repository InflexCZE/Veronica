# Testing

Rules every feature's tests follow. The tests themselves are the record of what is covered.

## Unit tests

- Deterministic and reproducible: no web and no disk writes. Reading committed repo files (shipped skills, wiki) is allowed.
- Decisions live in pure code tested on data: state and inputs in, actions out.
- Effects sit behind seams with in-memory versions: disk through a file interface, HTTP through `HttpClient` over an in-memory `HttpMessageHandler`, the Editor-running check through an interface.
- Recorded external formats (GitHub, Hugging Face, `.vrgproj`, `ModIndex`) are inline strings in the tests.
- A unit test never starts an external program: not the Editor, not Claude Code. The one exception is the local model runtime's correctness gate, which loads the real model on the GPU, is kept apart by its `Gpu` trait and runs when a new runtime build is pulled.
- Which parts get unit tests and which are left to a live run is agreed with the owner before the tests are written.

## Layering for testability

- Logic that can run without the Editor lives in assemblies that do not need it: `Veronica.Agent.dll` has no SE2 references, and `Veronica.Editing.dll` references only engine assemblies that load without the Editor, so the editing rules are tested on engine types without a running Editor.
- Thin glue over something only the real machine has (the Editor's project layer, Claude Code, the MCP transport, the UI) gets no unit tests and is checked live.
- Test projects split where their builds clash or their toolchains differ:
  - `Veronica.Tests` builds against the Editor folder.
  - `Veronica.Setup.Tests` stays apart, because the Editor's Avalonia build clashes with the public one the Launcher ships.
  - `WebQwen.Tests` needs the native toolchain, so `Veronica.Tests` builds with the managed SDK alone.

## Live checks

- What only the real machine can show is checked in a live run in the Editor on the dev mod, reverted with git afterwards.
- The model's behaviour with tools, skills and context is measured by eval runs (`Eval.md`), not unit tests.
- Checks due before a release are a skill, `release-check`, not documentation.
