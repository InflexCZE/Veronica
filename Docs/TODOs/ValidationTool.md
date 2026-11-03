# Validation issues

**Exists:** editor tools edit definitions but neither the agent nor an eval run sees whether the result is valid. Validation messages appear only in the Editor's log; eval grading does not read them, so a block with an unsatisfied component dependency passed (MyGatling H3).

**Missing:** a validation endpoint on the eval control channel, listing the Editor's validation issues (errors, warnings, field paths), after waiting until asset processing and content pushes are idle. Start from `AssetValidationFeature.Validator.ValidationControllers` (public, ContentPipeline) and `ProcessingManager` queue and push status for idleness. Eval grading then fails a run on a validation error; warnings pass.

**Owner decision:** the endpoint is for the eval control channel only, not a tool for the model: valid mods carry many benign warnings that would mislead it.
