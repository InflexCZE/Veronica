# A stalled reply stream waits forever

**Exists:** the OpenAI-compatible provider streams each round with no time limit (`Source/Veronica.Agent/OpenAi/OpenAiProvider.cs`): a round can generate for minutes, so the user's Stop is the timeout. Stop works on a stalled stream.

**Missing:** a stream that stops sending without closing is never noticed. Eval run 20261103-020916-FlameCopyQuestion (Bonsai, server through the KBird tunnel): a researcher round stopped mid-reply, the llama.cpp slot went idle, and the Editor stayed busy for 35 minutes until `/stop`. The Editor log shows no error, so the reply was lost between the server and the Editor. An unattended run, or a user who walked away, waits indefinitely.

Owner decision: whether to fail a round after a stretch with no streamed bytes (and how long, given that prompt processing of a long context also sends nothing for a while), or leave it to Stop.

**Start at:** `OpenAiProvider.StreamRoundAsync` and `ReadStreamAsync`, the read loop.
