# Wiki: background knowledge through a researcher

The wiki holds what and why: how the game and the Editor work (`Skills.md`, The split). Only a small part of it bears on any one task, and modding has no fixed spec: the only context is the mod and the conversation. So the chat model never reads pages itself. It asks `wiki(question)`, a researcher subagent reads for it, and only the answer enters the chat's transcript.

## Pages

- One Markdown file per topic in `Source/Veronica/Wiki`, with `name`, `title`, `source` and `description` frontmatter. The description routes questions to the page and is the only routing signal; pages do not link each other. `source` is provenance for whoever edits the page; the model never sees it.
- Skills and pages share the frontmatter parser and loader: a broken page is skipped and logged, and pages are read again on `/clear`.
- What goes in a page: knowledge a skill needs only sometimes. Knowledge every run of a skill needs stays in the skill; the rest moves to the wiki and is recalled when it is needed. Where the line falls is tuned in eval runs.
- Every fact is checked against the game source before it is added, under the rules of `Skills.md` (Context engineering).
- ASCII only.

## The researcher

`wiki(question)` runs a subagent (below) whose brief is the chat so far and the question.

| part | holds |
|---|---|
| system prompt | that it researches for Veronica and does not work on the task or talk to the user; its sources; how to research; the answer's form; the page index and the skill index |
| brief, its only user message | the chat's transcript rendered as plain text, the current plan item, the question |
| tools | `wiki_page(name)`, `skill(name)`, and the read-only project tools `find_content`, `list_folder`, `find_template`, `read` |

- The transcript is rendered as plain text inside one message: `[user]`, `[assistant]`, `[assistant used <tool>] <args>`, `[result of <tool>]`. Replayed as chat turns in the model's own tool-call format, a transcript reads to a small model as a session still in progress, and it carries on with the chat's task. Reasoning is left out. Tool results are cut to 1000 characters, since the researcher can read a file again; earlier wiki answers stay whole, so research is not repeated. The plan is harness state, not transcript, so its current item is added.
- The researcher reads skills to foresee what the task will need, but cannot activate them: its `skill` tool has a plan of its own that nothing reads.
- What a field means comes from a page. Where no page says, the researcher reports it as not stated: left to infer, it swept the definitions of every vanilla thruster to guess a field's meaning, and turned a page line into a stronger claim the chat model then repeated to the user.
- The answer has three sections: Facts (as the sources state them, nothing invented or stronger, no page names, since the chat model cannot read pages), Mod state (what it read in the project, with paths), Next step (only the one next step or skill). A wider look ahead would list skills the chat model then loads before it needs them, and questions it asks the user before they matter. Skills are named, not copied: the chat model reads them itself when it needs them.
- Reading is bounded: once the researcher's prompt grows past the context window less 10K tokens, its tool calls are not run and it is told to answer from what it has. A researcher that filled its context would end with no answer at all. The size is the server's whole-prompt count where it reports one (llama.cpp), else an estimate from the text the session holds, at three characters a token: WebQwen reports only the tokens it prefilled, not those served from its cache. Its round budget is 50.
- When the chat model asks: the system prompt says to ask when stuck, before a bigger task that needs knowledge of the game or data from the project, when the user asks why something behaves as it does, and before asking the user what a game term means. Without the last two, the model searched the project itself for a "what did I do wrong" question, at up to 22K of context, and asked the user what "terminal" meant. Values, units and sizes of game things come from a read or the wiki, not from memory: the model otherwise quoted values from other games. A skill step may also say "ask the wiki about X" where the step needs it only sometimes; none does yet.
- A question about how something works, what it takes or what went wrong is answered, not built: the system prompt puts activating a skill after the user wants the work done. Told to activate before asking anything, the model turned questions into plans.
- A `wiki` round is free for the chat, like `skill`: looking knowledge up must not cost the task its budget.
- In eval runs one direct question per need was enough; the model asks again for a follow-up rather than researching ahead. The build tasks rarely ask: their skills carry the steps, and the wiki answers what the skills leave out. What still makes the researcher sweep is a question of who references a definition, which no read-only tool answers (`Docs/TODOs/Wiki.md`).

## Subagents

A subagent is one self-contained turn on a session of its own: its own system prompt, one brief as its only user message, its own tools and limits, and its last text as the result. Its transcript dies with the session. The researcher is the first; a compaction agent would be another.

- A provider session is one conversation's worth of provider state. The chat holds one for its lifetime; a subagent opens its own with `OpenSession(name)`, runs one turn and disposes it, so two conversations never see each other's state. Keeping a server slot or KV cache per session is not done: on WebQwen, one process with one cache, a subagent's run evicts the chat's cached prompt, and the chat's next round prefills it again.
- The chat supplies the runner: it shows the run as a card of its own, with the subagent's tool calls as it works, then its answer. A tool such as `wiki` only builds the request.
- The runner exists only for providers that can run a second session while the chat's turn waits in a tool call (`RunsSubagents`). WebQwen and the OpenAI-compatible provider can. The Claude provider cannot: its turn is one `claude -p` reaching Veronica over MCP, so `wiki` says it is not available (`Docs/TODOs/ClaudeWiki.md`).
- `wiki` is not served over MCP (`McpEndpoint.md`): the researcher reads the chat's transcript, which an MCP client is not part of. The MCP tools still run one at a time with the chat's.
- Eval runs record each session to a file of its own (`Eval.md`).

## Testing

`Veronica.Tests` covers the page files, `wiki_page`, the transcript render, the subagent run (its brief, the reading bound, the round cap, stop), the `wiki` tool's request and its answers, and that every shipped page loads.
