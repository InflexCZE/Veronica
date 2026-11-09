# MCP endpoint

`Veronica.dll` serves the conversation's tools to any local MCP client: the Claude provider, or a client the user runs beside the chat.

- It starts with the panel when settings switch it on, or when the provider needs it (Claude), and listens until the Editor exits. Clients can reach the tools before any chat.
- A port already in use does not stop the chat: the failure is logged and shown in the chat status.
- MCP Streamable HTTP with plain JSON responses on loopback. A raw socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- No authentication: any local process can call Veronica's tools. This is accepted for a loopback-only endpoint.
- The tools are the conversation's tool set without `wiki`, whose researcher runs only inside a provider's session (`Wiki.md`).

## Calls

- Tools run one at a time whichever source calls them, chat or MCP (`Agent.md`, Tools). Which edit wins between a client and the chat is the user's responsibility, as with a click in the Editor.
- A call runs directly and leaves no trace in the chat, unless a provider that runs its own loop routes it into its turn (`ClaudeProvider.md`). The client-loop providers route nothing.
- `ask_user` from any client shows the question card and blocks until it is answered or closed. It waits outside the one-at-a-time queue, so other calls run meanwhile.
