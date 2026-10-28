# Claude provider

The Claude provider runs each turn through the Claude Code CLI (`claude -p`), which runs its own tool loop. Veronica's tools reach it through an MCP endpoint that `Veronica.dll` serves. The user must have Claude Code installed and signed in; Veronica does not install it.

## Selection

Environment variables, read once when the panel is created, like the OpenAI-compatible provider (`Agent.md`).

| variable | value |
|---|---|
| `VERONICA_PROVIDER` | `claude` |
| `VERONICA_CLAUDE_MODEL` | optional; unset is `sonnet` |
| `VERONICA_CLAUDE_EFFORT` | optional; unset passes no effort and the CLI's default applies |

The endpoint's port is `VERONICA_MCP_PORT` (`McpEndpoint.md`).

## Turns

- Each turn starts one `claude -p` process. The first turn of a conversation passes a new `--session-id`, later turns `--resume` it. `/clear` starts a new session id.
- The user message goes on stdin.
- The working directory is `%LOCALAPPDATA%\Veronica\Claude`. It holds the system prompt file and the MCP config each turn passes.
- Claude Code keeps the conversation. Veronica's transcript mirrors what the stream reports, for the chat and for eval runs (`Eval.md`); it is not sent back to Claude.
- The round limit is `--max-turns 100`. Reaching it ends the turn with the same "continue" notice as the other providers. The other turn limits (free rounds, rewards, nudges, thinking, max tokens) do not apply.
- Stop kills the process tree. A tool that is running gets cancelled. The next turn resumes normally: Claude sees that its last call was interrupted.
- The provider holds no VRAM.

### Stream

The output is `--output-format stream-json --verbose --include-partial-messages`, mapped to `ITurnHost`:

| stream | host |
|---|---|
| `message_start` | round start |
| text and thinking deltas | text and reasoning deltas |
| complete assistant message | assistant transcript message, with its tool calls |
| tool result | tool transcript message |
| usage | round stats |

- Round stats come from each model message's usage: prompt tokens are the newly processed ones, cached tokens the ones read from Claude's prompt cache. The result's top-level usage sums every model call in the turn and is not used.
- Speeds are measured by Veronica from the stream's timing, so they include network and Claude Code overhead.
- Claude Code runs a tool before the stream reports the end of the message that called it. The assistant message enters the transcript before the tool's result.
- A tool call left without a result when the turn ends (Stop, or Claude Code exiting) gets the same "stopped by the user" result as in the other providers.

## System prompt

- `--system-prompt-file` replaces Claude Code's own system prompt with Veronica's. Claude Code's prompt is written for a coding agent and adds nothing Veronica needs; tool definitions reach the model separately from the system prompt.

## Isolation

Claude sees Veronica's tools and nothing else from the user's Claude Code setup.

| flag | removes |
|---|---|
| `--tools ""` | built-in tools: shell, files, web, subagents, Claude Code's skills |
| `--strict-mcp-config` with only Veronica's endpoint | the user's MCP servers and connected services |
| `--setting-sources ""` | the user's and project settings: hooks, permissions, CLAUDE.md files |
| `--settings {"autoMemoryEnabled":false}` | Claude Code's auto-memory |
| `--permission-mode dontAsk` with `--allowedTools mcp__veronica__*` | any permission prompt: whatever is not Veronica's is refused |

- Claude Code's MCP tool timeout is lifted: the user's Stop is the timeout, and some Editor tools take minutes.
- Claude Code's tool search is off: tools hidden behind a search would need instructions that only its own system prompt carries.
- Claude Code still adds the signed-in account's email address to the first message. No flag removes it.
- `--bare` would isolate in one flag but refuses subscription sign-in. `--safe-mode` also drops the MCP servers given on the command line.

## Tool calls

- The tools reach Claude through the MCP endpoint (`McpEndpoint.md`), which the provider needs: it starts when the panel is created. Each turn passes its URL in the MCP config.
- During a turn the provider routes every endpoint call into the turn, whichever client sent it: it shows in the chat and obeys Stop. The transcript holds only the calls Claude reports in its stream.
- A call from Claude carries the id of its tool call in the stream: the provider matches the call by tool name and arguments, so the chat, the transcript and eval timings agree.
- A call that arrived during a turn and was still waiting when the turn ended does not run.
- Between turns a call runs directly, like with any provider.

## Testing

- No unit tests: the provider is thin glue over Claude Code, and a unit test may not start Claude Code.
- They are tested by hand: a chat in the Editor with tool calls, Stop during a tool call and the turn after it, `/clear`, and a call from another MCP client between turns.
