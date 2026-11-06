# Setup tool

**Exists:** `Docs/Tech/Distribution.md` decides `Veronica.exe`. None of it is built.

**Missing:**
- The project: a self-contained single-file .NET exe with a window.
- Install and update from `/releases/latest`: `Veronica.zip` through `Downloads`, `Runtime` replaced, waiting for the Editor to exit while `Runtime`'s DLLs are locked.
- Self-update: rename the running exe to `.old`, move the new one in, restart, delete `.old` on start.
- The mod list from the HUB's `ModIndex`, adding and removing the two `.vrgproj` entries.
- The model list: the requirements check per manifest model (`RequirementsCheck.md`), the recommendation, the override, several installed models, and the config in `Models` naming the active one. The downloads are `ModelDownloads.md`.
- The `Skills` / `Wiki` merge: the record of installed files with version and hash, `name.new.md` (empty for a dropped file), the conflict list with [Keep mine], [Use new version] and [Open].
