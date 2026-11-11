# Agent

`Veronica.Agent.dll` holds everything between the chat panel and a model: the provider abstraction, the tool loop, the tool set, the conversation and questions to the user. It has no Editor or SE2 references, so it runs and is tested without the Editor. The plugin ships it beside itself; the Launcher shares its settings and release code.

## Providers

- A provider opens sessions; a session runs one whole assistant turn, tool rounds included. A session is one conversation's state: the chat holds one for its lifetime, a subagent opens its own, runs one turn and disposes it, so two conversations never see each other's state.
- A provider says whether it runs subagents: whether a second session may run a turn while the chat's turn waits inside a tool call. Wiki research and compaction exist only with providers that do.
- A provider states its context window. It is assumed, not asked of the server ([ContextWindow](../TODOs/ContextWindow.md)).
- The chat sees a turn only through host callbacks in model order: round start, text and reasoning deltas, round stats, tool calls, notices, transcript appends. Every provider drives the same host, so the panel and eval recording do not depend on the provider.
- Two provider shapes:
  - one model request per round on the wire (WebQwen, OpenAI-compatible): the provider streams a round and the shared client tool loop turns it into a session;
  - a provider that runs its own loop (Claude Code) implements the turn directly and reaches the tools over MCP.

## Client tool loop

Rounds: stream a reply, run its tool calls, stream again. Each round sends the system prompt followed by the transcript; the system prompt is not part of the transcript.

- **Round budget:** rounds that call only free-round tools (skill, plan, wiki) are free. A hard cap of three times the budget bounds every turn, free rounds included. When the budget runs out the turn ends with a notice that the user can send "continue"; the last round's tools are told it is the last. Nudges count against the budget.
- **Reward:** a tool result that earns a reward refunds half the budget, and the model is never told. No tool earns one yet ([ToolRewards](../TODOs/ToolRewards.md)).
- **Length nudge:** a round cut at its token cap with no answer text (cut while thinking) gets an automatic user message and another round. A round cut mid-answer is not nudged: the model would start its answer over.
- **Cut tool call:** a round cut while writing a tool call keeps that call in the history with empty arguments, does not run it, and answers it as cut off. The round's complete calls run and the turn goes on. Half-written arguments kept in the history made the server refuse every later request of the conversation.
- **End nudge:** when the model ends its turn, a callback may send it back once, for an open plan item. It re-arms only after a call to a tool outside its bookkeeping set, the skill and plan tools; a `wiki` call re-arms it.
- **Turn-ending tools** (`ask_user`, `wait_for_user`): a round that calls one runs all its calls, then ends the turn with no further round, end nudge or budget notice. Small models stop on purpose through a tool call more reliably than by ending a reply.
  - A wait in a round with no text of its own is refused, and the loop goes on. Models narrate in earlier rounds, so only the waiting round's text can be the question; a wait with nothing written leaves the user nothing to answer. A call to `ask_user` counts as the round's text, since its card is what the user answers.
  - A refused call never ends the turn.
- **Stop:** a stopped round keeps what it generated. Its tool calls are not run and each gets a stopped result, so the transcript never carries a dangling call into the next turn. A tool stopped while it ran ends the turn the same way for every call left in the round.
- **Failure:** a failed round appends nothing and fails the turn. There are no retries.
- **Context wall:** a reply that ran into the end of the context window, or a prompt the server refuses as too long, fails the turn with a context-full error before any nudge or cut-call handling: it is not a token-cap cut. The conversation compacts on it ([Compaction](Compaction.md)).

## Tool set

- A tool has a name, a description, a JSON Schema for its parameters, and runs on the parsed arguments. Descriptions say only what the model would not guess; the system prompt carries no tool guidance.
- Every failure becomes a result the model can act on, never an exception: an unparsable call, an unknown tool (the result lists the real ones), arguments that are not a JSON object, an exception in the tool. Such results are marked refused, so a turn-ending tool does not end the turn on them.
- Each result carries a one-line summary for the chat; the model never sees it.
- Tools run one at a time on a worker thread, whoever calls: the chat's turn, an MCP client, or both at once ([McpEndpoint](McpEndpoint.md)). Concurrent tools are never wanted. A tool that waits on the user runs outside this queue, so it does not stall other callers.

## Questions to the user

- `ask_user` carries every question to the user, each with optional short answers to pick; `wait_for_user` is left for steps the user does in the Editor ([Skills](Skills.md)).
- The questions travel as arguments, so the user always sees them whole: a question written as reply text before a wait was sometimes cut to its lead-in.
- The 1-4 limit in the schema is guidance: more questions are shown, never dropped. A call with any unreadable entry is refused whole and asked again, because a dropped question would be waited for forever. The `ask_user` calls of one round merge into one card.
- **Client tool loop:** `ask_user` ends the turn, and the answers are the user's next message, which starts a turn with a fresh budget. The turn ends instead of waiting so the Editor gets its VRAM back while the user answers or works in the Editor ([VramSharing](VramSharing.md)).
- **Over MCP:** the call blocks until the card is answered or closed, and the answers are its result. The wait runs outside the tool queue.
- A blocked call whose wait is cancelled otherwise (its Claude Code turn ended) takes back only its own questions.
- Answers given while the asking turn still winds down are sent as soon as it ends.

## Conversation

- A conversation holds the chat's transcript and its provider session, and sends one turn at a time.
- The transcript lives in memory only; eval runs also record every session to a file ([Eval](Eval.md)).
- `/copy` puts the same recording on the clipboard, kept in memory since the last `/clear`: each session after a header line naming its eval file, so the paste splits back into the files the eval and replay tools read. It is a recording apart from the transcript because compaction replaces the transcript with its summary.
- Each recording tracks the tool call running in its own async-local slot: the eval recording and the `/copy` one wrap the same provider, and a shared slot would link a subagent's session to the other recording's call.
- `/clear` empties the transcript, resets the session and builds the system prompt anew, so edited skill files take effect without an Editor restart.
- Assistant messages keep their reasoning. A thinking model's chat template otherwise renders an empty think block for its earlier replies, after which the model stops closing `</think>`; and WebQwen reuses its KV cache only when every earlier reply comes back exactly as generated.

## Chat turn limits

| limit | value |
|---|---|
| thinking | on |
| round budget | 50, so a whole block build fits in one turn until tools earn rewards |
| tokens per round | 4000 for the answer and tool calls, plus a separate 4000 thinking budget where the provider supports one: under a shared cap, long thinking cut writes off mid-call |
| length nudges | 2 per turn |
| free rounds | rounds calling only `skill`, the plan tools and `wiki` |
| end nudge | while an item of the active plan is open ([Skills](Skills.md)) |
| turn-ending tools | `ask_user`, `wait_for_user`. Claude Code's own loop decides when a turn ends: `ask_user` blocks for the answers instead, `wait_for_user` is not offered over MCP ([ClaudeProvider](ClaudeProvider.md)) |
