using System.Text;
using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent;

public enum PlanItemState
{
    Open,
    Done,
    Skipped,
}

// Note is the model's proof for a completed item, or its reason for a skipped one.
public sealed record PlanItem(string Text, PlanItemState State, string? Note = null);

// The active skill's plan, the plans paused under it and the skills the model has read, held outside the transcript.
// It lasts across turns until the plans are through or the chat is cleared.
public sealed class SkillPlan
{
    private readonly object _lock = new();
    private readonly HashSet<string> _read = new(StringComparer.OrdinalIgnoreCase);
    private string? _skill;
    private string _overview = "";
    private List<PlanItem> _items = [];
    private readonly Stack<Paused> _paused = new();
    private int _unplannedEdits;
    private int _closedItems;

    private sealed record Paused(string Skill, string Overview, List<PlanItem> Items);

    public const string ActivationHint =
        "No skill is active. If a skill fits this work, activate it with " + ActivateSkillTool.ToolName + " so its plan keeps track of what is left.";

    public static IReadOnlySet<string> ToolNames { get; } = new HashSet<string>
    {
        SkillTool.ToolName, ActivateSkillTool.ToolName, MarkCompletedTool.ToolName, MarkSkippedTool.ToolName, WaitForUserTool.ToolName,
    };

    public string? ActiveSkill
    {
        get { lock (_lock) return _skill; }
    }

    public IReadOnlyList<PlanItem> Items
    {
        get { lock (_lock) return _items.ToList(); }
    }

    // Items completed or skipped since the plan was created, across plans and resets.
    public int ClosedItems
    {
        get { lock (_lock) return _closedItems; }
    }

    public void MarkRead(string skill)
    {
        lock (_lock)
            _read.Add(skill);
    }

    // After a compaction only the skill carried in the compacted message is in context again.
    public void ForgetReads(string? keep)
    {
        lock (_lock)
        {
            _read.Clear();
            if (keep != null)
                _read.Add(keep);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _read.Clear();
            _skill = null;
            _overview = "";
            _items = [];
            _paused.Clear();
            _unplannedEdits = 0;
        }
    }

    // Edits made while no plan is open: the first carries the hint, then every tenth, so it reminds without nagging.
    // After a finished plan the count starts one in, so the first hint comes on the tenth edit.
    public string? EditHint()
    {
        lock (_lock)
        {
            if (Current() >= 0)
                return null;
            return _unplannedEdits++ % 10 == 0 ? ActivationHint : null;
        }
    }

    public string Activate(Skill skill)
    {
        lock (_lock)
        {
            if (skill.Plan.Count == 0)
                return $"{skill.Name} has no plan, so it cannot be activated; read it with {SkillTool.ToolName} instead.";
            int current = Current();
            if (current >= 0 && string.Equals(_skill, skill.Name, StringComparison.OrdinalIgnoreCase))
                return $"{_skill} is already active. {WorkOnOrSkip(current)}";
            var paused = _paused.FirstOrDefault(p => string.Equals(p.Skill, skill.Name, StringComparison.OrdinalIgnoreCase));
            if (paused != null)
            {
                int at = Current(paused.Items) + 1;
                return $"{paused.Skill} is paused at item {at} under {_skill}. {_skill} is on item {current + 1} of {_items.Count}: {_items[current].Text}. " +
                    $"Work on it, then call {MarkCompletedTool.ToolName} with proof, or {MarkSkippedTool.ToolName} with a reason if it does not apply; " +
                    $"when {_skill}'s plan is done, {paused.Skill} resumes at item {at}.";
            }
            var result = new StringBuilder($"Skill {skill.Name} is active. ");
            if (current >= 0)
            {
                _paused.Push(new Paused(_skill!, _overview, _items));
                result.Append($"{_skill} pauses at item {current + 1} and resumes when {skill.Name}'s plan is done. ");
            }
            _skill = skill.Name;
            _overview = skill.Overview;
            _unplannedEdits = 0;
            _items = skill.Plan.Select(i => new PlanItem(i, PlanItemState.Open)).ToList();
            result.Append(WorkOn(0));
            if (_read.Add(skill.Name))
                result.Append("\n\nThe skill:\n").Append(skill.Overview);
            return result.ToString();
        }
    }

    public string Complete(string proof) => Close(PlanItemState.Done, proof);

    public string Skip(string reason) => Close(PlanItemState.Skipped, reason);

