# read of a block part: header does not name the block

**Exists:** a read of a block's `_Client` or `_Server` prefab or its composition opens the whole bound block and shows it.

**Missing:** the reply's header does not say that the file is part of block X and that the whole block is shown. The model takes the block view as a misresolved name and reads again:
- Bonsai MyLight `.tmp/eval/runs/20261107-080538-MyLight` l34-39: three reads of `CornerLight50_Client`.
- Bonsai MyTank `.tmp/eval/runs/20261107-133638-MyTank` l18-19: the binding read twice.
- Bonsai MyWindTurbine `.tmp/eval/runs/20261107-115746-MyWindTurbine` l11-19: the template binding read three times.

**Options:** a header line naming the block and saying the whole block is shown; or show only the part that was asked for.

Observe how often it recurs before acting: every run recovered, at a cost of a few reads.
