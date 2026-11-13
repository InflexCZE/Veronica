# Wiki

The wiki holds what and why: how the game and the Editor work (`Skills.md`). Only a small part of it bears on any one task, and modding has no fixed spec: the only context is the mod and the conversation. So the chat model never reads pages itself. It asks `wiki(question)`, a researcher subagent reads for it, and only the answer enters the chat's transcript.

## Pages

- One Markdown file per topic, with `name`, `title`, `source` and `description` frontmatter. The description is the only routing signal; pages do not link each other. `source` is provenance for whoever edits the page; the model never sees it.
- Pages share the skills' loader: a broken page is skipped and logged, and pages are read again on `/clear`. Like skills, the modder may edit and add them in the install folder.
- A page holds knowledge a skill needs only sometimes; what every run of a skill needs stays in the skill. Where the line falls is tuned in eval runs.
- Facts follow the rules of `Context.md`. ASCII only.

## The researcher

| part | holds |
|---|---|
| system prompt | that it researches for Veronica and does not work on the task, talk to the user or change the project; its sources; how to research; the answer's form; the page and skill indexes |
| brief, its only user message | the chat's transcript as plain text, the current plan item, the question |
| tools | `wiki_page`, `skill`, and the read-only project tools |

- The transcript is rendered as plain text in one message, not replayed as chat turns: in the model's own tool-call format, a transcript reads to a small model as a session in progress, and it carries on with the chat's task. Reasoning is left out and tool results are cut short, since the researcher can read again; earlier wiki answers stay whole so research is not repeated. The plan is harness state, so its current item is added.
- The researcher reads skills to foresee what the task will need but cannot activate them: its `skill` tool has a plan of its own that nothing reads.
- What a field means comes from a page or from `describe`, the game's own documentation; where neither says, it is reported as not stated. Left to infer, the researcher swept every vanilla definition of a kind to guess a meaning, and turned a page line into a stronger claim the chat model repeated to the user.
- The answer has three sections: Facts (as the sources state them, nothing stronger, no page names, since the chat model cannot read pages), Mod state (what it read in the project, with paths), Next step (only the one next step or skill). A wider look ahead made the chat model load skills and ask the user questions before they mattered. Skills are named, not copied.
- Reading is bounded: once the researcher's prompt grows past the context window less 10K tokens, its tool calls are not run and it is told to answer from what it has. A researcher that filled its context would end with no answer, and researchers do not compact. The size is the server's whole-prompt count where it reports one, else an estimate from the session's text, because WebQwen reports only newly prefilled tokens, not those served from its cache.
- After 16 tool calls the researcher is reminded once that it may answer if it already can. Eval research uses what it reads up to about 13 calls; over-research went well past 16. The reminder does not stop reading, so a researcher still missing a fact reads it.

## When the chat asks

- The system prompt says to ask when stuck, before a bigger task that needs knowledge of the game or data from the project, when the user asks why something behaves as it does, and before asking the user what a game term means. Without the last two, the model searched the project itself for a "what did I do wrong" question and asked the user what a game term meant.
- Reading is left open on purpose (`Context.md`): a model that knows where a value is reads it in one call instead of a research run.
- A skill step may say "ask the wiki about X" where the step needs it only sometimes.
- A `wiki` round is free for the chat, like `skill`: looking knowledge up must not cost the task its budget.

## Subagents

A subagent is one self-contained turn on a session of its own: its own system prompt, one brief as its only user message, its own tools and limits, and its last text as the result. Its transcript dies with the session. The researcher is one; the compaction summarizer is another (`Compaction.md`).

- A subagent opens its own provider session, runs one turn and disposes it, so two conversations never see each other's state. No server slot or KV cache is kept per session: on WebQwen, one process with one cache, a subagent's run evicts the chat's cached prompt and the chat's next round prefills it again.
- The chat supplies the runner and shows the run as its own card, with the subagent's tool calls and then its answer. A tool such as `wiki` only builds the request.
- The runner exists only for providers that can run a second session while the chat's turn waits in a tool call. The Claude provider cannot, since its turn is one `claude -p` reaching Veronica over MCP, so `wiki` says it is not available there.
- `wiki` is not served over MCP (`McpEndpoint.md`): the researcher reads the chat's transcript, which an MCP client is not part of. A client gets `list_wiki` (the page index) and `wiki_page` instead and reads pages itself; a large model's context holds the reading the researcher keeps out of a small one's.
- Eval runs record each subagent session to a file of its own (`Eval.md`).
