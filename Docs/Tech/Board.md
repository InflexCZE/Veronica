# Board: showing content to the user

The tools that show content to the user, `show` and `present_graph`, and the Board panel that holds the graph. The shared tool contract is in `Tools_Overview.md`.

## show

- Does what a click and a double-click on the item do: selects it in the Project Explorer and the Property Inspector and opens it in the Render View. Selection and opening are internal to the Editor and reached by reflection.
- An item without a preview of its own opens through the asset that owns it (a prefab through its binding), so the model names any file of the thing.
- Items that open their own editor or an external app are only selected: opening them is the user's call. An item with no preview is only selected, and the reply gives the Editor's reason.

## present_graph

- Explains how content items relate by drawing them in the user's own visual language: each node is the item's Content Browser tile, so the user recognises it there afterwards. The model explains in text as well; the graph supports the answer.
- Node ids are short monikers the model picks, so it never numbers nodes.
- **Direction:** an edge always points from the referencing item to the referenced one. One rule a small model keeps.
- **Nodes are content items only**, resolved like any target: the model has no tool that reads source files, so links to them would be guesses.
- **Validation:** the whole call is refused and nothing reaches the user on any bad node or edge (an unresolved item, an unknown id, a duplicate item, a repeated or self edge, an overlong note). A wrong item on the Board would mislead the user; a refusal costs the model one round.
- **Size limits** of 25 nodes and 30 edges keep a model from dumping a whole dependency tree. The node refusal says how many to drop: a model told only the count and the limit sent the same nodes again.

## Board panel

- A second dock item beside the chat panel, docked in the Render View's group: a graph needs the main area and the chat panel is narrow. Without that group it docks where the chat panel would.
- Each graph replaces the previous one and brings the Board to front. The last graph stays until the next, across `/clear` and project reloads; there is no history (`Docs/TODOs/BoardStaleAfterReload.md`).
- **Canvas:** the Editor's own node editor control, shipped with the Editor and used by its Animation Controller and Behavior Tree, for pan, zoom, dragging and connections. It has no layout and no edge labels of its own.
- **Layout:** MSAGL computes node positions only, layered left to right with a referencing item left of what it references. Tiles with notes are taller than wide, so columns fit better than rows. Edges are the canvas's own connections, so they follow a dragged node; dragged positions are not kept.
- **Node:** the Editor's own Content Browser tile control bound to the item's explorer view model (reached by reflection), so thumbnails, overlays and markers stay live.
- **The tile takes no input:** its context menu acts on the Content Browser's selection, a double click on its name renames the real file, its clicks swallow the press, and F2 renames every visible tile of the same view model. The node's background takes the input instead: a click selects the item without opening it, so the Board stays in front; a double click opens it as `show` does.
- The view drops its tiles when the dock closes it, since the explorer's view models would keep them alive.
- Notes are model text and use the model-text font (`EditorPlugin.md`).
