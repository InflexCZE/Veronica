# Chat panel: smaller gaps

**Exists:** the chat panel as `Docs/Spec/Chat.md` describes it: `ChatTimeline` turns the turn's callbacks into items, `MarkupView` renders model text.

**Missing:**
- Streaming still costs a UI post, a string copy and a reparse of the whole reply per token, and rebuilds the last paragraph's controls each time. A long paragraph gets slow, and text selected in it is lost while it streams. Coalescing deltas (one update per frame) would fix both.
- The strip above the input shows only the latest provider status, so "Veronica's tools could not start" disappears once the local model reports ready.
- Live timers (thinking, subagent card) tick only while a turn runs. No subagent runs outside a turn today; one that did would show a frozen elapsed time.

**Start at:** `ChatPanelViewModel.UiHost` (posting), `ChatPanelViewModel.ShowStatus`, `ChatTimeline.Tick`.
