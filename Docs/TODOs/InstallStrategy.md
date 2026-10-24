# Where Veronica finds WebQwen.exe and the model

**Exists:** `Source/Veronica/LocalModel.cs` runs `WebQwen.exe` from this repo's build output. `Veronica.csproj` writes the exe path into the assembly as `WebQwenExe` metadata. The model id (Bonsai) is a constant there, and `WebQwen.exe` finds the weights in `.deps/models` above itself. A released plugin cannot work this way.

**Missing (needs design with the owner):**
- The install strategy: where Veronica puts `WebQwen.exe` and the model on a user's machine, how it finds and updates them, and how this connects to `ReleaseLayout.md` and `ModelDownloads.md`.
- Which model to run, now a constant.
- A version check between `Veronica.dll` and `WebQwen.exe`. The protocol has no version today because both come from one build (`Docs/Tech/WebQwenProtocol.md`).
