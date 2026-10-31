# Agent: providers, tool loop, conversation

`Veronica.Agent.dll` holds everything between the chat panel and a model: the provider abstraction, the tool loop, the conversation and the WebQwen client. It has no Editor references, so its tests run without the Editor. `Veronica.dll` references it and ships it beside itself.

## Providers

- A provider opens sessions. A session runs one whole assistant turn, tool rounds included: `IProviderSession.RunTurnAsync(TurnRequest, ITurnHost, CancellationToken)`.
- A session is one conversation's state; `OpenSession(name)` names whose: the chat's, or a kind of subagent's (`Wiki.md`, Subagents). `RunsSubagents` says whether a second session may run while the chat's turn waits in a tool call.
- A provider states its context window. Every provider assumes 45K tokens for now (`Docs/TODOs/ContextWindow.md`).
- The chat sees only `ITurnHost` callbacks, in model order: round start, text and reasoning deltas, round stats, tool calls, notices, transcript appends. Every provider drives the same host.
- Providers whose wire protocol is one model request per round (WebQwen) implement a `StreamRound` function. `ClientToolLoop` wraps it into a session.
- Providers that run their own loop (`claude -p`, `ClaudeProvider.md`) implement `RunTurnAsync` directly.

## Tool loop

`ClientToolLoop` runs rounds: stream a reply, run its tool calls, stream again.

- Each round sends the system prompt followed by the transcript. The system prompt is not part of the transcript.
- Tool-call fragments merge by index: each fragment carries part of the id, name or arguments.
- Each round appends the assistant message (content, reasoning, tool calls) to the transcript, followed by one tool message per call, in order.
- A round without tool calls ends the turn, except for the nudges below.
- **Round budget:** `MaxRounds` (default 20) rounds per turn. Rounds that call only `FreeRoundTools` are not counted. A hard cap of three times `MaxRounds` bounds every turn. When the budget runs out, the turn ends with a notice that tells the user to send "continue". The last round's tools are told it is the last.
- **Reward:** a tool result with `EarnedReward` refunds half the budget. The model is never told.
- **Length nudge:** a round that hits the token cap with no answer text (cut off while thinking) gets an automatic user message and another round, up to `MaxNudges`. A round cut off mid-answer is not nudged, since the model would start its answer over.
- **Cut tool call:** a round that hits the token cap while writing a tool call keeps that call in the history with `{}` as its arguments, does not run it, and answers it with a result saying it was cut off, to send it again with fewer calls per reply. The round's complete calls run and the turn goes on. Kept as it was, the half-written arguments made the server refuse every later request of the conversation.
- **End nudge:** when the model ends its turn, a callback can send it back with a message, once. It re-arms only after a tool call outside `BookkeepingTools`.
- **Turn-ending tools:** a round that calls one of `TurnEndingTools` runs all its calls, in order, then ends the turn: no further round, no end nudge, and no budget notice even on the last round, since the user's answer starts a fresh budget. It lets the model stop on purpose through a tool call, the way small models act. A round with no text of its own does not end through `wait_for_user`: the tool's result is replaced by one saying nothing was written for the user and the question belongs in the same reply, and the loop goes on. Only the waiting round's text counts, since models narrate their work in earlier rounds; a wait with no question leaves the user nothing to answer. A round that also calls `ask_user` ends without text: the card is what the user answers.
- Nudges count against the round budget.
- **Stop:** a stopped round keeps what it generated. Its tool calls are not run: each gets a "stopped by the user" result, so the transcript never carries dangling calls. A tool that returns null (stopped while it ran) also ends the turn this way, for every call left in the round.
- A failed round appends nothing and fails the turn. There are no retries.

## Tools

