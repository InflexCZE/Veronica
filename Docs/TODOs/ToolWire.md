# Tools over the WebQwen wire

**Exists:** the tool loop (`Source/Veronica.Agent/ClientToolLoop.cs`) and the message types carry tool definitions, tool calls and tool results (`Docs/Tech/Agent.md`). The engine bundle renders tools into the chat template and parses tool calls itself, returning OpenAI-style `tool_calls` deltas.

**Missing:** carrying tools end to end, with the first real tool so it can be tested:
- The protocol (`Source/WebQwen/Protocol/Protocol.cs`): tools in `chat`, tool-call deltas, assistant messages with tool calls, `tool` role messages with `tool_call_id`.
- The C API (`wq.h`, `Engine.cs`, `Engine.cpp`) and `api.js`: the same fields through to web-llm's request.
- `WebQwenConnection` and `WebQwenProvider` reject tools and tool messages today (`NotSupportedException`).
- Calls the engine cannot parse arrive with an empty name and the raw text as arguments. The tool layer must answer them with an error result that asks the model to write the call again.
