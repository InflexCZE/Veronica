# Veronica.dll and WebQwen.exe

`Veronica.dll` runs `WebQwen.exe` as a child process and talks to it over stdio.

## Process

- Command line: `WebQwen.exe serve --model <id> [--models <dir>] [--context <n>]`. The process loads the model at start; one process serves one model.
- **Job object:** the plugin puts the process in a job with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. The plugin never closes the job handle, so Windows closes it when the Editor exits, however the Editor exits, and kills WebQwen.exe with it.
- **Stdin EOF:** WebQwen.exe exits by itself when its stdin closes. This is the clean path: it interrupts the running chat, releases the engine and exits.
- **Shutdown:** the plugin closes WebQwen.exe's stdin when the Editor exits and when it replaces the process. It kills a process that has not exited 5 s later.

## Wire

- One JSON object per line, UTF-8 without BOM, discriminated by `type`.
- stdin carries commands and stdout carries events. stdout carries nothing else; WebQwen.exe writes its log to stderr, and the plugin forwards each line to the Editor log with a `[WebQwen]` prefix.
- The message types are defined once in `Source/WebQwen/Protocol/Protocol.cs`, which is compiled into both `WebQwen.exe` and `Veronica.Agent.dll`.
- The plugin and WebQwen.exe come from the same build, so the protocol has no version.

Commands:

```
{"type":"chat","id":7,"messages":[{"role":"system","content":"..."},{"role":"user","content":"hi"},{"role":"assistant","content":"..","reasoning":".."}],"maxTokens":4000,"thinking":true}
{"type":"interrupt","id":7}
```

Events:

```
{"type":"progress","progress":0.42,"text":"..."}
{"type":"ready"}
{"type":"delta","id":7,"channel":"reasoning","text":".."}
{"type":"delta","id":7,"channel":"content","text":".."}
{"type":"done","id":7,"finishReason":"stop","promptTokens":812,"completionTokens":95,"prefillTokensPerS":450.1,"decodeTokensPerS":39.8}
{"type":"error","id":7,"message":".."}
{"type":"error","message":".."}
```

## Behaviour

- `progress` events report the model load. `ready` follows a successful load.
- A failed load sends an `error` without an id, and WebQwen.exe exits with code 1. An `error` without an id is always fatal.
- `chat` carries the whole conversation each time. The engine reuses its KV cache for the prefix the conversation shares with the previous request, so only new messages are prefilled.
- An assistant message carries its reasoning (`reasoning`, the engine's `reasoning_content`), byte-identical to what the engine streamed. Without it the prefix stops matching at that message.
- A reply cut off inside its reasoning (interrupt, token cap) is re-rendered by the chat template with a closed think block, so the prefix stops matching there and the rest is prefilled again.
- A `chat` sent before `ready` waits for the load.
- One chat runs at a time. A `chat` while another runs gets `error` `busy`; the plugin never sends one.
- `interrupt` stops the chat with that id. The chat then ends with `done`, keeping what it generated, with `finishReason` `abort`.
- `maxTokens` <= 0 means no limit. `thinking` maps to the engine's `enable_thinking`.
- A line that cannot be decoded is logged to stderr and ignored.
- Token rates the engine did not measure are sent as 0.
