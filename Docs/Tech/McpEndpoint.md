# MCP endpoint

`Veronica.dll` serves the conversation's tools to any local MCP client: the Claude provider (`ClaudeProvider.md`), or a client the user runs beside the chat.

## Selection

Settings (`Distribution.md`), read once when the panel is created.

| setting | value |
|---|---|
| `VERONICA_MCP` | optional; `1` serves the endpoint with any provider |
| `VERONICA_MCP_PORT` | optional; unset is 47812 |

- The endpoint starts when the panel is created, with `VERONICA_MCP=1` or with the Claude provider, which cannot work without it. Other MCP clients can reach the tools before any chat.
- A port already in use is logged and shown in the chat status; the chat keeps working without the endpoint. With the Claude provider, each turn tries to start it again and fails with the same error.
- It listens until the Editor exits.

## Protocol

- MCP Streamable HTTP with plain JSON responses, on 127.0.0.1. A raw loopback socket serves it rather than `HttpListener`, which needs a URL reservation for a non-admin user.
- There is no authentication: any local MCP client can call Veronica's tools.
- The server is named `veronica`, so Claude sees each tool as `mcp__veronica__<tool>`. The tools are the conversation's `ToolSet` without `wiki` (`Wiki.md`), run one at a time with the chat's.

## Calls

- A call runs directly and leaves no trace: no chat line, no Stop.
- `ask_user` from any client shows the question card in the panel and blocks until it is answered or closed (`Agent.md`, Questions to the user). It does not hold the tool queue.
- A provider that runs its own tool loop routes the calls into its turn while one runs (`ClaudeProvider.md`). The WebQwen and OpenAI-compatible providers route nothing: a call during their turn runs directly too.
- Tools other than `ask_user` run one at a time, whichever source calls them (`Agent.md`, Tools), so a call made during a turn waits for the model's tool and the other way round. Which edit wins is still the user's responsibility, as with a click in the Editor's UI.

## Testing

- No unit tests: the endpoint is thin glue over HTTP.
- Tested by hand: `tools/list` and a `tools/call` before any chat, with each provider.
