# Veronica agent rules

- Entry points: `Docs/Spec/Index.md` (user behaviours and scenarios) and `Docs/Tech/Index.md` (architecture decisions). Read them before working.
- `Docs/TODOs/Index.md` lists known missing or unfinished work, one file per item. When work leaves something undone (a deferred piece, a known gap, a review finding not fixed), add or update its file in the same commit, without asking; delete the file when the item is done.
- The docs cover architecture and why. They do not restate what the code does; implementation details belong in the code.
- The docs hold only decisions the owner approved. A design fork found during implementation goes to the owner as options with a recommendation; it is not decided in code.

## Tests

- Tests are deterministic and reproducible: no web and no disk writes. Reading committed repo files (shipped skills, wiki) is allowed.
- Decisions live in pure code tested on data. Disk goes through a file interface with an in-memory version for tests; HTTP goes through `HttpClient` over an in-memory `HttpMessageHandler`.
- Recorded external formats (GitHub, Hugging Face, `.vrgproj`, `ModIndex`) are inline strings in the tests.
- What only the real machine can show is checked by hand: a live run, or a checklist in the doc that owns it.
- Agree which parts get unit tests and which are left to a live run with the owner before writing them.

## Commit style

- Headline: what the change brings to the user or the project, not how it was done or the history behind it.
- Blank line, then `- ` bullets, as many as needed, for what a quick look at the diff does not tell: why a non-obvious choice was made, an owner decision, a TODO added or closed.
- Eval results stay: when a commit adds, fixes or improves an eval task, name the model, the task and the result (`27B MyGatling 0/3 -> 3/3`).
- No implementation details the diff shows, no test or line counts, no run IDs, transcript line numbers or commit hashes.
