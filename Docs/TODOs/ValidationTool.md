# Validation issues for the agent

**Exists:** editor tools edit definitions but the agent cannot see whether the result is valid. The user finds issues in the Editor or in-game.

**Missing:** a tool listing the Editor's validation issues (errors, warnings, field paths) for a target or folder, after waiting until asset processing and content pushes are idle. Start from `AssetValidationFeature.Validator.ValidationControllers` (public, ContentPipeline) and `ProcessingManager` queue and push status for idleness.
