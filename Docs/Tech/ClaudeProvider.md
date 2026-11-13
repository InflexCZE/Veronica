# Claude provider

Each turn runs through the Claude Code CLI, `claude -p`, which runs its own tool loop. Veronica's tools reach it through the MCP endpoint (`McpEndpoint.md`). The user installs and signs in to Claude Code; Veronica does not.

## Turns

- One `claude -p` process per turn. Claude Code keeps the conversation: the first turn creates a session id, later turns resume it, and clearing the chat starts a new id.
- Veronica's transcript mirrors what the stream reports, for the chat and for eval runs; it is never sent back to Claude.
- The stream-json output maps onto the same turn host callbacks every provider drives (`Agent.md`). Round stats come from each model message's usage, since the result's total sums the whole turn. Speeds are measured from the stream, so they include network and Claude Code overhead.
- Claude Code runs a tool before it reports the end of the message that called it; the assistant message is still entered into the transcript before the tool's result.
- The round limit is Claude Code's `--max-turns`, set high (100). Veronica's other turn limits (free rounds, nudges, rewards, token caps) do not apply. Because the limit counts inside one process, an answered question does not reset it as a new turn does in the client loop; the margin makes that acceptable.
- Stop kills the process tree. The next turn resumes normally, and Claude sees that its last call was interrupted.
- The provider holds no VRAM and runs no subagents, so Claude reads wiki pages itself over MCP instead of asking the researcher (`Wiki.md`, Subagents).

## System prompt

Veronica's system prompt replaces Claude Code's own. Claude Code's prompt is written for a coding agent and adds nothing Veronica needs; tool definitions reach the model separately.

## Isolation

Claude sees Veronica's tools and nothing else from the user's Claude Code setup: no built-in tools, no other MCP servers or connected services, no user or project settings (hooks, permissions, CLAUDE.md), no auto-memory, and no permission prompts, since everything that is not Veronica's is refused.

- The single-flag isolation mode is not used: it refuses subscription sign-in. Safe mode is not used either: it also drops MCP servers given on the command line.
- The MCP tool timeout is lifted: some Editor tools take minutes, and the user's Stop is the timeout.
- Tool search is off: tools hidden behind a search need instructions that only Claude Code's own system prompt carries.
- `wait_for_user` is not offered: it only ends a turn, and Claude Code's own loop decides that. It is left out of the MCP endpoint for every client, and out of the chat's tools while Claude is the provider, so a call by name finds no such tool.
- Claude Code still adds the signed-in account's email address to the first message; no flag removes it.

## Tool calls

- The endpoint is required: it starts with the panel, and each turn passes its URL in the MCP config.
- During a turn every endpoint call runs inside the turn, whichever client sent it: it shows in the chat and obeys Stop. Each call is matched to the tool call Claude streamed for it, so the chat, the transcript and eval timings share one id.
- A call still waiting when the turn ends does not run. Between turns a call runs directly, as with any provider.
- `ask_user` blocks until the question card is answered or closed and returns the answers, so Claude's loop keeps its turn (`Agent.md`, Questions to the user).
