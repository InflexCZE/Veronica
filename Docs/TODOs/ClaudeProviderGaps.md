# Claude provider gaps

**Exists:** the Claude provider and its MCP endpoint (`Docs/Tech/ClaudeProvider.md`).

**Missing:**
- **Browser requests (owner decision):** the endpoint does not check `Origin` or `Content-Type`. A web page open in the user's browser can post a `tools/call` without a CORS preflight (a `text/plain` body) and run Editor tools. Rejecting requests that carry an `Origin` header would stop browsers and leave MCP clients and `curl` working; it narrows "any local client" in the design.
- **Failed resume:** when `--resume` fails because Claude Code no longer has the session, every later turn fails the same way until `/clear`. The `session_id` in Claude Code's init event is not checked.
- **External call at turn start:** a call from another client that was waiting for the tool gate when a turn began runs inside that turn, after a 5 second wait for a matching tool call that never comes.
- **Long tools, untested:** a tool running longer than about 5 minutes. The endpoint sends nothing until the tool finishes; Claude Code's HTTP client may give up before `MCP_TOOL_TIMEOUT` and retry, and the retry would run the tool a second time.

**Start at:** `Source/Veronica.Agent/Claude/McpServer.cs`, `Source/Veronica.Agent/Claude/ClaudeProvider.cs`.
