# Editor hangs after load in eval runs

**Exists:** Wait-Ready reports Ready once the control channel answers; Get-Validation at start then finds whether the Editor serves. A run whose start check gets no answer is stopped and started again once.

**Missing:** the cause. Two of six Editor starts in batch C1 (`.tmp/eval/runs/20261104-013733-MyGatling`, `20261104-021227-MyCockpit`) answered `/status` but timed out on `/validation` and on MCP calls. Both Editor logs stop at "MainWindow Menu lost focus"; a good start logs "Pushing Startup Batch ... finished" about 16 s later, so the startup content push never finished and the main thread stopped serving. Possibly related to the `.startupblobscache` warning every load logs. Start in the Editor log of those runs and the startup push.
