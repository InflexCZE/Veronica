# MCP endpoint

`Veronica.dll` serves the conversation's tools to any local MCP client: the Claude provider, or a client the user runs beside the chat.

- It starts with the panel when settings switch it on, or when the provider needs it (Claude), and listens until the Editor exits. Clients can reach the tools before any chat.
- A port already in use does not stop the chat: the failure is logged and shown in the panel.
- MCP Streamable HTTP with plain JSON responses on loopback. A raw socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- No authentication: any local process can call Veronica's tools. This is accepted for a loopback-only endpoint.
- The tools are the conversation's tool set with `list_wiki` and `wiki_page` in place of `wiki`, whose researcher runs only inside a provider's session (`Wiki.md`).

## Calls

- Tools run one at a time whichever source calls them, chat or MCP (`Agent.md`, Tools). Which edit wins between a client and the chat is the user's responsibility, as with a click in the Editor.
- A call runs directly and leaves no trace in the chat, unless a provider that runs its own loop routes it into its turn (`ClaudeProvider.md`). The client-loop providers route nothing.
- `ask_user` from any client shows the question card and blocks until it is answered or closed. It waits outside the one-at-a-time queue, so other calls run meanwhile.

## Bare MCP

`VERONICA_PROVIDER=bare_mcp` creates no provider: no WebQwen, no VRAM sharing. The endpoint always starts, and the panel shows the connection prompt and the open session count instead of the chat.

- `initialize` issues an `Mcp-Session-Id`, and each id gets a session state of its own (`Skills.md`, State). `DELETE` ends a session; otherwise it lasts until the Editor exits.
- A request without an id works on the chat's state, so a client that ignores sessions still works. An unknown id gets 404, which makes a conforming client start a new session after an Editor restart.
- Only bare MCP issues ids. The Claude provider starts a new `claude -p` each turn, so per-session state would give every turn an empty plan; it and every other client share the chat's state.
- A bare client gets the shared system prompt from `read_instructions`, and `initialize` only tells it to call that first: Claude Code cuts long server instructions short. Bare MCP only, since the Claude provider already has the prompt as its system prompt. One prompt serves every provider, so it names tools a bare client lacks (`wiki`, `ask_user`, `wait_for_user`); bare clients are large models that map them to what they have.
- `ask_user` is not served: no chat shows its card, and the client asks in its own UI.
- The open count is sessions opened less those deleted. Claude Code never sends `DELETE`, so the count grows until the Editor restarts; accepted, with no idle tracking.
- The Launcher's check runs the same server with no tools and a one-line instructions text, and passes on `initialize`: it proves the agent's configuration, which the Editor's endpoint on the same port then serves.
