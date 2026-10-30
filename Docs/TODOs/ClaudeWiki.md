# No wiki with the Claude provider

**Exists:** the Claude provider cannot run a second session while its `claude -p` turn waits in a tool call (`Docs/Tech/Wiki.md`, Subagents), and `wiki` is not served over MCP, the only way Claude reaches Veronica's tools. Claude's system prompt still has the line telling it to ask the wiki. What is only in the wiki is out of Claude's reach; Claude mostly knows it, so this is not a priority.

**Missing:** wiki access for Claude, and the system prompt line dropped while it has none. A large model with a large context may not need the researcher: the page index and `wiki_page` could be offered directly over MCP. Needs an owner decision.
