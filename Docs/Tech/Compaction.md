# Compaction

The local model has a small context window (`WebQwen.md`), and a block build fills it. When the chat's conversation hits the end of the window, a subagent summarizes it, the summary replaces the whole transcript, and the turn goes on from it.

## When

- Only at the wall, never ahead of it: a window this small has no room to spare for compacting early. Nothing here depends on the provider's assumed window size.
- The wall comes in two forms, both reported by the provider as context full:
  - **Prompt:** the server refuses the request before the model writes anything. Each provider maps its server's refusal ([OpenAiProvider](OpenAiProvider.md), [WebQwen](WebQwen.md)).
  - **Reply:** the model runs into the end of the window while it writes. Servers end such a reply as `length`, like one that used its token cap; a `length` reply shorter than the `max_tokens` it was sent ran out of window. Without this check the cut reply was nudged and the wall was found only when the next request was refused, a round late; in eval runs the wall mostly came this way first.
- A reply cut by the wall appends nothing, like any failed round: its half-written thinking or call would only be stale context. A refused prompt keeps everything; its last tool results record what the actions did.
- Only the chat compacts, and only with providers that run subagents. Claude Code runs its own loop and context. The wiki researcher does not compact: it stops reading before its wall ([ResearcherContext](../TODOs/ResearcherContext.md)).

## The summarizer

A subagent on a session of its own ([Wiki](Wiki.md), Subagents): one round, no tools, thinking off.

- Its only user message is the chat's transcript rendered as plain text, then the open plans, framed as not part of the session. Replayed as chat turns, the transcript made a small model take it for a session in progress and make the next tool call instead of summarizing.
- The render leaves out reasoning and cuts long tool results, so it fits well inside the window the transcript itself just filled; wiki answers stay whole. The researcher uses the same render, so a change to it serves both.
- The open plans are every plan on the stack, outermost first, each from its current item on ([Skills](Skills.md), Plan). The transcript alone does not show which work is still open: a paused parent item reads like finished work once its nested skill is done, and a summary that dropped it lost the values the parent item later checked.
- The summary hands over what the open work still needs: the whole current task, every plan on the stack and the items still to come. It mainly keeps what that work builds on (names, values and paths earlier work produced, where a value came from and why, what was learned) and only lightly allows leaving out finished steps, since one line of history can keep the model from repeating a mistake. File contents are not copied: the model reads the project itself.
- `ask_user` answers are not carried over separately: they are user messages in the transcript like any other, and a carry-over of the cards alone would miss answers the user typed as a message.

## What the turn goes on from

The transcript becomes one user message, marked as coming from the Editor and unseen by the user, holding:

1. the summary;
2. the last wiki answer, verbatim: research costs a whole subagent run to redo, and a summary condenses away its exact names and values;
3. the active skill's overview, verbatim: the steps the model works from;
4. the active plan's current item.

- The system prompt and the tools are not in the transcript and stay as they were. The plan is harness state and outlives the compaction untouched.
- The record of which skills the model has read is reset to the active skill alone, the only overview the new transcript holds; a paused skill that resumes later comes with its overview again.
- The turn goes on with a fresh round budget.

## How often

- One compaction is allowed. The allowance comes back with the user's next message, an `ask_user` answer included, or once the model closed five plan items since the last compaction. A model working with the user or making progress on its plan is not a runaway; one that hits the wall again without either is stopped.
- A wall with no allowance left ends the turn and offers the user to compact again or clear the chat.
- A summary that fails (its own request hits the wall, the provider fails, or it comes back empty) ends the turn, offers only a clear, and leaves the conversation as it was. It is not retried. Stop during the summary does the same without the offer.
