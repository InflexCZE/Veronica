---
name: pull-webqwen
description: Pull a new WebQwen build (C:\Codes\WebQwen\dist) into Veronica - engine tarball, manifest, golden logit references - then build, run the logit gate and commit
---

# Pull a new WebQwen dist into Veronica

Source: `C:\Codes\WebQwen\dist` (or a path given as the argument). The vendored
copy lives in `Source/WebQwen.Native/Engine/` (tarball + `manifest.json`); the
golden references in `Source/WebQwen.Tests/Golden/<model_id>/`.

1. Check the dist: `manifest.json` present, `repo.dirty` false, and
   `repo.sha` equal to `git -C C:\Codes\WebQwen rev-parse HEAD`. Otherwise
   stop and tell the owner; do not rebuild the dist from here.
2. What shipped since the vendored build: take the old `repo.short_sha` from
   `Source/WebQwen.Native/Engine/manifest.json`, then
   `git -C C:\Codes\WebQwen log --oneline <old>..<new>` and
   `git -C C:\Codes\WebQwen diff --stat <old>..<new> -- docs/qwen38`.
   Summarize for the owner which slices landed and what they change for
   Veronica (engine API, browser APIs used, model records, libraries).
3. Replace the vendored files: delete the old `.tgz`, copy the new `.tgz` and
   `manifest.json`. Report manifest changes: models added or removed, library
   `original_name` / `url` / `sha256`, weights `url` revisions (a new weights
   revision means every user downloads again).
   Then re-apply Veronica's edit of the copied manifest (a WebQwen dist carries
   every model it builds; modders get only these). Keep only the offered models,
   ordered by `vram_required_MB`, each with `display_name`, `description` and
   `graphics_card_GB` (the card size modders are told to have, with room for
   the Editor) right after `model_id`:
   - `Ternary-Bonsai-2-27B-MLC`: `Small`, `Dumber, but less demanding.`, 8
   - `Qwen3.8-27B-UD-Q3_K_XL-MLC`: `Smart`, `Better answers, if you can run it.`, 16
   Drop every other model (the 9B is not offered). A new model in the dist that
   is not listed here goes to the owner: offer it or not, and under what name.
4. Golden references, per model folder in `Source/WebQwen.Tests/Golden/`:
   the dumps' `lib` field names the library they were made with. If it equals
   the new manifest's `lib.original_name`, keep them. Otherwise find the
   browser build's teacher-forced dumps for the new library: the
   `C:\Codes\WebQwen\.tmp\quality\*-forced` directory whose 12 files all carry
   that `lib` (the slice that shipped the library names its forced run). Copy
   them over the old ones. If none exist, stop and tell the owner: producing
   them needs the WebQwen bench daemon and Chrome (an attended step there).
5. Models: for every model whose weights or library changed, run
   `Build\FetchModel.ps1 -ModelId <id>` (add `-From` / `-LibFrom` to copy a
   local WebQwen build; the script verifies against Hugging Face either way).
6. Build with VS MSBuild:
   `"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" Veronica.sln /restore /p:Configuration=Release /p:Platform=x64`.
   A new engine may use browser APIs the trampolines do not implement yet
   (`Source/WebQwen.Native/js/`); a load or chat failure names them. Implement
   only what the engine uses.
7. Run the gate with about 7 GB of VRAM free (`nvidia-smi`; another app
   holding VRAM makes Dawn page and slows decode about 100x):
   `dotnet test Source\WebQwen.Tests\bin\x64\Release\net9.0\WebQwen.Tests.dll --logger "console;verbosity=detailed"`.
   It must pass; report the table.
8. Commit: subject `WebQwen: engine <new short_sha>`, bullets for the slices
   that matter to Veronica and any trampoline or golden changes.
