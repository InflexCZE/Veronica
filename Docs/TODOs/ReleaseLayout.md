# Publishing a release

**Exists:** `Build\Publish.ps1 -Version X.Y.Z` builds `Veronica.exe` and `Veronica.zip` into `bin\dist\<version>` (`Docs/Tech/Distribution.md`).

**Missing:**
- The GitHub repo and creating its releases with both assets; the repo lives on a local Gitea only.
- The plugin in the release compiles against the Editor `Directory.Build.local.props` points at (`EditorVersionFloor.md`).