    // Null lets the turn end: no item is open. A turn ended by wait_for_user never gets here.
    public string? EndNudge()
    {
        lock (_lock)
        {
            int current = Current();
            if (current < 0)
                return null;
            return $"<harness>You ended your turn, but item {current + 1} of {_items.Count} of {_skill} is still open: {_items[current].Text}. " +
                $"Continue it, or call {MarkCompletedTool.ToolName} with proof if you did it, or {MarkSkippedTool.ToolName} with a reason only if it does not apply to the task, never to wait. " +
                $"The user has not seen this reminder. If you need the user's answer, ask with {AskUserTool.ToolName}; if you described a step for them in the Editor, call {WaitForUserTool.ToolName}.</harness>";
        }
    }

    // A model cut off while thinking has lost track of where it was.
    public string CurrentItemNote() => CurrentItem() is { } item ? $" You are on {item}." : "";

    // Null while no plan is open.
    public string? CurrentItem()
    {
        lock (_lock)
        {
            int current = Current();
            return current < 0 ? null : $"item {current + 1} of {_items.Count} of {_skill}: {_items[current].Text}";
        }
    }

    // Every plan on the stack, outermost first, from its current item on. Null while no plan is open.
    public string? OpenPlans()
    {
        lock (_lock)
        {
            if (Current() < 0)
                return null;
            var text = new StringBuilder();
            foreach (var paused in _paused.Reverse())
                AppendOpen(text, paused.Skill, "paused until the plans below it are done", paused.Items);
            AppendOpen(text, _skill!, "active", _items);
            return text.ToString().TrimEnd();
        }
    }

    private static void AppendOpen(StringBuilder text, string skill, string state, List<PlanItem> items)
    {
        int current = Current(items);
        text.Append($"{skill} ({state}):\n");
        for (int i = current; i < items.Count; i++)
            text.Append($"  {i + 1}. ").Append(i == current ? "[current] " : "").Append(items[i].Text).Append('\n');
    }

    private string Close(PlanItemState state, string note)
    {
        lock (_lock)
        {
            int current = Current();
            if (current < 0)
                return $"No skill is active. Call {ActivateSkillTool.ToolName} first.";
            if (note.Length == 0)
                return state == PlanItemState.Done
                    ? $"{MarkCompletedTool.ToolName} needs proof: one line on what you did for the item."
                    : $"{MarkSkippedTool.ToolName} needs a reason.";
            var item = _items[current] = _items[current] with { State = state, Note = note };
            _closedItems++;
            var closed = state == PlanItemState.Done ? $"Done: {item.Text} ({note})." : $"Skipped: {item.Text} ({note}).";
            int next = Current();
            if (next >= 0)
                return $"{closed} {WorkOn(next)}";
            var finished = _skill;
            if (_paused.TryPop(out var parent))
            {
                _skill = parent.Skill;
                _overview = parent.Overview;
                _items = parent.Items;
                int resumed = Current();
                var back = $"{closed} {finished}'s plan is done. Back to {_skill}, item {resumed + 1} of {_items.Count}: {_items[resumed].Text}. " +
                    $"If the work this item asks for is done, call {MarkCompletedTool.ToolName} with proof; do not redo it. If it needs another skill, activate it.";
                return _read.Add(_skill) ? back + "\n\nThe skill:\n" + _overview : back;
            }
            _skill = null;
            _overview = "";
            _items = [];
            // A follow-up to finished work is often a few edits; the hint then waits for the tenth instead of the first.
            _unplannedEdits = 1;
            return $"{closed} The plan of {finished} is through. Do what its last item says, or end your turn with a short summary for the user.";
        }
    }

    private int Current() => Current(_items);

    private static int Current(List<PlanItem> items) => items.FindIndex(i => i.State == PlanItemState.Open);

    private string WorkOn(int index) =>
        $"Work on item {index + 1} of {_items.Count} now: {_items[index].Text}. Ask the user for what it needs from them; once its work is done, call {MarkCompletedTool.ToolName} with proof of what you did.";

    private string WorkOnOrSkip(int index) =>
        $"Work on item {index + 1} of {_items.Count} now: {_items[index].Text}. Ask the user for what it needs from them; once its work is done, call {MarkCompletedTool.ToolName} with proof of what you did, " +
        $"or {MarkSkippedTool.ToolName} with a reason if it does not apply.";
}

public sealed class ActivateSkillTool(Func<IReadOnlyList<Skill>> skills, SkillPlan plan) : ITool
{
    public const string ToolName = "activate_skill";

    public string Name => ToolName;

