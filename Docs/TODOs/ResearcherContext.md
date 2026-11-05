# The researcher's context is not compacted

**Exists:** the wiki researcher's brief is the chat's whole transcript rendered as text (`Docs/Tech/Wiki.md`), so it grows with the chat. Its reading stops once its prompt passes the context window less 10K tokens. Researchers do not compact (`Docs/Tech/Compaction.md`).

**Missing (needs design with the owner):** a researcher that inherits a long transcript starts close to its wall and has little room to read. Options are open, such as bounding the rendered transcript, briefing it from a summary, or compacting it.

The reading bound rests on the assumed 45K window (`ContextWindow.md`). Against a smaller server window (eval runs at 25K), the bound lies past the real wall: the researcher hits the wall before it is told to answer, its request fails, and the chat's `wiki` call gets `wiki failed: <the server's error>` as its result. The chat's turn goes on; the research is lost.
