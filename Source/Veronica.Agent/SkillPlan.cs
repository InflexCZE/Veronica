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

// The active skill's plan and the skills the model has read, held outside the transcript.
// It lasts across turns until the plan is through or the chat is cleared.
public sealed class SkillPlan
{
    private readonly object _lock = new();
    private readonly HashSet<string> _read = new(StringComparer.OrdinalIgnoreCase);
    private string? _skill;
    private List<PlanItem> _items = [];
    private int _unplannedEdits;

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

    public void MarkRead(string skill)
    {
        lock (_lock)
            _read.Add(skill);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _read.Clear();
            _skill = null;
            _items = [];
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
                return $"{_skill} is already active. {WorkOn(current)}";
            // Only the last item may still be open: in a chain it is the hand-off to the next skill. An earlier
            // open item would be dropped without a word, so the model has to close it first.
            var open = Enumerable.Range(0, Math.Max(0, _items.Count - 1))
                .Where(i => _items[i].State == PlanItemState.Open)
                .ToList();
            if (open.Count > 0)
                return $"{_skill} still has open items: " +
                    string.Join("; ", open.Select(i => $"{i + 1}. {_items[i].Text}")) +
                    $". Before activating another skill, call {MarkCompletedTool.ToolName} with proof for each one you did, " +
                    $"or {MarkSkippedTool.ToolName} with the reason it does not apply.";
            _skill = skill.Name;
            _unplannedEdits = 0;
            _items = skill.Plan.Select(i => new PlanItem(i, PlanItemState.Open)).ToList();
            var result = new StringBuilder($"Skill {skill.Name} is active. ").Append(WorkOn(0));
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
            var closed = state == PlanItemState.Done ? $"Done: {item.Text} ({note})." : $"Skipped: {item.Text} ({note}).";
            int next = Current();
            if (next >= 0)
                return $"{closed} {WorkOn(next)}";
            var finished = _skill;
            _skill = null;
            _items = [];
            // A follow-up to finished work is often a few edits; the hint then waits for the tenth instead of the first.
            _unplannedEdits = 1;
            return $"{closed} The plan of {finished} is through. Do what its last item says, or end your turn with a short summary for the user.";
        }
    }

    private int Current() => _items.FindIndex(i => i.State == PlanItemState.Open);

    private string WorkOn(int index) =>
        $"Work on item {index + 1} of {_items.Count} now: {_items[index].Text}. Ask the user for what it needs from them; once its work is done, call {MarkCompletedTool.ToolName} with proof of what you did.";
}

public sealed class ActivateSkillTool(Func<IReadOnlyList<Skill>> skills, SkillPlan plan) : ITool
{
    public const string ToolName = "activate_skill";

    public string Name => ToolName;

    public string Description =>
        "Makes a skill the active one and starts its plan; the Editor then names its items one at a time. Replaces a plan whose only open item is its last.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Skill name, e.g. create-block"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var skill = SkillTool.Find(skills(), arguments, out var notFound);
        return Task.FromResult(new ToolResult(skill == null ? notFound : plan.Activate(skill)));
    }
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

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ToolResult(plan.Complete(ToolArgs.Text(arguments, "proof"))));
    }
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

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ToolResult(plan.Skip(ToolArgs.Text(arguments, "reason"))));
    }
}

// Small models act through tool calls; a reply marker beside the plan tools read to them as one more way to close an item.
public sealed class WaitForUserTool : ITool
{
    public const string ToolName = "wait_for_user";

    public string Name => ToolName;

    public string Description => $"Ends your turn to wait while the user does a step in the Editor that your reply described. Ask questions with {AskUserTool.ToolName} instead.";

    public string Parameters => """{"type":"object","properties":{}}""";

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolResult("Waiting for the user."));
}

// An editing tool whose results carry the plan's activation hint while no skill plan is open.
public sealed class PlanHintedTool(ITool inner, SkillPlan plan) : ITool
{
    public string Name => inner.Name;

    public string Description => inner.Description;

    public string Parameters => inner.Parameters;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        return plan.EditHint() is { } hint ? result with { Output = result.Output + "\n\n" + hint } : result;
    }
}