    public string Description =>
        "Call this before doing a skill's work, as soon as the user wants it done: the Editor gives you the skill's plan one item at a time and keeps track of what is done, so no step is missed. Reading a skill with skill does not start it. An open plan pauses at its current item and resumes when the new skill's plan is done.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Skill name, e.g. create-block"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var skill = SkillTool.Find(skills(), arguments, out var notFound);
        if (skill == null)
            return Task.FromResult(new ToolResult(notFound, Refused: true) { Summary = $"Could not start skill {Gist.Text(arguments, "name")}: there is no such skill" });
        var before = plan.ActiveSkill;
        var output = plan.Activate(skill);
        var after = plan.ActiveSkill;
        bool active = string.Equals(after, skill.Name, StringComparison.OrdinalIgnoreCase);
        var result = active && !string.Equals(before, skill.Name, StringComparison.OrdinalIgnoreCase)
            ? new ToolResult(output) { Summary = $"Started skill: {skill.Name}" }
            : active
                ? new ToolResult(output) { Summary = $"Skill {skill.Name} is already active" }
                : new ToolResult(output, Refused: true)
                {
                    Summary = skill.Plan.Count == 0
                        ? $"Could not start skill {skill.Name}: it has no plan"
                        : $"Could not start skill {skill.Name}: it is paused under {after}",
                };
        return Task.FromResult(result);
    }

    public string Attempt(JsonElement arguments) => $"start skill {Gist.Text(arguments, "name")}";
}

public sealed class MarkCompletedTool(SkillPlan plan) : ITool
{
    public const string ToolName = "mark_completed";

    public string Name => ToolName;

    public string Description =>
        "Marks the current item of the active skill's plan completed and names the next one. Call it only after you did the item's work yourself, " +
        "with proof: one line on what you did. Marking does none of the work.";

    public string Parameters => """
        {"type":"object","properties":{"proof":{"type":"string","description":"One line on what you did for this item"}},"required":["proof"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(Close(plan, plan.Complete, ToolArgs.Text(arguments, "proof"), "Finished step", "finish a step", "proof"));

    // The closed count tells whether the plan took the call.
    internal static ToolResult Close(SkillPlan plan, Func<string, string> close, string note, string done, string attempt, string noteName)
    {
        var step = plan.Items.FirstOrDefault(i => i.State == PlanItemState.Open)?.Text;
        int closed = plan.ClosedItems;
        var output = close(note);
        if (plan.ClosedItems != closed && step != null)
            return new ToolResult(output) { Summary = $"{done}: {Gist.Clip(step, 80)}" };
        return new ToolResult(output, Refused: true)
        {
            Summary = step == null ? $"Could not {attempt}: no skill is active" : $"Could not {attempt}: no {noteName} given",
        };
    }

    public string Attempt(JsonElement arguments) => "finish a step";
}

public sealed class MarkSkippedTool(SkillPlan plan) : ITool
{
    public const string ToolName = "mark_skipped";

    public string Name => ToolName;

    public string Description => "Marks the current item of the active skill's plan skipped, only when it does not apply to the task, and names the next one. " +
        "If the item's work is already done, call mark_completed with proof instead.";

    public string Parameters => """
        {"type":"object","properties":{"reason":{"type":"string","description":"Why the item does not apply"}},"required":["reason"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(MarkCompletedTool.Close(plan, plan.Skip, ToolArgs.Text(arguments, "reason"), "Skipped step", "skip a step", "reason"));

    public string Attempt(JsonElement arguments) => "skip a step";
}

// Small models act through tool calls; a reply marker beside the plan tools read to them as one more way to close an item.
// Only the client tool loop can end a turn on it, so it is never offered over MCP.
public sealed class WaitForUserTool : ITool
{
    public const string ToolName = "wait_for_user";

    public string Name => ToolName;

    public string Description => $"Ends your turn to wait while the user does a step in the Editor that your reply described. Ask questions with {AskUserTool.ToolName} instead.";

    public string Parameters => """{"type":"object","properties":{}}""";

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        // The turn ends on this call, so the model reads the result only after the user's reply.
        Task.FromResult(new ToolResult("Waited for the user.") { Summary = "Waiting for you to do the step in the Editor" });
}

// An editing tool whose results carry the plan's activation hint while no skill plan is open.
public sealed class PlanHintedTool(ITool inner, SkillPlan plan) : ITool
{
    public string Name => inner.Name;

    public string Description => inner.Description;

    public string Parameters => inner.Parameters;

    public string Attempt(JsonElement arguments) => inner.Attempt(arguments);

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        return plan.EditHint() is { } hint ? result with { Output = result.Output + "\n\n" + hint } : result;
    }
}
