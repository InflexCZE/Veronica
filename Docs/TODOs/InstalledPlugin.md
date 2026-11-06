# Plugin running from an install

**Exists:** `Veronica.dll` finds `WebQwen.exe`, the skills and the wiki through paths the build writes into it (`WebQwenExe`, `SkillsDir`, `WikiDir` in `Source/Veronica/Veronica.csproj`). The model id is a constant in `Source/Veronica/LocalModel.cs`, and `WebQwen.exe` finds the models in `.deps/models` above itself. `Docs/Tech/Distribution.md` decides how an installed plugin works instead.

**Missing:**
- Finding `WebQwen.exe`, `..\Skills`, `..\Wiki` and `..\Models` relative to `Veronica.dll` when the install layout is there, the build-time paths otherwise; passing `--models` to `WebQwen.exe`.
- Running the model the `Models` config names as active, instead of the constant.
- Copying `dxcompiler.dll` and `dxil.dll` from the Editor's folder next to `WebQwen.exe` when missing or older.
- The latest-release check per Editor start, the notice in the panel and its button that starts setup.
- The skill and wiki loaders skipping `*.new.md`.
