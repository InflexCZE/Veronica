# Plugin running from an install

**Exists:** `Veronica.dll` finds `WebQwen.exe`, the skills and the wiki through paths the build writes into it (`WebQwenExe`, `SkillsDir`, `WikiDir` in `Source/Veronica/Veronica.csproj`). The model id is a constant in `Source/Veronica/LocalModel.cs`, and `WebQwen.exe` finds the models in `.deps/models` above itself. Settings are environment variables read where they are used. `Docs/Tech/Distribution.md` decides how an installed plugin works instead.

**Missing:**
- The settings store: the static `Settings` class of keys, filled from `Veronica.settings.json` and then environment variables, DPAPI decryption of the API key; every place that reads an environment variable reads the store instead.
- Finding `WebQwen.exe`, `..\Skills`, `..\Wiki`, `..\Models` and `..\Veronica.settings.json` relative to `Veronica.dll` when the install layout is there, the build-time paths otherwise; passing `--models` to `WebQwen.exe`.
- Running the model the settings name as active, instead of the constant.
- Copying `dxcompiler.dll` and `dxil.dll` from the Editor's folder next to `WebQwen.exe` when missing or older.
- Stamping the SemVer version into `Veronica.dll`; the latest-release check per Editor start, the notice in the panel and its button that starts `Veronica.exe`.
- The skill and wiki loaders skipping `*.new.md`.
