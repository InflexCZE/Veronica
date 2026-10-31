# Board layout gaps

**Exists:** MSAGL lays out the graph left to right from the measured node and label sizes (`GraphLayout.cs`, `BoardView.axaml.cs`).

**Missing:**
- **Cycles:** an edge that goes back against the layers is drawn from the right side of its source to the left side of a node further left, across the nodes between them. A back edge could leave and enter on the other sides.
- **Late size changes:** a node whose thumbnail loads after the layout and changes its height can overlap a neighbour. Laying out again on size changes until the user drags a node would cover it.
- **Many unconnected nodes** stack in one column (twenty make one about 4400 px tall). Packing components side by side would fit the view better.
- **Cancelled drag:** a drag Nodify aborts puts the node back, but its edges stay where the drag left them until the node moves again. The edges follow `PreviewLocation`, which an abort does not reset; the container's `PreviewLocationChanged` event does report it.
