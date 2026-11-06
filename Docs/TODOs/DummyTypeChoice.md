# Dummy types are chosen by name

**Exists:** each block skill restates the dummy types it needs (e.g. "UseConveyor for a port"), and the `dummies` wiki page lists the types and their rules. Nothing shared tells the model where the valid choices are kept.

**Problem:** a find listing of Assets/System/DummyTypes shows ConveyorBack, ConveyorLeft, ConveyorRight, ConveyorHole and UseConveyor side by side. Asked for a port on the back, the 27B wrote ConveyorBack instead of the skill's UseConveyor (MyGatling run 20261106-000528; a compaction summary suggested it first, and the model reasoned its way there again from the listing). ConveyorBack, ConveyorLeft and ConveyorRight are referenced by nothing in the game and make no port; the face of a port comes from the dummy's Transform. The conveyor network accepts only the types listed in `Assets/System/GridConveyorSystem.def` (PipeableDummies, InteractableDummies).

**Missing (needs design with the owner):** a shared place, such as a dummies skill, that every block skill goes to when it sets up dummies such as conveyor ports, and that tells the model which configuration holds the valid types so it reads them there instead of choosing by name. Open: its scope (conveyor ports only or every dummy), and whether the block skills hand over all their dummies at once.
