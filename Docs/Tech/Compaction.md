# Compaction: going on past a full context window

The target model runs locally with a small context window (about 25K to 56K tokens). A build task fills it: eval runs ended on the server refusing a prompt that no longer fit. When the chat's conversation hits the end of the window, a subagent summarizes it, the summary replaces the whole transcript, and the turn goes on from it.

## When

- Only at the wall, never ahead of it: a window this small has no room to spare for compacting early. The provider's context window stays an assumed size (`Docs/TODOs/ContextWindow.md`), and nothing here depends on it.
- The wall comes in two forms, both a `ContextFullException`:
  - **Prompt:** the server refuses the request before the model writes anything. llama.cpp answers 400 with the error type `exceed_context_size_error` (the message alone in builds without the type; OpenAI's own API uses the code `context_length_exceeded`). WebQwen.exe sends its `error` event with the code `context_full` when web-llm throws its `ContextWindowSizeExceededError` (`WebQwenProtocol.md`).
  - **Reply:** the model runs into the end of the window while it writes. Servers end such a reply as `length`, the same as one that used its token cap. A reply that ended as `length` with fewer tokens than the `max_tokens` it was sent ran out of window; one that used the whole cap is a normal length cut and is nudged as before (`Agent.md`, Tool loop). In eval runs the wall came this way first in most runs: the cut reply was nudged, and only the next request was refused, a round late.
- A reply cut by the wall appends nothing, like any failed round: its half-written thinking or call would only be stale context. A refused prompt keeps everything; its last tool results record what the actions did.
- Only the chat compacts, with the providers that run subagents (WebQwen, OpenAI-compatible). The Claude provider runs its own loop and context. The wiki researcher does not compact: it stops reading before its wall (`Wiki.md`); see `Docs/TODOs/ResearcherContext.md`.

## The summarizer

A subagent (`Wiki.md`, Subagents) on a session of its own, named `compaction`, shown in the chat as a card titled "Summarizing conversation" with the summary as its answer.

| part | holds |
|---|---|
| system prompt | that it compacts a session of Veronica with a user that ran out of context, that Veronica continues the task from the summary alone, the five sections, and that the carry-overs come separately |
| brief, its only user message | the chat's transcript rendered as plain text, then the open plans, between a preamble and a closing request that both say it is not part of the session, does not continue it and does not call tools |
| tools | none |
| limits | one round, thinking off, 4000 tokens |

- The transcript is rendered as for the researcher (`Wiki.md`): plain text, reasoning left out, tool results cut to 1000 characters, wiki answers whole. Replayed as chat turns, a small model took the transcript for a session in progress and made the next tool call instead of summarizing. The cut keeps the render well inside the window that the transcript itself just filled, since the system prompt, the tool definitions and the reasoning are not in it. The researcher and the summarizer share the render, so a change to it serves both.
- The open plans are every plan on the stack, outermost first, each from its current item on (`Skills.md`, Plan). The transcript alone does not show which work is still open: a paused parent item reads like finished work once its nested skill is done, and a summary that dropped it lost the values the parent item later checked.
- The summary hands over what the open work still needs. The open work is the whole current task: every item on the plan stack, paused plans included, and the items still to come. The prompt mainly asks to keep what that work builds on: names, values and paths earlier work produced, where a value came from and why, what was learned. It only lightly allows leaving out finished steps nothing open depends on: one line of history can keep the model from repeating a mistake, and the model is left to judge which.
- Sections: Current task (what the user asked, with later changes of direction), State (what is created or changed, with the values the open work relies on, and what is open), Learned (what reading the project again does not give back: how the game or the Editor behaved, names and values looked up and where they came from, approaches that failed and why), Next steps (through every open plan item to the end of the task), User decisions (the choices the remaining work still needs). File contents are not copied: the model reads the project itself.
- The summary captures what the user asked for. Questions answered through `ask_user` are not carried over on their own: their answers are user messages in the transcript like any other, and a carry-over of the cards alone would miss answers the user typed as a message.
- Thinking is off. The OpenAI-compatible provider sends `enable_thinking: false` to the chat template for it.

## What the turn goes on from

The transcript becomes one user message, marked as coming from the Editor (`<harness>`), which the user has not seen:

1. That the conversation ran out of context and was summarized, and the summary.
2. The last wiki answer that answered, verbatim. Research costs a whole subagent run to redo, and a summary condenses away its exact names and values.
3. The active skill's overview, verbatim: the steps the model works from.
4. The active plan's current item. The plan itself is harness state and outlives the compaction untouched (`Skills.md`, Plan).
5. That it continues a compacted session, with Next steps toward the Current task.

The system prompt and the tools are not in the transcript and stay as they were. The harness's record of which skills the model has read is reset to the active skill alone, the only overview the new transcript holds; a paused skill that resumes later comes with its overview again (`Skills.md`, Plan). The turn goes on with a fresh round budget. The chat panel keeps showing the whole conversation, with the summarizer's card and a notice that the context was full and the conversation was summarized.

## How often

- One compaction is allowed. The allowance comes back with the user's next message, an `ask_user` answer included, or once the model closed five plan items since the last compaction. A model working with the user, or making progress on its plan, is not a runaway; one that hits the wall again without either is stopped.
- A wall with no allowance left ends the turn with a card: the conversation no longer fits even after it was summarized, with Compact (summarize now and go on) and Clear (start a new chat).
- A summary that fails ends the turn with a card offering Clear only, and the conversation stays as it was. A summary fails when its own request hits the wall, when the provider fails, or when it comes back empty. It is not retried.
- Stop during the summary stops the turn; the conversation stays as it was.
- In eval runs nobody presses the card: the turn fails with its text (`Eval.md`).

## Testing

`Veronica.Tests` covers the typed errors of the OpenAI-compatible provider, the reply wall in the provider and the tool loop, thinking off, and the conversation's compaction: the summary brief with the open plans and what the turn goes on from, the read skills reset to the active one, the second wall, the allowance coming back by plan items and by the user's message, a failed or stopped summary, the Compact choice, and the last wiki answer. `WebQwen.Tests` covers the `context_full` error and the reply wall over the protocol. The live wall is tested in eval runs against a llama.cpp server started with a small context.
