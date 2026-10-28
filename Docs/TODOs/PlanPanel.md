# Plan panel

**Exists:** the active skill's plan is harness state (`Docs/Tech/Skills.md`, Plan). The user sees it only through the plan tool calls in the chat.

**Missing:** a checklist in the panel above the input: the active skill, done and skipped items (with the skip reason) and the current one, so the user sees where the work is and can tell a summary that claims more than was done. Deferred until eval runs show the plan working.

**Start at:** `Source/Veronica.Agent/SkillPlan.cs`, `Source/Veronica/Ui/ChatView.axaml`.
