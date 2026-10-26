# Agent: providers, tool loop, conversation

`Veronica.Agent.dll` holds everything between the chat panel and a model: the provider abstraction, the tool loop, the conversation and the WebQwen client. It has no Editor references, so its tests run without the Editor. `Veronica.dll` references it and ships it beside itself.

## Providers

- A provider opens sessions. A session runs one whole assistant turn, tool rounds included: `IProviderSession.RunTurnAsync(TurnRequest, ITurnHost, CancellationToken)`.
- The chat sees only `ITurnHost` callbacks, in model order: round start, text and reasoning deltas, round stats, tool calls, notices, transcript appends. Every provider drives the same host.
- Providers whose wire protocol is one model request per round (WebQwen) implement a `StreamRound` function. `ClientToolLoop` wraps it into a session.
- Providers that run their own loop (`claude -p`) implement `RunTurnAsync` directly.

## Tool loop

`ClientToolLoop` runs rounds: stream a reply, run its tool calls, stream again.

- Each round sends the system prompt followed by the transcript. The system prompt is not part of the transcript.
- Tool-call fragments merge by index: each fragment carries part of the id, name or arguments.
- Each round appends the assistant message (content, reasoning, tool calls) to the transcript, followed by one tool message per call, in order.
- A round without tool calls ends the turn, except for the nudges below.
- **Round budget:** `MaxRounds` (default 20) rounds per turn. Rounds that call only `FreeRoundTools` are not counted. A hard cap of three times `MaxRounds` bounds every turn. When the budget runs out, the turn ends with a notice that tells the user to send "continue". The last round's tools are told it is the last.
- **Reward:** a tool result with `EarnedReward` refunds half the budget. The model is never told.
- **Length nudge:** a round that hits the token cap with no answer text (cut off while thinking) gets an automatic user message and another round, up to `MaxNudges`. A round cut off mid-answer is not nudged, since the model would start its answer over.
- **End nudge:** when the model ends its turn, a callback can send it back with a message, once. It re-arms only after a tool call outside `BookkeepingTools`.
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
- Tools run on a worker thread.
- The tools are the Editor tools in `EditorTools.md`. Their descriptions say only what the model would not guess; the system prompt carries no tool guidance.

## Conversation

- `Conversation` holds the transcript and the session. It sends one turn at a time: the user message is appended, then the turn's messages as the loop reports them.
- The transcript lives in memory only and is lost when the Editor exits.
- `/clear` empties it and resets the session.
- Assistant messages keep their reasoning. Thinking models need their own earlier reasoning: their chat template otherwise renders an empty think block, and the model stops closing `</think>`. WebQwen also reuses its KV cache only when every earlier reply comes back exactly as it was generated.

## Chat settings

| setting | value |
|---|---|
| system prompt | "You are Veronica, a modding assistant inside the Space Engineers 2 Editor. Answer concisely." and "When a task needs a file or value you cannot find in the project, ask the user for it.", then the skill index (`Skills.md`) |
| thinking | on |
| rounds per turn | 50, until tool rewards exist (`Docs/TODOs/ToolRewards.md`) |
| max tokens per round | 4000 |
| length nudge | up to 2 per turn |
| tools | the Editor tools (`EditorTools.md`) and `skill` (`Skills.md`) |
| free rounds | rounds that call only `skill` |

## WebQwen provider

- WebQwen.exe starts with the first round of the first turn and stays up. See `WebQwenProtocol.md`.
- Stop while WebQwen.exe is starting or loading the model ends the turn, and the load continues. The user's message stays in the transcript.
- When WebQwen.exe exits or breaks the protocol, the running round fails, the turn shows the error, and the next round starts a new process. A failed request (an `error` event with an id) fails the round but keeps the process.
- The status line shows the start, the load progress, ready, and each round's prompt and decode speed.
- The provider holds the Editor's VRAM (`IGpuShare`, `VramSharing.md`) for each whole turn and for each model load until it completes. `GpuShare` counts the holds: the first one frees the Editor's VRAM, the last one gives it back. A hold that cannot free the VRAM fails the turn.

## Testing

- `Veronica.Tests` covers `ClientToolLoop` with scripted rounds, and the editing layer behind the Editor tools (see `EditorTools.md`).
- It also runs WebQwen.exe's `Server` and the plugin's `WebQwenConnection` against each other over in-memory pipes, with a scripted engine in place of the model.
- Process lifetime and the live model are tested by hand in the Editor.
