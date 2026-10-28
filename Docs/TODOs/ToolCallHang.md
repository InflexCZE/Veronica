# A tool call that never returns blocks the chat

**Exists:** Editor tools run their work on the Editor's main thread and the tool loop waits for each result with no time limit. Seen once in an eval run (`.tmp/eval/runs/20261028-044920-IonThruster100Block`, 9B, OpenAI-compatible provider): right after the project loaded, the first `find_content` never returned, the Editor log ended with "MainWindow Menu lost focus", and Stop did not end the turn. The run was killed after about 20 minutes. Cause unknown; the server was answering.

**Missing (needs design with the owner):** a way out of a tool call that does not return, for example a timeout that fails the call with a result the model can act on, and Stop releasing the turn while a tool runs. For eval runs, a liveness check that `Wait-Ready.ps1` or the fork can use, so a hung Editor is reported instead of waited on. Where to start: the tool execution path in `ClientToolLoop` and the Editor tools' main-thread dispatch.