- A tool is an `ITool`: name, description, JSON Schema parameters, and `ExecuteAsync` over the parsed arguments.
- `ToolSet` holds a conversation's tools. It supplies their definitions to every turn and runs each call by name.
- Every failure becomes a tool result the model can act on, never an exception:
  - a call the engine could not parse (empty name, raw text as arguments) asks the model to write the call again;
  - an unknown tool lists the tools there are;
  - arguments that are not a JSON object;
  - an exception thrown by the tool.
- Tools run on a worker thread, one at a time, whichever source calls them: the chat's turn or an MCP client (`McpEndpoint.md`). Concurrent tools are never wanted; a call waits for the running one. A blocking `ask_user` waits outside this queue (Questions to the user).
- The tools are the Editor tools in `EditorTools.md`. Their descriptions say only what the model would not guess; the system prompt carries no tool guidance.

## Questions to the user

- `ask_user(questions)` asks the user questions, each with optional short answers to pick. It is for every question to the user; `wait_for_user` is left for steps the user does in the Editor (`Skills.md`).
- The questions travel as arguments, so the user always sees them whole. A question written as reply text before a wait was sometimes cut to its lead-in.
- The schema says 1-4 questions. It is guidance: more are shown, never dropped, since the model would wait for answers it never gets.
- The panel shows the questions as a card (`EditorUi.md`). The user picks an option, types an answer or skips, per question. The answers reach the model as one `Q: <question>` / `A: <answer>` pair per question, `(skipped)` for one without an answer.
- The `ask_user` calls of one round merge into one card.
- The description says the tool waits for the answers by itself, so the model neither ends its turn nor calls `wait_for_user` after it.
- A call with no usable question gets a result asking to send it again, and the turn goes on.
- **Client tool loop:** `ask_user` is a turn-ending tool. Its result says the questions are shown; the answers are the next user message, which starts a turn with a fresh budget. The turn ends instead of waiting so that the Editor's VRAM is free (`VramSharing.md`) while the user answers or works in the Editor. Closing the card sends nothing: the user's next message is what the model sees. A message sent while the card is open closes it unanswered.
- **Over MCP** (the Claude provider and any other client): the call blocks until the card is answered or closed, and the answers are its result. A closed card's result says the user closed the questions without answering. The wait runs outside the one-at-a-time tool queue, so other calls run while the card is open.
- `/clear` and Stop close an open card. A blocked call whose wait is cancelled otherwise (its Claude turn ended) takes back only its own questions.
- Answers given while the asking turn still winds down are sent as soon as it ends; questions asked after them stay open. The eval channel never reports idle in between.
- A call the tool set could not run (arguments not JSON, an exception) is refused like a malformed one, so a turn-ending tool does not end the turn on it.

## Conversation

- `Conversation` holds the transcript and the session. It sends one turn at a time: the user message is appended, then the turn's messages as the loop reports them.
- The transcript lives in memory only and is lost when the Editor exits. Eval runs also record every session to a file (`Eval.md`).
- `/clear` empties it and resets the session.
- Assistant messages keep their reasoning. Thinking models need their own earlier reasoning: their chat template otherwise renders an empty think block, and the model stops closing `</think>`. WebQwen also reuses its KV cache only when every earlier reply comes back exactly as it was generated.

## Chat settings

