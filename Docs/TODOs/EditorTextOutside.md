# Text outside Veronica's views

**Exists:** Veronica's own views set absolute or system fonts, so model text with emoji or CJK cannot trigger the Editor's font-fallback crash there (`Docs/Tech/EditorUi.md`, Fonts).

**Missing:** anything Veronica writes to Editor surfaces it does not style, such as the Console log or asset names it creates, still uses the Editor's relative-URI Roboto. A character outside Roboto and the system default font there crashes the Editor. Start where the first such write is added: sanitize the text, or keep model text out of those surfaces.
