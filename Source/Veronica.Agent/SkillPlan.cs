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

public sealed record PlanItem(string Text, PlanItemState State, string? SkipReason = null);

// The active skill's plan and the skills the model has read, held outside the transcript.
// It lasts across turns until the plan is through or the chat is cleared.
public sealed class SkillPlan
{
    public const string EndTurnMarker = "<end_turn>";

    private readonly object _lock = new();
    private readonly HashSet<string> _read = new(StringComparer.OrdinalIgnoreCase);
    private string? _skill;
    private List<PlanItem> _items = [];

    public static IReadOnlySet<string> ToolNames { get; } = new HashSet<string>
    {
        SkillTool.ToolName, ActivateSkillTool.ToolName, CompletePlanItemTool.ToolName, SkipPlanItemTool.ToolName,
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
        }
    }

    public string Activate(Skill skill)
    {
        lock (_lock)
        {
            var result = new StringBuilder();
            if (_skill != null && Current() >= 0)
                result.Append($"This replaces the plan of {_skill}, whose open items are dropped. ");
            _skill = skill.Name;
            _items = skill.Plan.Select(i => new PlanItem(i, PlanItemState.Open)).ToList();
            result.Append($"Skill {skill.Name} is active. Its plan:");
            for (int i = 0; i < _items.Count; i++)
                result.Append($"\n{i + 1}. {_items[i].Text}");
            result.Append('\n').Append(WorkOn(0));
            if (_read.Add(skill.Name))
                result.Append("\n\nThe skill:\n").Append(skill.Body);
            return result.ToString();
        }
    }

    public string Complete() => Close(PlanItemState.Done, null);

    public string Skip(string reason) => Close(PlanItemState.Skipped, reason);

    // Null lets the turn end: no open item, or the model stopped on purpose.
    public string? EndNudge(string reply)
    {
        lock (_lock)
        {
            int current = Current();
            if (current < 0 || reply.Contains(EndTurnMarker, StringComparison.Ordinal))
                return null;
            return $"<harness>You ended your turn, but item {current + 1} of {_items.Count} of {_skill} is still open: {_items[current].Text}. " +
                $"Continue it, call {CompletePlanItemTool.ToolName} if it is done, or call {SkipPlanItemTool.ToolName} with a reason if it does not fit. " +
                $"If you asked the user something, they have not seen this reminder and have not answered yet: to wait for their answer, reply with just {EndTurnMarker}</harness>";
        }
    }

    // A model cut off while thinking has lost track of where it was.
    public string CurrentItemNote()
    {
        lock (_lock)
        {
            int current = Current();
            return current < 0 ? "" : $" You are on item {current + 1} of {_items.Count} of {_skill}: {_items[current].Text}.";
        }
    }

    public static string StripEndTurnMarker(string text) => text.Replace(EndTurnMarker, "", StringComparison.Ordinal).Trim();

    private string Close(PlanItemState state, string? reason)
    {
        lock (_lock)
        {
            int current = Current();
            if (current < 0)
                return $"No skill is active. Call {ActivateSkillTool.ToolName} first.";
            var item = _items[current] = _items[current] with { State = state, SkipReason = reason };
            var closed = state == PlanItemState.Done ? $"Done: {item.Text}." : $"Skipped: {item.Text} ({reason}).";
            int next = Current();
            if (next >= 0)
                return $"{closed} {WorkOn(next)}";
            var finished = _skill;
            _skill = null;
            _items = [];
            return $"{closed} The plan of {finished} is through. Do what its last item says, or end your turn with a short summary for the user.";
        }
    }

    private int Current() => _items.FindIndex(i => i.State == PlanItemState.Open);

    private string WorkOn(int index) =>
        $"Work on item {index + 1} of {_items.Count} now: {_items[index].Text}. Do only this, then call {CompletePlanItemTool.ToolName}.";
}

public sealed class ActivateSkillTool(Func<IReadOnlyList<Skill>> skills, SkillPlan plan) : ITool
{
    public const string ToolName = "activate_skill";

    public string Name => ToolName;

    public string Description =>
        "Makes a skill the active one and starts its plan, item by item. Replaces the active skill's plan.";

    public string Parameters => """
        {"type":"object","properties":{"name":{"type":"string","description":"Skill name, e.g. create-block"}},"required":["name"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var skill = SkillTool.Find(skills(), arguments, out var notFound);
        return Task.FromResult(new ToolResult(skill == null ? notFound : plan.Activate(skill)));
    }
}

public sealed class CompletePlanItemTool(SkillPlan plan) : ITool
{
    public const string ToolName = "complete_plan_item";

    public string Name => ToolName;

    public string Description => "Marks the current item of the active skill's plan done and names the next one.";

    public string Parameters => """{"type":"object","properties":{}}""";

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolResult(plan.Complete()));
}

public sealed class SkipPlanItemTool(SkillPlan plan) : ITool
{
    public const string ToolName = "skip_plan_item";

    public string Name => ToolName;

    public string Description => "Skips the current item of the active skill's plan, when it does not fit the task, and names the next one.";

    public string Parameters => """
        {"type":"object","properties":{"reason":{"type":"string","description":"Why the item does not fit"}},"required":["reason"]}
        """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var reason = arguments.TryGetProperty("reason", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : "";
        return Task.FromResult(new ToolResult(reason.Length == 0 ? $"{ToolName} needs a reason." : plan.Skip(reason)));
    }
}
