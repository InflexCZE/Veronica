# wait_for_user named to the Claude provider

**Exists:** `wait_for_user` is not offered over MCP and not in the chat's tools while Claude is the provider (`Docs/Tech/ClaudeProvider.md`, Isolation). Claude's system prompt and the skills still tell it to call `wait_for_user` for a step the user does in the Editor.

**Missing:** wording for Claude: describe the Editor step and end the reply, or ask with `ask_user` to wait for the user. Needs an owner decision on which, and on how the prompt and skills vary by provider.
