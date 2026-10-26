# Wiki for the model

**Exists:** the split between skills and wiki (`Docs/Tech/Skills.md`). Skills carry only the facts their steps need.

**Missing:** the wiki itself.
- Pages: one Markdown file per topic with `name`, `title`, `source` and `description` frontmatter. The description routes questions to the page; pages do not link each other.
- Content from the official SE2 modding guides on Steam (the Modding Overview, G-Screen, Create New Thruster, Creating an Armor Block, Create New Voxel Material, Making a Skybox, Character Model Modding, Thruster Flame Visuals, New Thruster Sound, Adding New Music): concepts, the G-Screen hierarchy and ordering, art rules (mesh types, LODs, mount points, materials, texture and cubemap naming), the vanilla folder layout, FMOD routing. Click-by-click UI walkthroughs and art-tool instructions are dropped.
- Delivery: a `wiki(question)` tool. A researcher subagent on the same model reads pages and answers only what bears on the question; its reading never enters the main transcript. It needs a bound on how much it reads, since pages and its answer share a 20K context.
- Start from the skills' steps that say "ask the wiki", once skills have run live.
