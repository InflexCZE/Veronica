# Editor plugin: UI

## Panel

- The Editor docks panels with its own framework (UniDock). Its registration path for panels (view model registry, `[DockItem*]` attributes, Window menu, docking service) is internal to the Editor. A plugin can only use the public `DockItemViewModel` base and `DockManager.AddViewModel`.
- The panel is a `DockItemViewModel` with `Content` left null and a `ContentTemplateResourceKey`. Its `DataTemplate` is merged into the application resources, where the dock item finds it. This is the pattern of the Editor's own panels. A control placed in `Content` would be serialized into the saved layout.
- The template has no `x:DataType`: a typed template does not match the null `Content`. The view sets its own `DataContext` to the single panel view model, as the Editor's views do from their registry.
- A tabbed dock group builds the selected tab's view again on every tab switch. All panel state lives in the view model; a view subscribes to it only while attached to the visual tree.
- Layout persistence drops view models the Editor does not know, and Restore Layout replaces the whole set. The panel is therefore added again on every project load and from the menu, never restored from the layout.
- The saved layout is user-wide and keeps the Veronica tab itself. On the next start the panel reuses that tab, so its position is remembered. A project that does not load Veronica shows the tab empty; Veronica is assumed to be loaded in every project.
- The panel docks in `MainRightDockGroup`. If the user's layout has no such group, it goes to the first tabbed group in the layout, with a log line; a dock item whose group is missing is never shown.

## Board

The panel that shows `present_graph` (`EditorTools.md`).

- A second `DockItemViewModel`, registered like the Veronica panel, docked in the Render View's group: a graph needs the main area, and the chat panel is narrow. Without that group it goes where the Veronica panel would.
- Each `present_graph` replaces the graph and brings the Board to front. It keeps the last graph until the next one, across `/clear` and project reloads; there is no history.
- **Canvas:** the Editor's own node editor control (Nodify.Avalonia, shipped with the Editor and used by its Animation Controller and Behavior Tree), for pan, zoom, node dragging and connections. It has no general layout and no edge labels.
- **Layout:** MSAGL (`AutomaticGraphLayout`) computes the node positions only, layered left to right: a referencing item left of what it references. Nodes have tiles with notes under them, taller than wide, so columns of tiles fit better than rows. Edges are the Editor's usual Nodify connections with an arrowhead at the referenced end, so they follow a dragged node. Dragged positions are not kept; the next graph is laid out fresh.
- **Edge labels** sit at the middle of their edge, as decorators on the canvas. Columns stand far enough apart for the widest label.
- **Node:** the item's Content Browser tile, the Editor's `ProjectExplorerFile` control bound to the item's own explorer view model, with the note under it. The tile brings the browser's thumbnail or type icon, its partial and template overlays, and its lock, warning and detached markers, all live. The view model comes from the explorer's internal file map, by reflection.
- **The tile is for show only and takes no input:**
  - its context menu's commands (Delete, Rename, Duplicate) act on the Content Browser's selection, not on the tile they were opened from;
  - a double click on its name renames the real file;
  - its own clicks swallow the press, so the node could neither be clicked nor dragged;
  - F2 and new items in the Content Browser start a rename or take focus on every visible tile of the same view model, so the Board's tile never renames or takes focus.
  The node's own background takes the input instead. The view drops its tiles when the dock closes it, since the explorer's view models would keep them alive.
- **Clicks:** the node handles them:
  - a click selects the item in the Project Explorer and Property Inspector, without opening it, so the Board stays in front;
  - a double click opens it in the Render View, as `show` does.
- Notes are model text and use the model-text font (see Fonts).

## Chat

- Each tool call shows as a dim line in the reply's bubble, `name arguments -> result`. The text after a tool call starts a new bubble.
- Each reply shows its reasoning in a collapsible Thinking block above the answer. The block is expanded while the reasoning streams and collapses when the answer starts.
- During a turn the Send button becomes Stop. Enter only sends; it never stops a turn.
- `/clear` starts a new chat; a running turn is stopped first.
- A status line under the input shows the model's state and the last round's speed.
- Notices (round budget) and errors appear in the chat as plain lines.
- The view follows a streaming reply unless the user has scrolled up.

## Question card

`ask_user`'s questions (`Agent.md`, Questions to the user) show as a card at the end of the chat.

- One question at a time. The header holds the question, `< n of N >` paging when there are several, and a close X.
- Each option is a full-width row of clickable text with a thin divider and a hover highlight, not a button. Clicking one answers the question and moves to the next.
- Below the options, a text field and one button: Skip while the field is empty, Next once something is typed, Answer on the last question. Enter does the same.
- After the last question the card goes away and the answers show as the user's message.
- While the card is open in a client-loop chat, the input sends as usual and the card closes unanswered. In a Claude turn blocked on the card, the button stays Stop.

## Menu

The Window menu is built before plugins load and has no extension point. Veronica adds a top-level `Veronica` menu to the main menu bar with `Show Veronica`, which re-adds the panel if needed and selects it. This covers a closed tab and a restored layout.

## Frozen viewport

- While the Editor is frozen for the local model (`VramSharing.md`), each visible viewport shows a small "Veronica is working..." badge in its bottom-right corner, dark grey with a lighter border so it stands out against the skybox. The last frame stays visible underneath.
- A viewport is a native child window, and Avalonia cannot draw over it. The badge is a `Popup`, which is a top-level window, as the Editor's own flyouts over the viewport are.
- The popup gets the viewport as its logical parent (`ISetLogicalParent`, as Avalonia's `ContextMenu` does). Without one, its window finds no theme and opens empty and zero-sized.
- A badge that fails to show is logged and never fails the freeze.

## Fonts

- The Editor's theme declares its `Roboto` font with a relative URI and sets it on every `TextBlock`. Avalonia's glyph fallback throws on relative font URIs. Any character missing from Roboto and the system default font (emoji, CJK, many symbols) crashes the whole Editor during layout, whichever panel shows it.
- Every text control in Veronica's views gets an absolute or system font through local styles:
  - our UI text: Roboto through its full `avares://VRage.UI.Shared/...` URI, which looks native;
  - model text and user input: `Segoe UI, Segoe UI Emoji, Segoe UI Symbol`, for wide glyph coverage.
- Text Veronica writes to Editor surfaces outside its own views (the Console log) is not covered by these styles.

## Theme

Views use the Editor's Fluent controls and its shared colour and size resources (`PrimaryTextColor`, `PrimaryAccentDarkColor`, `MediumFontSize`, ...) through `DynamicResource`, so they follow the Editor's theme.
