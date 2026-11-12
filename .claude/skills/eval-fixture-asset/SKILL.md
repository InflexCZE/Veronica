---
name: eval-fixture-asset
description: Add a new asset file (an icon png or another small generated file) to a Veronica eval task's fixture - synthesize it, let a live Editor import it so it writes the .meta, and place both in Eval/Tasks/<task>/. Use when an eval task needs a file the model should find and use, not make.
---

# New eval fixture asset

Fixture layout and lifecycle: `Docs/Tech/Eval.md` (Tasks) and `Eval/Start-Run.ps1`. Models are never new: fixtures reuse the MyThruster models.

## Steps

1. Synthesize the file with Python run with `-I`, the script kept in `.tmp/` only; the small file itself goes to git, the script does not. For icons match vanilla: 200x200 RGBA png (e.g. `Assets/MainMenuData/UI/Terminal/Components/Simple/SteelPlate.png`). Look at it with Read before using it.
2. Name it so no prefab the model will make collides with it: `create_prefab` refuses a name whose generated files share a stem with a file in the folder, so `MyItem.png` blocks a prefab `MyItem` there. Use a suffix, e.g. `MyItem_Icon.png`.
3. Start an Editor on a fixture the asset joins: `Eval/Start-Run.ps1 -Task <task> -Model probe` (no model call is made), `Wait-Ready.ps1`. A task without its `.md` yet needs a stub `Eval/Tasks/<task>.md`, deleted afterwards.
4. With the Editor running, copy the file into `Assets/VeronicaDevMod/Assets/<folder>/`. The Editor writes `<file>.meta` and, for a new folder, `<folder>.meta` within seconds. It may write the folder meta in lower case; name it as the folder in the fixture.
5. Check the asset is usable the way the task needs, through the MCP endpoint (`Eval/Mcp-Call.ps1`), e.g. `find_content` finds it and a `write` of it into the field it is meant for holds, then `Get-Validation.ps1`.
6. Copy the file and both metas to `Eval/Tasks/<task>/Assets/<folder>/` (folder meta to `Eval/Tasks/<task>/Assets/`), then `Stop-Run.ps1`.
7. Load check: `Start-Run.ps1 -Task <task> -Model probe`, `Wait-Ready.ps1`, `Get-Validation.ps1 -Label start` exits 0. Diff the dev mod's metas against the fixture's: the Editor rewrites an asset meta at load when nothing uses the asset yet (processing state back to empty, the AssetID kept). Copy the load-time meta into the fixture so every run starts from the same bytes. `Stop-Run.ps1`.
8. Leave the dev mod as it was: `Start-Run` resets it with git and wipes its untracked files, so copy aside anything untracked there first, and restore it after.
