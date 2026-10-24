# Engine loop polls while the GPU works

**Needs an owner decision.**

**Exists:** `Engine::Loop` (`Source/WebQwen.Native/src/Engine.cpp`) calls `wgpuInstanceProcessEvents` in a tight loop with `std::this_thread::yield()` whenever a Dawn callback is outstanding (`Gpu::pending > 0`). Every decode step awaits `mapAsync` / `onSubmittedWorkDone`, so one CPU core sits at 100% for the whole generation. The startup loop that awaits module evaluation also spins.

**Options:**
- Keep polling: lowest latency, costs a core while generating.
- `wgpuInstanceWaitAny` with a timeout: needs the instance's timed-wait feature and futures from the callback-returning calls.
- Sleep a short interval between polls: simple, but Windows timer resolution (about 1-15 ms) may cost decode speed.

**Verify:** decode tok/s on Bonsai stays at about 40 on the RTX 3070 and CPU use drops; the logit gate still passes.
