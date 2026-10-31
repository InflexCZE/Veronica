using System.Text.Json;
using Veronica.Agent.Tools;

namespace Veronica.Agent;

public sealed record Question(string Text, IReadOnlyList<string> Options);

// Refused says why nothing was answered. Waited went back to callers blocked on their questions,
// Unwaited is for questions nobody waits for: the user's next message.
public sealed record QuestionAnswers(string? Refused, string? Waited = null, string? Unwaited = null);

// The open question card (Docs/Tech/Agent.md, Questions to the user). ask_user fills it; the panel
// or the eval channel answers or closes it.
public sealed class QuestionBoard
{
    public const string ClosedOutput = "The user closed the questions without answering.";

    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];

    private sealed record Entry(IReadOnlyList<Question> Questions, TaskCompletionSource<string>? Waiter);

    // Any thread.
    public event Action? Changed;

    public IReadOnlyList<Question> Questions => Snapshot().Questions;

    // Card counts up each time the open card is answered, closed or loses questions: what is open
    // then is a new card, even when no one saw it empty in between.
    public (IReadOnlyList<Question> Questions, int Card) Snapshot()
    {
        lock (_gate)
            return (_entries.SelectMany(e => e.Questions).ToList(), _card);
    }

    private int _card;

    public void Post(IReadOnlyList<Question> questions) => Add(new Entry(questions, null));

    // A cancelled wait takes back only its own questions; the user's Stop and /clear close the card.
    public async Task<string> WaitAsync(IReadOnlyList<Question> questions, CancellationToken cancellationToken)
    {
        var waiter = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new Entry(questions, waiter);
        Add(entry);
        using (cancellationToken.Register(() => Remove(entry)))
        {
            string answer = await waiter.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return answer;
        }
    }

    private void Remove(Entry entry)
    {
        lock (_gate)
        {
            if (!_entries.Remove(entry))
                return;
            // The remaining questions moved: answers kept by position would land on the wrong ones.
            _card++;
        }
        entry.Waiter?.TrySetCanceled();
        Changed?.Invoke();
    }

    // One answer per open question, in order; null or blank skips it.
    public QuestionAnswers Answer(IReadOnlyList<string?> answers)
    {
        List<Entry> entries;
        lock (_gate)
        {
            int count = _entries.Sum(e => e.Questions.Count);
            if (count == 0)
                return new QuestionAnswers("No question card is open.");
            if (answers.Count != count)
                return new QuestionAnswers($"The card has {count} questions; send one answer per question, {answers.Count} given.");
            entries = [.. _entries];
            _entries.Clear();
            _card++;
        }

        var waited = new List<string>();
        var unwaited = new List<string>();
        int next = 0;
        foreach (var entry in entries)
        {
            string text = Format(entry.Questions, answers.Skip(next).Take(entry.Questions.Count).ToList());
            next += entry.Questions.Count;
            if (entry.Waiter is { } waiter)
            {
                waited.Add(text);
                waiter.TrySetResult(text);
            }
            else
                unwaited.Add(text);
        }
        Changed?.Invoke();
        return new QuestionAnswers(null, Join(waited), Join(unwaited));
    }

    public void Close()
    {
        List<Entry> entries;
        lock (_gate)
        {
            if (_entries.Count == 0)
                return;
            entries = [.. _entries];
            _entries.Clear();
            _card++;
        }
        foreach (var entry in entries)
            entry.Waiter?.TrySetResult(ClosedOutput);
        Changed?.Invoke();
    }

    public static string Format(IReadOnlyList<Question> questions, IReadOnlyList<string?> answers) =>
        string.Join("\n\n", questions.Select((q, i) =>
            $"Q: {q.Text}\nA: {(string.IsNullOrWhiteSpace(answers[i]) ? "(skipped)" : answers[i]!.Trim())}"));

    private static string? Join(List<string> texts) => texts.Count == 0 ? null : string.Join("\n\n", texts);

    private void Add(Entry entry)
    {
        lock (_gate)
            _entries.Add(entry);
        Changed?.Invoke();
    }
}

// Over the client tool loop the turn ends and the answers come as the next user message, so the
// Editor's VRAM is free while the user answers. Over MCP the call blocks until the card is answered.
public sealed class AskUserTool(QuestionBoard board, bool waitsForAnswers) : ITool
{
    public const string ToolName = "ask_user";

    public const string ShownOutput = "The questions are shown to the user. Their answers come as their next message.";

    public const string MalformedOutput =
        "Nothing was shown to the user. ask_user needs \"questions\": a list of objects, each with a \"question\" text and optional \"options\" texts. Send the call again.";

    public string Name => ToolName;

    public string Description =>
        $"Asks the user 1-4 questions and waits for their answers; you do not need to end your turn or call {WaitForUserTool.ToolName}. " +
        "Use it for every question to the user: a choice that is theirs, a name or text players will see, a folder, a value they did not give. " +
        "The user can pick one of your options, type their own answer, or skip.";

    public string Parameters => """
        {"type":"object","properties":{"questions":{"type":"array","minItems":1,"maxItems":4,"items":{"type":"object","properties":{"question":{"type":"string","description":"One specific question."},"options":{"type":"array","items":{"type":"string"},"description":"Optional short answers the user can pick; they can always type their own."}},"required":["question"]}}},"required":["questions"]}
        """;

    public bool Exclusive => false;

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (Parse(arguments) is not { } questions)
            return new ToolResult(MalformedOutput, Refused: true);
        if (!waitsForAnswers)
        {
            board.Post(questions);
            return new ToolResult(ShownOutput);
        }
        return new ToolResult(await board.WaitAsync(questions, cancellationToken).ConfigureAwait(false));
    }

    // Small models write the list as JSON text, or a question as a bare string; both are read.
    // Any unreadable entry refuses the whole call: a dropped question would be waited for forever.
    public static IReadOnlyList<Question>? Parse(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("questions", out var list))
            return null;
        if (list.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var document = JsonDocument.Parse(list.GetString()!);
                list = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }
        if (list.ValueKind != JsonValueKind.Array)
            return null;

        var questions = new List<Question>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString()!.Trim() is { Length: > 0 } bare)
            {
                questions.Add(new Question(bare, []));
                continue;
            }
            if (item.ValueKind != JsonValueKind.Object || ToolArgs.Text(item, "question") is not { Length: > 0 } text)
                return null;
            var options = new List<string>();
            if (item.TryGetProperty("options", out var given) && given.ValueKind == JsonValueKind.Array)
                foreach (var option in given.EnumerateArray())
                    if (option.ValueKind == JsonValueKind.String && option.GetString()!.Trim() is { Length: > 0 } o)
                        options.Add(o);
            questions.Add(new Question(text, options));
        }
        return questions.Count == 0 ? null : questions;
    }
}
