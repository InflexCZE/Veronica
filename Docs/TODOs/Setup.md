# Veronica.exe

**Exists:** `Docs/Tech/Distribution.md` decides `Veronica.exe`. None of it is built.

**Missing:**
- The project: a self-contained single-file .NET exe with an Avalonia window, SemVer-stamped, its log.
- The install root: the exe's folder, with a warning when it holds files that are not Veronica's.
- The first-run pages (Welcome, provider selection, provider setup, wait for the provider's check) and app mode (provider, mods, versions). Provider pages and checks are `ProviderSelection.md`.
- Writing `Veronica.settings.json` with the DPAPI-encrypted API key.
- Install and update from `/releases/latest` (the release source is a setting): `Veronica.zip` through `Downloads`, `Runtime` replaced, waiting for the Editor to exit.
- Self-update: rename the running exe to `.old`, move the new one in, restart, delete `.old` on start.
- The mod list from the HUB's `ModIndex`; adding, removing and repointing the two `.vrgproj` entries, only while the Editor is closed.
- The model list: the requirements check per manifest model (`RequirementsCheck.md`), fit by total VRAM with a free-VRAM warning, the recommendation, the override, Delete per installed model, the active model in the settings. The downloads are `ModelDownloads.md`.
- The `Skills` / `Wiki` merge against `Veronica.installed.json`: `name.new.md` (empty for a dropped file), the conflict list with [Keep mine], [Use new version] and [Open].
- The unit tests and the live install test listed in `Distribution.md`.
