# Claude provider gaps

**Exists:** the Claude provider (`Docs/Tech/ClaudeProvider.md`) and the MCP endpoint (`Docs/Tech/McpEndpoint.md`).

**Missing:**
- **Browser requests (owner decision):** the endpoint does not check `Origin` or `Content-Type`. A web page open in the user's browser can post a `tools/call` without a CORS preflight (a `text/plain` body) and run Editor tools. Rejecting requests that carry an `Origin` header would stop browsers and leave MCP clients and `curl` working; it narrows "any local client" in the design.
- **Failed resume:** when `--resume` fails because Claude Code no longer has the session, every later turn fails the same way until `/clear`. The `session_id` in Claude Code's init event is not checked.
- **Long tools, untested:** a tool running longer than about 5 minutes. The endpoint sends nothing until the tool finishes; Claude Code's HTTP client may give up before `MCP_TOOL_TIMEOUT` and retry, and the retry would run the tool a second time.

- **Hung external call:** an MCP call outside a Claude turn is cancelled only when the endpoint is disposed, which never happens, and a client disconnecting does not cancel it. Since tools run one at a time, a tool that hangs there (such as the UI dispatch hang in `ToolCallHang.md`) blocks every chat's tools until it returns.

**Start at:** `Source/Veronica.Agent/Mcp/McpServer.cs`, `Source/Veronica.Agent/Claude/ClaudeProvider.cs`.
