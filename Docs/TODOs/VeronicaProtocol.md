# Veronica.dll and WebQwen.exe protocol

**Exists:** WebQwen.exe has only a dev CLI (`generate`, `Source/WebQwen/Program.cs`). The managed engine API (`Source/WebQwen/Engine.cs`) offers load, streamed chat (content and reasoning channels), interrupt, reset and scoring.

**Missing (needs design with the owner):** how Veronica.dll starts WebQwen.exe and talks to it (transport, message shapes, lifetime, crash handling), and what the chat request carries beyond messages and sampling (tools for the MCP side, thinking on/off). The C API grows field by field as each is approved.
