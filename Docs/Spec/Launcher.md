# Launcher

One downloaded program installs Veronica, sets up its AI provider, enables it per mod and keeps it up to date.

## First run

- Three pages: welcome, provider choice, provider setup. Going back is always free; going forward only past finished pages.
- Welcome picks the install folder and offers Start menu and Desktop shortcuts. A folder that already holds other files is allowed: Veronica's folders go beside them, and the modder is told so. The Launcher copies itself there and carries on from the copy.
- The install runs in the background while the modder sets up a provider, with its progress or an error and a retry.
- Each provider's setup ends with one button that runs a quick check, then saves. A failed check stays on the page with its reason.
- With no model, the setup shows the prompt that connects the modder's agent and the check waits until the agent connects. It needs the Editor closed, and can be skipped.

## Afterwards

- Three tabs: mods, AI provider, updates (marked when something waits).
- Mods: the modder's local mods, each with Veronica enabled or not, and a way to open it in the Editor. A mod the Editor does not list is added by its project file and stays listed; removing it takes Veronica out of it first. An added project that went missing stays listed, marked, until removed. The Launcher finds the Editor and the game through Steam, or asks for the Editor once. A mod using another Veronica build offers only removal.
- Installed models can be deleted.
- Downloads can be cancelled; a partial model download resumes.

## Updates

- The chat panel shows when a newer Veronica exists; its button runs the Launcher, which updates once the Editor is closed.
- The Launcher updates itself too.
- A model whose pinned revision changed is replaced without asking.
- Skill and wiki files the modder edited are kept. The new version is offered beside each, to keep either or open both.

## Voice

- Only headlines and greetings speak as Veronica. Status and technical lines are neutral, and the local model is never "I".

## Not yet

- Code signing: Windows SmartScreen warns on the first start ([CodeSigning](../TODOs/CodeSigning.md)).

## Out of scope

- Collaborators without Veronica at the same path cannot open a mod with Veronica enabled.
