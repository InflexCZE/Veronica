# MCP endpoint

The assistant's tools are available to any MCP client on the modder's machine, so other agents can work on the open project too.

- Opt in through settings; always on with the Claude Code provider and with no model (`AiProviders.md`).
- Loopback only, no authentication.
- Every chat tool, except that the wiki is read page by page instead of asking a researcher.
- A call runs silently: no chat line, no Stop. Only during a Claude Code turn are calls routed into the turn and shown in the chat; during a local-model or OpenAI-compatible turn they stay silent too.
- A question from a client shows the question card (`Chat.md`) and waits until the modder answers or closes it; other calls still run meanwhile.
- Calls run one at a time, together with the chat's own tool calls.
- A busy port shows in the panel; the chat keeps working.

## With no model

- Each client connection works on its own skill plans, starting from the skills and wiki as they are on disk when it connects. They last until the client closes the connection or the Editor exits.
- A client that does not keep the protocol's connection id still works, sharing one state with every other such client.
- The client is told to read Veronica's working rules before it starts.
- No question card: there is no chat to show it, so the client asks the modder in its own UI.
