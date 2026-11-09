# Chat

The modder talks to the assistant in a panel inside the Editor. The assistant answers questions, does modding work in the open project and asks the modder for what it cannot decide alone.

## Conversation

- One conversation per Editor session. It lives in memory and is lost when the Editor exits.
- Clearing the chat starts a new conversation. It also picks up skill and wiki files edited on disk, without an Editor restart.
- The panel can always be reopened from the Editor's menu; its dock position is remembered.
- Any script and emoji are safe in chat text.

## Turns

- One block per assistant turn: a live line of the model's thinking (expandable), one plain-language line per tool call (expandable to what the model got, marked when it failed), the answer in a small Markdown subset, and a footer with speed, tokens and context use.
- A wiki lookup or other subagent shows as its own card with its steps and elapsed time.
- Enter sends, Shift+Enter starts a new line. During a turn the send button becomes Stop; Enter never stops.
- Stop keeps what the model already wrote. Tool calls not yet run are not run, and the next turn knows it was interrupted.
- A turn has a round budget. When it runs out the turn ends with a notice, and the modder resumes the work by asking to continue.
- A failed turn shows its error. A new message starts over cleanly.
- The view follows the stream unless the modder scrolled up.
- An empty chat offers suggestions to start from.

## Questions to the modder

- The assistant asks through a question card, not buried in reply text, so the modder always sees the whole question.
- One question at a time, with paging. Per question the modder picks an option, types an answer or skips. After the last one the answers show as the modder's message.
- Questions are never dropped, however many there are.
- A message typed while the card is open closes it unanswered. Stop and clearing close it too. During a Claude Code turn, which waits on the card, the send button stays Stop: the modder answers the card or stops the turn.
- An ambiguous request is acted on once the assistant has confirmed its reading with the modder, who can then steer.

## Long conversations

- When the context window fills, the conversation is summarized and the work goes on. The panel keeps the whole conversation visible, with the summary and a notice.
- When the context fills again without progress or input from the modder, the modder chooses: summarize again or clear.
- A failed summary offers only clearing and loses nothing. Stop during a summary leaves the conversation as it was.
- With the Claude Code provider, Claude manages its own context (`AiProviders.md`).

## Status

- A strip above the input shows the local model starting, loading and stopping, and hides once it is ready.
- A banner offers a newer Veronica when one exists (`Launcher.md`).

## Out of scope

- Conversation history across Editor sessions.

## Not yet

- Showing the active skill plan's progress in the panel ([PlanPanel](../TODOs/PlanPanel.md)).
