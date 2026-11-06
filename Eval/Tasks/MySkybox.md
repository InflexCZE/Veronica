# My Skybox

## Prompt

I put six skybox images into my mod. Make them the space background in the game.

## User

- Exported six face images from a sky tool and dropped them into the mod; knows they are in a MySkybox folder and named with `_px`, `_nx`, `_py`, `_ny`, `_pz`, `_nz` endings, not the full paths. Confirms the files when the model names them.
- Each only when asked:
  - name and folder for new files: "put it next to the images" or anything sensible;
  - whether to replace the default background everywhere: yes, everywhere;
  - the sun: leave it as it is.
- Does not know engine internals: cubemaps, definition types, field names, file paths in vanilla.
- **Second message,** sent after the model says the skybox is done (or once the first turn has ended with the skybox in place): "Looking at it, my px and nx images came out of the export the wrong way round. Can you swap those two?" If asked which faces those are, the user only repeats the file endings.

## Pass

1. A `.cubemap` in the dev mod made from the six fixture faces with `create_asset` kind `skybox`: `Category` Skybox, and before the swap each face in its suffix's slot: `PositiveXTexture` `MySky_px.png` (b48fec41-95e0-41f2-8084-4553ca016973), `NegativeXTexture` `MySky_nx.png` (f32ce6cc-1f91-4c24-a5ac-ed8370b86a12), `PositiveYTexture` `MySky_py.png` (3cc862be-1de0-4912-84e3-5fb1770bfd9f), `NegativeYTexture` `MySky_ny.png` (e8ef179b-0ff6-4129-9d22-05241a712092), `PositiveZTexture` `MySky_pz.png` (8580f797-f043-4585-804f-b48dc6900d5b), `NegativeZTexture` `MySky_nz.png` (dfab4e8f-df68-420a-9b34-867b164da36d). The transcript shows the slots before the swap.
2. Exactly one override in the dev mod of the Engine's `EnvironmentResourcesConfiguration` (made with `create_partial` override of Vanilla's `EnvironmentResourcesConfigurationOverride`), its `Skybox` the new cubemap and its `SunFlare` still Vanilla's `SunFlareDefinition`.
3. The model called `show` on the cubemap.
4. After the second message, the same cubemap has `PositiveXTexture` `MySky_nx.png` and `NegativeXTexture` `MySky_px.png`, the other four faces and `Category` unchanged, done by `write` on the cubemap; no second cubemap exists.
5. The fixture's images and metas are unchanged, no vanilla file changed, and nothing outside the dev mod was changed.

Not required, reported: whether the swap went through `read` of the cubemap first, whether the model asked the wiki, and any value it asked the user for that it could have read.

## Fixture

`Eval/Tasks/MySkybox/` holds `make_fixture.py` and the metas of the six faces and their folder (`Assets/MySkybox`), captured from the Editor's own import. Start-Run copies the metas over the clean dev mod and runs the script, which writes the six 256x256 PNG faces `MySky_px.png` .. `MySky_nz.png` (a star field, each face tinted differently so a swapped face shows), the same bytes every time. The images stay out of git.
