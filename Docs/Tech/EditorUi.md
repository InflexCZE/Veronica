# Editor plugin: UI

## Panel

- The Editor docks panels with its own framework (UniDock). Its registration path for panels (view model registry, `[DockItem*]` attributes, Window menu, docking service) is internal to the Editor. A plugin can only use the public `DockItemViewModel` base and `DockManager.AddViewModel`.
- The panel is a `DockItemViewModel` with `Content` left null and a `ContentTemplateResourceKey`. Its `DataTemplate` is merged into the application resources, where the dock item finds it. This is the pattern of the Editor's own panels. A control placed in `Content` would be serialized into the saved layout.
- The template has no `x:DataType`: a typed template does not match the null `Content`. The view sets its own `DataContext` to the single panel view model, as the Editor's views do from their registry.
- A tabbed dock group builds the selected tab's view again on every tab switch. All panel state lives in the view model; a view subscribes to it only while attached to the visual tree.
- Layout persistence drops view models the Editor does not know, and Restore Layout replaces the whole set. The panel is therefore added again on every project load and from the menu, never restored from the layout.
- The saved layout is user-wide and keeps the Veronica tab itself. On the next start the panel reuses that tab, so its position is remembered. A project that does not load Veronica shows the tab empty; Veronica is assumed to be loaded in every project.
- The panel docks in `MainRightDockGroup`. If the user's layout has no such group, it goes to the first tabbed group in the layout, with a log line; a dock item whose group is missing is never shown.

## Chat

- Each reply shows its reasoning in a collapsible Thinking block above the answer. The block is expanded while the reasoning streams and collapses when the answer starts.
- During a turn the Send button becomes Stop. Enter only sends; it never stops a turn.
- `/clear` starts a new chat; a running turn is stopped first.
- A status line under the input shows the model's state and the last round's speed.
- Notices (round budget) and errors appear in the chat as plain lines.
- The view follows a streaming reply unless the user has scrolled up.

## Menu

The Window menu is built before plugins load and has no extension point. Veronica adds a top-level `Veronica` menu to the main menu bar with `Show Veronica`, which re-adds the panel if needed and selects it. This covers a closed tab and a restored layout.

## Fonts

- The Editor's theme declares its `Roboto` font with a relative URI and sets it on every `TextBlock`. Avalonia's glyph fallback throws on relative font URIs. Any character missing from Roboto and the system default font (emoji, CJK, many symbols) crashes the whole Editor during layout, whichever panel shows it.
- Every text control in Veronica's views gets an absolute or system font through local styles:
  - our UI text: Roboto through its full `avares://VRage.UI.Shared/...` URI, which looks native;
  - model text and user input: `Segoe UI, Segoe UI Emoji, Segoe UI Symbol`, for wide glyph coverage.
- Text Veronica writes to Editor surfaces outside its own views (the Console log) is not covered by these styles.

## Theme

Views use the Editor's Fluent controls and its shared colour and size resources (`PrimaryTextColor`, `PrimaryAccentDarkColor`, `MediumFontSize`, ...) through `DynamicResource`, so they follow the Editor's theme.
