# ask_user from a disconnected MCP client

**Exists:** `ask_user` over MCP blocks until the card is answered or closed (`Docs/Tech/Agent.md`, Questions to the user). A Claude turn that ends takes its questions back.

**Missing:** a direct MCP call (no Claude turn routing it) gets only the endpoint's lifetime token. A client that times out or disconnects leaves its questions on the card until the user answers or closes it, and those answers go nowhere.

**Start at:** `Source/Veronica.Agent/Mcp/McpServer.cs`, the tool call: a per-request token cancelled when the connection drops.
