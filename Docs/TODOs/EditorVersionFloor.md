# Editor version floor

**Needs an owner decision.**

**Exists:** `Veronica.dll` compiles against the Editor that `Directory.Build.local.props` points at (the dev checkout). `Docs/Tech/EditorPlugin.md` records that a plugin compiled against VRage build N does not load into an Editor with a lower build number.

**Missing:** the release policy. A release must be compiled against the oldest Editor build it supports. Open: which build that is, where its reference assemblies come from for a release build (a retail Editor install, a pinned reference set in the repo, ...), and how a too-old Editor is reported to the user.
