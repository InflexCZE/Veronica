# show during a content reload

**Seen once:** show called right after an edit ran while the Editor was still rebuilding the preview session; it failed inside the Editor's open call, and a second later the Editor raised a modal assert "Command buffer is already open on this thread" from Scene.Tick, which blocked the Editor. The game's Scene.Tick opens and closes its command buffer without a try/finally, so any exception in a tick leaves it open. Four other runs with the same edits did not hit it.

**Missing:** the inner exception was not captured then (it is logged now), so the root cause is unconfirmed. Candidate guard: show waits, bounded, until no asset processing or content push is running, with the idle check the eval validation already has. Decide when it recurs with its logged exception.