| setting | value |
|---|---|
| system prompt | who Veronica is and that `<harness>` messages come from the Editor and the user has not seen them, then a "How we work" section and the skill index (`Skills.md`). The section is the procedure in four steps: understand the task and ask the user for what the project lacks (a question about how something works, what it takes or what went wrong is answered, not built), read the skills that may cover it, once the user wants the work done activate the one that fits best before asking the user anything (a small model that asked first often never activated), then work its current item, asking the user for what that item needs from them (their choices, such as names players see or a folder, with `ask_user`; steps they do in the Editor, described in the reply, with `wait_for_user`), and mark it with proof or a reason. Models told to work the item "only, yourself" went straight to work and invented the user's choices. The step also says a new thing gets its own name and text from the user even when a similar one exists, that a skill's example folder is a suggestion to offer, and to ask when unsure: left to judge, a model argued itself out of asking, reused a block's name for its new tile and picked folders silently. Small models skipped activation or worked from memory when the procedure was spread over separate hints. A paragraph after the procedure says when to ask the wiki and that values of game things come from a read or the wiki, not memory (`Wiki.md`). Without skills, only the ask-the-user rule is left. |
| thinking | on |
| rounds per turn | 50, until tool rewards exist (`Docs/TODOs/ToolRewards.md`) |
| max tokens per round | 4000 for the answer and tool calls. With the OpenAI provider, thinking has its own budget of 4000 on top (`thinking_budget_tokens`, llama.cpp; `max_tokens` is the sum): a reply of 30 writes after long thinking hit a shared 4000 cap mid-call. The server closes the thinking at the budget and the model still answers. OpenAI's own API refuses unknown request fields, so it would refuse this one. |
| length nudge | up to 2 per turn |
| tools | the Editor tools (`EditorTools.md`), `skill` and the plan tools, `wait_for_user` among them (`Skills.md`), `wiki` (`Wiki.md`), `ask_user` |
| free rounds | rounds that call only `skill`, the plan tools and `wiki` |
| end nudge | when an item of the active skill's plan is open (`Skills.md`, Plan); `skill` and the plan tools are its bookkeeping tools |
| turn-ending tools | `ask_user`, `wait_for_user`. The Claude provider gets them over MCP like every tool, but its own loop decides when a turn ends |

## WebQwen provider

- WebQwen.exe starts with the first round of the first turn and stays up. See `WebQwenProtocol.md`.
- Stop while WebQwen.exe is starting or loading the model ends the turn, and the load continues. The user's message stays in the transcript.
- When WebQwen.exe exits or breaks the protocol, the running round fails, the turn shows the error, and the next round starts a new process. A failed request (an `error` event with an id) fails the round but keeps the process.
- The status line shows the start, the load progress, ready, and each round's prompt and decode speed.
- The provider holds the Editor's VRAM (`IGpuShare`, `VramSharing.md`) for each whole turn and for each model load until it completes. `GpuShare` counts the holds: the first one frees the Editor's VRAM, the last one gives it back. A hold that cannot free the VRAM fails the turn.

## OpenAI-compatible provider

- Any server that speaks OpenAI chat completions; the target is a local llama.cpp server. It uses one streamed request per round, run by `ClientToolLoop`.
- It is selected by environment variables; there is no UI yet (`Docs/TODOs/ProviderSelection.md`). Without them, WebQwen is the provider.

| variable | value |
|---|---|
| `VERONICA_PROVIDER` | `openai` |
| `VERONICA_OPENAI_URL` | base URL including the API prefix, e.g. `http://localhost:8080/v1` |
| `VERONICA_OPENAI_MODEL` | optional; unset takes the first model the server lists |
| `VERONICA_OPENAI_API_KEY` | optional bearer token |

- The request body is plain chat completions plus `max_tokens`, which llama.cpp takes and OpenAI's own reasoning models reject. Thinking stays at the server's default, which is on for thinking models.
- Reasoning is read from `reasoning_content` or `reasoning`. Each assistant message sends its reasoning back as `reasoning_content` (see Conversation).
- Round stats come from llama.cpp's `timings`; other servers show none.
- Any server certificate is accepted: locally hosted servers are typically self-signed.
- The Editor keeps its VRAM: the server may be remote or on another GPU.

## Testing

- `Veronica.Tests` covers `ClientToolLoop` with scripted rounds, and the editing layer behind the Editor tools (see `EditorTools.md`).
- It also runs WebQwen.exe's `Server` and the plugin's `WebQwenConnection` against each other over in-memory pipes, with a scripted engine in place of the model.
- The OpenAI-compatible provider runs against a scripted HTTP handler.
- Process lifetime and the live model are tested by hand in the Editor.
