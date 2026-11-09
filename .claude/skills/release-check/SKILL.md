---
name: release-check
description: By-hand checks of a Veronica release candidate on a real machine before it is published - Launcher install, provider setup, mods, updates, self-update, plugin update notice
---

# Release check

Run on a real machine against the release candidate built by `Build/Publish.ps1`, served from a test release (the releases URL setting can point at a local Gitea).

## First run

- Start the Launcher from a Downloads folder.
- Default install folder, and a changed one picked with the folder dialog.
- A chosen folder that already holds other files: the line saying Veronica's folders go beside them, and going on works.
- The copy and restart lands on the provider choice with the install running; the original exe stays where it was.
- Both shortcuts (Start menu, Desktop) are created and start the installed Launcher.
- A second start while one runs brings the first to the front.

## Providers

- Fresh install from the release: set up each provider and pass its check.
- Local model: download a model, interrupt it, and resume; the check passes after verification.
- OpenAI-compatible: a bare host name finds the server and the full URL is saved.
- The API key survives a Launcher restart and is not readable in `Veronica.settings.json`.

## Mods

- Enable and disable Veronica in a mod; also in a mod enabled by a development build (only removal is offered).
- Start Editor on a mod without Veronica:
  - with the Steam Mod SDK, with and without the Steam game installed;
  - with no SDK in Steam: the Editor exe dialog on first use.

## Updates

- An update with a skill file the user changed: the conflict list, and each of keep mine, use new version and open.
- Self-update of the Launcher when the release carries a newer one.
- The plugin's update notice in the Editor, and its button starting the update once the Editor closes.
- The update button while a Launcher already runs: the running one comes to the front and takes the update request.
