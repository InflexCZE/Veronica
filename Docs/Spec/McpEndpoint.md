# MCP endpoint

The assistant's tools are available to any MCP client on the modder's machine, so other agents can work on the open project too.

- Opt in through settings; always on with the Claude Code provider.
- Loopback only, no authentication.
- Every chat tool except the wiki.
- A call runs silently: no chat line, no Stop. Only during a Claude Code turn are calls routed into the turn and shown in the chat; during a local-model or OpenAI-compatible turn they stay silent too.
- A question from a client shows the question card (`Chat.md`) and waits until the modder answers or closes it; other calls still run meanwhile.
- Calls run one at a time, together with the chat's own tool calls.
- A busy port shows in the chat status; the chat keeps working.
