# Full context window

**Exists:** a conversation grows without bound. When the prompt or the reply no longer fits in the model's context window, the engine fails the request, and the chat shows the error. The only way on is `/clear`.

**Missing (needs design with the owner):** what happens when the context is full, for example compacting the transcript into a summary and retrying the turn once. It also needs a typed error from WebQwen that tells a full context apart from other failures.
